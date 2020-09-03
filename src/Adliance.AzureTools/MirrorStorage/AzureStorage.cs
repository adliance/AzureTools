using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;

namespace Adliance.AzureTools.MirrorStorage
{
    public class AzureStorage : IStorage
    {
        private readonly BlobServiceClient _client;

        public AzureStorage(string connectionString)
        {
            _client = new BlobServiceClient(connectionString);
        }

        public async Task<IList<Container>> Enumerate()
        {
            var result = new List<Container>();
            await foreach (var c in _client.GetBlobContainersAsync())
            {
                var containerClient = _client.GetBlobContainerClient(c.Name);
                var container = new Container(c.Name);
                result.Add(container);
                await foreach (var blob in containerClient.GetBlobsAsync())
                {
                    container.Blobs.Add(new Blob(blob.Name, blob.Properties.ContentLength ?? 0));
                }
            }

            return result;
        }

        public async Task DownloadTo(string containerName, string fileName, IStorage target)
        {
            var containerClient = _client.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(fileName);

            var tempFile = Path.GetTempFileName();
            
            // for some reason, the download to a file is much, much, much faster than working with streams directly
            // either I did something wrong with the streams, or whatever, this is quite fast now
            await blobClient.DownloadToAsync(tempFile); 
            await target.UploadFrom(containerName, fileName, tempFile);

            if (File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // do nothing here
                }
            }
        }

        public async Task UploadFrom(string containerName, string fileName, string temporaryFileName)
        {
            var containerClient = _client.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(fileName);
            await blobClient.UploadAsync(temporaryFileName, true);
        }

        public async Task Delete(string containerName, string fileName)
        {
            var containerClient = _client.GetBlobContainerClient(containerName);
            await containerClient.DeleteBlobIfExistsAsync(fileName);
        }
        
        public async Task CreateContainer(string containerName)
        {
            var containerClient = _client.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
        }
        
        public async Task DeleteContainer(string containerName)
        {
            var containerClient = _client.GetBlobContainerClient(containerName);
            await containerClient.DeleteIfExistsAsync();
        }
    }
}