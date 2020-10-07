using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
                    DownloadDatabase(_parameters.Source, fileName);
                }

                var temporaryDbName = targetDbName + "_" + Guid.NewGuid();
                Console.WriteLine($"Restoring to temporary database \"{temporaryDbName}\" ...");
                RestoreDatabase(_parameters.Target.Replace(targetDbName, temporaryDbName), fileName);

                await using (var connection = new SqlConnection(_parameters.Target.Replace(targetDbName, "master")))
                {
                    await connection.OpenAsync();

                    var databaseExists = await SqlScalar(connection, $"SELECT COUNT(*) from master.sys.databases where name='{targetDbName}';");
                    if (databaseExists is int i && i > 0)
                    {
                        Console.WriteLine("Deleting existing database ...");
                        try
                        {
                            await SqlCommand(connection, $"ALTER DATABASE [{targetDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;");
                        }
                        catch 
                        {
                           // do nothing here, fails in Azure but has no effect usually
                        }

                        try
                        {
                            await SqlCommand(connection, $"DROP DATABASE [{targetDbName}];");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Unable to delete existing database \"{targetDbName}\": {ex.Message}");
                        }
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

        private void DownloadDatabase(string connectionString, string fileName)
        {
            RunSqlPackage(
                "/Action:Export",
                $" /TargetFile:\"{fileName}\"",
                $" /SourceConnectionString:\"{connectionString}\"");
        }

        private void RestoreDatabase(string connectionString, string fileName)
        {
            RunSqlPackage(
                "/Action:Import",
                $" /SourceFile:\"{fileName}\"",
                $" /TargetConnectionString:\"{connectionString}\"");
        }

        private void RunSqlPackage(params string[] arguments)
        {
            var codeBase = Assembly.GetExecutingAssembly().CodeBase ?? "";
            var assemblyPath = Uri.UnescapeDataString(new UriBuilder(codeBase).Path);
            var sqlPackagePath = new FileInfo(Path.Combine(Path.GetDirectoryName(assemblyPath) ?? "", "CopyDatabase/sqlpackage/sqlpackage.exe"));

            if (!sqlPackagePath.Exists)
            {
                throw new Exception($"{sqlPackagePath.FullName} does not exist.");
            }

            var pi = new ProcessStartInfo(sqlPackagePath.FullName)
            {
                Arguments = string.Join(" ", arguments)
            };

            var currentConsoleColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            var process = Process.Start(pi);
            if (process == null)
            {
                throw new Exception("Process is null.");
            }

            process.WaitForExit();
            Console.ForegroundColor = currentConsoleColor;

            if (process.ExitCode != 0)
            {
                throw new Exception($"sqlpackage failed (exit code {process.ExitCode}.");
            }
        }

        private string FindDatabaseName(string connectionString)
        {
            var match = Regex.Match(connectionString, @"[ ;]*Initial Catalog[ ]*\=(.*?)[;$]", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            throw new Exception($"No database name found in \"{connectionString}\".");
        }
    }
}