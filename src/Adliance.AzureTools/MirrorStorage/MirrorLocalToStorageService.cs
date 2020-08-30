using System.Threading.Tasks;
using Adliance.AzureTools.Parameters;

namespace Adliance.AzureTools.MirrorStorage
{
    public class MirrorLocalToStorageService
    {
        private readonly MirrorLocalToStorageParameters _parameters;

        public MirrorLocalToStorageService(MirrorLocalToStorageParameters parameters)
        {
            _parameters = parameters;
        }

        public async Task Run()
        {
            await new MirrorStorageService(new LocalStorage(_parameters.Source), new AzureStorage(_parameters.Target), _parameters.Delete).Run();
        }
    }
}