using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Adliance.AzureTools.CopyDatabase.Parameters;

namespace Adliance.AzureTools.CopyDatabase
{
    public class CopyDatabaseService
    {
        private readonly CopyDatabaseParameters _parameters;

        public CopyDatabaseService(CopyDatabaseParameters parameters)
        {
            _parameters = parameters;
        }

        public async Task Run()
        {
            try
            {
                var sourceDbName = FindDatabaseName(_parameters.Source);
                var targetDbName = FindDatabaseName(_parameters.Target);
                var fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $"{sourceDbName}.bacpac");

                if (_parameters.UseLocalIfExists && File.Exists(fileName))
                {
                    Console.WriteLine($"Using local file \"{fileName}\".");
                }
                else
                {
                    Console.WriteLine($"Downloading database to \"{fileName}\" ...");
                    await DownloadDatabase(_parameters.Source, fileName);
                }

                var temporaryDbName = targetDbName + "_" + Guid.NewGuid();
                Console.WriteLine($"Restoring to temporary database \"{temporaryDbName}\" ...");
                await RestoreDatabase(_parameters.Target.Replace(targetDbName, temporaryDbName), fileName);

                await using (var connection = new SqlConnection(_parameters.Target.Replace(targetDbName, "master")))
                {
                    await connection.OpenAsync();

                    if (await SqlScalar(connection, $"SELECT db_id('{targetDbName}');") != DBNull.Value)
                    {
                        Console.WriteLine("Deleting existing database ...");
                        await SqlCommand(connection, $"ALTER DATABASE [{targetDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;");
                        await SqlCommand(connection, $"DROP DATABASE [{targetDbName}];");
                    }
                    
                    Console.WriteLine($"Renaming temporary database to \"{targetDbName}\" ...");
                    await SqlCommand(connection, $"ALTER DATABASE [{temporaryDbName}] MODIFY NAME = [{targetDbName}];");
                    
                    if (!string.IsNullOrWhiteSpace(_parameters.ElasticPool))
                    {
                        Console.WriteLine($"Setting elastic pool to \"{_parameters.ElasticPool}\" ...");
                        await SqlCommand(connection, $"ALTER DATABASE [{targetDbName}] MODIFY ( SERVICE_OBJECTIVE = ELASTIC_POOL ( name = [{_parameters.ElasticPool}] ) );");
                    }
                }

                Console.WriteLine("Everything done.");
            }
            catch (Exception ex)
            {
                Program.Exit(ex);
            }
        }

        private async Task SqlCommand(SqlConnection connection, string sql)
        {
            await using (var command = new SqlCommand(sql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }
        
        private async Task<object> SqlScalar(SqlConnection connection, string sql)
        {
            await using (var command = new SqlCommand(sql, connection))
            {
                return await command.ExecuteScalarAsync();
            }
        }

        private async Task DownloadDatabase(string connectionString, string fileName)
        {
            await RunSqlPackage(
                "/Action:Export",
                $" /TargetFile:\"{fileName}\"",
                $" /SourceConnectionString:\"{connectionString}\"");
        }

        private async Task RestoreDatabase(string connectionString, string fileName)
        {
            await RunSqlPackage(
                "/Action:Import",
                $" /SourceFile:\"{fileName}\"",
                $" /TargetConnectionString:\"{connectionString}\"");
        }

        private async Task RunSqlPackage(params string[] arguments)
        {
            var sqlPackage = new FileInfo("CopyDatabase/sqlpackage/sqlpackage.exe");
            if (!sqlPackage.Exists)
            {
                throw new Exception($"{sqlPackage.FullName} does not exist.");
            }

            var pi = new ProcessStartInfo(sqlPackage.FullName)
            {
                Arguments = string.Join(" ", arguments),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = Process.Start(pi);
            if (process == null)
            {
                throw new Exception("Process is null.");
            }

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception(await process.StandardError.ReadToEndAsync());
            }
        }

        private string FindDatabaseName(string connectionString)
        {
            var match = Regex.Match(connectionString, @"[ ;]Initial Catalog[ ]*\=(.*?)[;$]", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            throw new Exception($"No database name found in \"{connectionString}\".");
        }
    }
}