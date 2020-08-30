using System;
using System.Threading.Tasks;
using Adliance.AzureTools.CopyDatabase;
using Adliance.AzureTools.CopyDatabase.Parameters;
using Adliance.AzureTools.MirrorStorage;
using Adliance.AzureTools.MirrorStorage.Parameters;
using CommandLine;
using CommandLine.Text;

namespace Adliance.AzureTools
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Program
    {
        private const int ExitCodeOk = 0;
        private const int ExitCodeError = -1;
        private const int ExitCodeParameters = -2;

        private static async Task Main(string[] args)
        {
            var parserResult = Parser.Default.ParseArguments<
                MirrorStorageToStorageParameters,
                MirrorStorageToLocalParameters,
                MirrorLocalToLocalParameters,
                MirrorLocalToStorageParameters,
                CopyDatabaseParameters>(args);
            await parserResult.WithParsedAsync<MirrorStorageToStorageParameters>(async p =>
            {
                await new MirrorStorageToStorageService(p).Run();
                Exit(ExitCodeOk);
            });
            await parserResult.WithParsedAsync<MirrorStorageToLocalParameters>(async p =>
            {
                await new MirrorStorageToLocalService(p).Run();
                Exit(ExitCodeOk);
            });
            await parserResult.WithParsedAsync<MirrorLocalToLocalParameters>(async p =>
            {
                await new MirrorLocalToLocalService(p).Run();
                Exit(ExitCodeOk);
            });
            await parserResult.WithParsedAsync<MirrorLocalToStorageParameters>(async p =>
            {
                await new MirrorLocalToStorageService(p).Run();
                Exit(ExitCodeOk);
            });
            await parserResult.WithParsedAsync<CopyDatabaseParameters>(async p =>
            {
                await new CopyDatabaseService(p).Run();
                Exit(ExitCodeOk);
            });

            parserResult.WithNotParsed(errs =>
            {
                var helpText = HelpText.AutoBuild(parserResult, h => HelpText.DefaultParsingErrorsHandler(parserResult, h), e => e);
                Console.Error.Write(helpText);
                Exit(ExitCodeParameters);
            });
        }

        private static void Exit(int code, string? message = null)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine(message);
            }

            Environment.Exit(code);
        }

        public static void Exit(Exception ex)
        {
            Exit(ExitCodeError, ex.Message);
        }
    }
}