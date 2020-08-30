using System.Threading.Tasks;
using Adliance.AzureTools.Parameters;

namespace Adliance.AzureTools.MirrorStorage
{
    public class MirrorStorageToStorageService
    {
        private readonly MirrorStorageToStorageParameters _parameters;

        public MirrorStorageToStorageService(MirrorStorageToStorageParameters parameters)
        {
            _parameters = parameters;
        }

        public async Task Run()
        {
            await new MirrorStorageService(new AzureStorage(_parameters.Source), new AzureStorage(_parameters.Target), _parameters.Delete).Run();
        }
    }
}