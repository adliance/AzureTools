using CommandLine;

namespace Adliance.AzureTools.Parameters
{
    [Verb("mirror-local-to-storage", HelpText = "Copies all subdirectories and files from a local directory to an Azure Storage account.")]
    public class MirrorLocalToStorageParameters
    {
        [Option('s', "source", Required = true, Default = "", HelpText = "The path of the local source directory.")] public string Source { get; set; } = "";
        [Option('t', "target", Required = true, Default = "", HelpText = "The connection string to the target storage account.")] public string Target { get; set; } = "";
      
        [Option('d', "delete", Required = false, Default = true, HelpText = "If true, all files that do not exist in the source will be deleted from the target.")] public bool Delete { get; set; } = true;
    }
}
