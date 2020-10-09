using CommandLine;

namespace Adliance.AzureTools.CopyDatabase.Parameters
{
    [Verb("copy-database", HelpText = "Copies an existing database (structure and data) to another one. If the target database already exists, it will be overwritten. Please note that this uses SqlPackage and currently only works on Windows.")]
    public class CopyDatabaseParameters
    {
        [Option('s', "source", Required = true, Default = "", HelpText = "The connection string to the source database.")] public string Source { get; set; } = "";
        [Option('t', "target", Required = true, Default = "", HelpText = "The connection string to the target database.")] public string Target { get; set; } = "";
        
        [Option( 'p',"elastic-pool", Required = false, Default = "", HelpText = "If set, the target database will be moved to the specified elastic pool.")] public string ElasticPool { get; set; } = "";
        [Option( 'l', "use-local-bacpac", Required = false, Default = false, HelpText = "If true and a local BACPAC file exists, then it will be used instead of downloading a new one. This is useful if you want to restore a database often, but not download it all the time.")] public bool UseLocalIfExists { get; set; } = false;

        [Option('f', "force", Required = false, Default = false, HelpText = "Force copy-database operation and skip user confirmation or interaction")] public bool Force { get; set; } = false;
    }
}
