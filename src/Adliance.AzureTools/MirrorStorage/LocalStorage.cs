using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Adliance.AzureTools.MirrorStorage
{
    public class LocalStorage : IStorage
    {
        private readonly string _basePath;

        public LocalStorage(string basePath)
        {
            _basePath = basePath;

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }
        }

        public async Task<IList<Container>> Enumerate()
        {
            var result = new List<Container>();
            foreach (var d in Directory.GetDirectories(_basePath).Select(x => new DirectoryInfo(x)))
            {
                var container = new Container(d.Name);
                result.Add(container);
                foreach (var f in d.GetFiles())
                {
                    container.Blobs.Add(new Blob(f.Name, f.Length));
                }
            }

            return await Task.FromResult(result);
        }

        public async Task DownloadTo(string containerName, string fileName, IStorage target)
        {
            var filePath = Path.Combine(_basePath, containerName, fileName);
            await using (var sourceStream = File.OpenRead(filePath))
            {
                await target.UploadFrom(containerName, fileName, sourceStream);
            }
        }

        public async Task UploadFrom(string containerName, string fileName, Stream sourceStream)
        {
            var filePath = Path.Combine(_basePath, containerName, fileName);
            await using (var targetStream = File.OpenWrite(filePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }
        }

        public Task Delete(string containerName, string fileName)
        {
            var filePath = Path.Combine(_basePath, containerName, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return Task.CompletedTask;
        }

        public Task CreateContainer(string containerName)
        {
            if (!Directory.Exists(Path.Combine(_basePath, containerName)))
            {
                Directory.CreateDirectory(Path.Combine(_basePath, containerName));
            }

            return Task.CompletedTask;
        }
        
        public Task DeleteContainer(string containerName)
        {
            if (Directory.Exists(Path.Combine(_basePath, containerName)))
            {
                Directory.Delete(Path.Combine(_basePath, containerName));
            }

            return Task.CompletedTask;
        }
    }
}