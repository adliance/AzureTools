using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Adliance.AzureTools.MirrorStorage
{
    public interface IStorage
    {
        Task<IList<Container>> Enumerate();

        Task DownloadTo(string containerName, string fileName, IStorage target);
        Task UploadFrom(string containerName, string fileName, string temporaryFileName);
        Task Delete(string containerName, string fileName);
        Task CreateContainer(string containerName);
        Task DeleteContainer(string containerName);
    }

    public class Container
    {
        public Container(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public IList<Blob> Blobs { get; } = new List<Blob>();
        public long Size => Blobs.Sum(x => x.Size);

        public override string ToString()
        {
            return Name;
        }
    }

    public class Blob
    {
        public Blob(string name, long size)
        {
            Name = name;
            Size = size;
        }

        public string Name { get; }
        public long Size { get; }

        public override string ToString()
        {
            return Name;
        }
    }
}