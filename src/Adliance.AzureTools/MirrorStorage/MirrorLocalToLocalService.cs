using System.Threading.Tasks;
using Adliance.AzureTools.Parameters;

namespace Adliance.AzureTools.MirrorStorage
{
    public class MirrorLocalToLocalService
    {
        private readonly MirrorLocalToLocalParameters _parameters;

        public MirrorLocalToLocalService(MirrorLocalToLocalParameters parameters)
        {
            _parameters = parameters;
        }

        public async Task Run()
        {
            await new MirrorStorageService(new LocalStorage(_parameters.Source), new LocalStorage(_parameters.Target), _parameters.Delete).Run();
        }
    }
}