using System.Threading.Tasks;
using Adliance.AzureTools.Parameters;

namespace Adliance.AzureTools.MirrorStorage
{
    public class MirrorStorageToLocalService
    {
        private readonly MirrorStorageToLocalParameters _parameters;

        public MirrorStorageToLocalService(MirrorStorageToLocalParameters parameters)
        {
            _parameters = parameters;
        }

        public async Task Run()
        {
            await new MirrorStorageService(new AzureStorage(_parameters.Source), new LocalStorage(_parameters.Target), _parameters.Delete).Run();
        }
    }
}