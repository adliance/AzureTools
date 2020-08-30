using CommandLine;

namespace Adliance.AzureTools.MirrorStorage.Parameters
{
    [Verb("mirror-storage-to-local", HelpText = "Copies all containers and blobs from one Azure Storage account to a local directory.")]
    public class MirrorStorageToLocalParameters
    {
        [Option('s', "source", Required = true, Default = "", HelpText = "The connection string to the source storage account.")] public string Source { get; set; } = "";
        [Option('t', "target", Required = true, Default = "", HelpText = "The path of the target local directory.")] public string Target { get; set; } = "";
        
        [Option('d', "delete", Required = false, Default = true, HelpText = "If true, all containers and blobs that do not exist in the source will be deleted from the target.")] public bool Delete { get; set; } = true;
    }
}
