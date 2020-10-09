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

                var forceOperation = _parameters.Force;
                if (forceOperation)
                {
                    Console.WriteLine("[Force] Skipping connection string check...");
                }
                else
                {
                    var azureDbStringResult = AnalyzeAzureDbString(_parameters.Target);
                    
                    // User should confirm operation, since we're going to manipulate an azure-hosted database
                    if (azureDbStringResult.IsAzureConnectionString)
                    {
                        var confirmationResult = UserConfirmAzureDatabaseTarget(azureDbStringResult.AzureDbSubdomain);
                        if (confirmationResult == AzureTargetConfirmation.AbortOperation)
                        {
                            Console.WriteLine("Exiting...");
                            return;
                        }                        
                    }
                }
                
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

        private enum AzureTargetConfirmation
        {
            AbortOperation,
            Confirmed
        }
        
        private AzureTargetConfirmation UserConfirmAzureDatabaseTarget(string targetAzureDbUrl)
        {
            Console.WriteLine("[CRITICAL] The defined target is hosted at Microsoft Azure. This operation will overwrite the target with the defined source. Without a backup, all data could be lost forever.");

            var maxRetries = 3;
            for (var retries = maxRetries; retries > 0; --retries)
            {
                Console.WriteLine($"Enter '{targetAzureDbUrl}' to confirm the operation, to abort leave empty and hit enter ({retries} retries left):");
                    
                var confirmationString = Console.ReadLine();
                if (string.IsNullOrEmpty(confirmationString))
                {
                    Console.WriteLine("Aborting copy-database operation...");
                    return AzureTargetConfirmation.AbortOperation;
                }
                
                if (confirmationString == targetAzureDbUrl)
                {
                    Console.WriteLine("Confirmed, continue operation...");
                    return AzureTargetConfirmation.Confirmed;
                }
            }
            
            Console.WriteLine("Too may retries. Aborting copy-database operation...");
            return AzureTargetConfirmation.AbortOperation;
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

        private struct AzureConnectionStringResult
        {
            public bool IsAzureConnectionString { get; set; }
            public string AzureDbSubdomain { get; set; }
        }
        
        private AzureConnectionStringResult AnalyzeAzureDbString(string connectionString)
        {
            var match = Regex.Match(connectionString, @"[ ;]*Server[ ]*\=[ ]*tcp:(.*?)\.database\.windows\.net.*[;$]", RegexOptions.IgnoreCase);

            return new AzureConnectionStringResult
            {
                IsAzureConnectionString = match.Success,
                AzureDbSubdomain = match.Success && match.Groups.Count >= 2 ? match.Groups[1].Value.Trim() : "" 
            };
        }
    }
}