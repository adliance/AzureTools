using CommandLine;

namespace Adliance.AzureTools.MirrorStorage.Parameters
{
    [Verb("mirror-storage-to-storage", HelpText = "Copies all containers and blobs from one Azure Storage account to another one.")]
    public class MirrorStorageToStorageParameters
    {
        [Option('s', "source", Required = true, Default = "", HelpText = "The connection string to the source storage account.")] public string Source { get; set; } = "";
        [Option('t', "target", Required = true, Default = "", HelpText = "The connection string to the target storage account.")] public string Target { get; set; } = "";
        
        [Option('d', "delete", Required = false, Default = true, HelpText = "If true, all containers and blobs that do not exist in the source will be deleted from the target.")] public bool Delete { get; set; } = true;
    }
}
