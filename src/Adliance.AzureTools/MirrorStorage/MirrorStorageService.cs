using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Humanizer;

namespace Adliance.AzureTools.MirrorStorage
{
    public class MirrorStorageService
    {
        private readonly IStorage _source;
        private readonly IStorage _target;
        private readonly bool _delete;

        public MirrorStorageService(IStorage source, IStorage target, bool delete)
        {
            _source = source;
            _target = target;
            _delete = delete;
        }

        public async Task Run()
        {
            try
            {
                Console.WriteLine("Enumerating source ...");
                var sourceContent = await _source.Enumerate();
                Console.WriteLine($"\t {"container".ToQuantity(sourceContent.Count)} ({"file".ToQuantity(sourceContent.Sum(x => x.Blobs.Count))}) with a total of {sourceContent.Sum(x => x.Size).Bytes().Humanize("#.##")}.");

                Console.WriteLine("Enumerating target ...");
                var targetContent = await _target.Enumerate();
                Console.WriteLine($"\t {"container".ToQuantity(targetContent.Count)} ({"file".ToQuantity(targetContent.Sum(x => x.Blobs.Count))}) with a total of {targetContent.Sum(x => x.Size).Bytes().Humanize("#.##")}.");

                Console.WriteLine("Calculating differences ...");
                var filesToCopy = FindFilesToCopy(sourceContent, targetContent);
                var containersToCreate = FindContainersToCreate(sourceContent, targetContent);
                Console.WriteLine($"\t {"file".ToQuantity(filesToCopy.Sum(x => x.Blobs.Count))} to copy with a total of {filesToCopy.Sum(x => x.Size).Bytes().Humanize("#.##")}.");
                var filestoDelete = FindFilesToDelete(sourceContent, targetContent);
                var containersToDelete = FindContainersToDelete(sourceContent, targetContent);
                Console.WriteLine($"\t {"file".ToQuantity(filestoDelete.Sum(x => x.Blobs.Count))} to delete with a total of {filestoDelete.Sum(x => x.Size).Bytes().Humanize("#.##")}.");
                
                await CreateContainers(containersToCreate);
                await CopyFiles(filesToCopy);

                if (_delete)
                {
                    await DeleteFiles(filestoDelete);
                    await DeleteContainers(containersToDelete);
                }

                Console.WriteLine("Everything done.");
            }
            catch (Exception ex)
            {
                Program.Exit(ex);
            }
        }


        private async Task CreateContainers(IList<string> containersToCreate)
        {
            if (!containersToCreate.Any())
            {
                return;
            }
            
            Console.WriteLine("Creating containers on target ...");
            var totalCount = containersToCreate.Count;
            var currentCount = 0;
            foreach (var c in containersToCreate)
            {
                Console.Write($"\t ({++currentCount}/{totalCount}) Creating {c} ... ");

                try
                {
                    await _target.CreateContainer(c);
                    Console.WriteLine("completed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
        
        private async Task DeleteContainers(IList<string> containersToDelete)
        {
            if (!containersToDelete.Any())
            {
                return;
            }
            
            Console.WriteLine("Deleting containers on target ...");
            var totalCount = containersToDelete.Count;
            var currentCount = 0;
            foreach (var c in containersToDelete)
            {
                Console.Write($"\t ({++currentCount}/{totalCount}) Deleting {c} ... ");
                
                try
                {
                    await _target.DeleteContainer(c);
                    Console.WriteLine("completed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private async Task CopyFiles(IList<Container> filesToCopy)
        {
            if (!filesToCopy.Any())
            {
                return;
            }
            
            Console.WriteLine("Copying files from source to target ...");
            var totalCount = filesToCopy.Sum(x => x.Blobs.Count);
            var currentCount = 0;
            foreach (var c in filesToCopy)
            {
                foreach (var b in c.Blobs)
                {
                    Console.Write($"\t ({++currentCount}/{totalCount}) Copying {c.Name}/{b.Name} ({b.Size.Bytes().Humanize("#.##")}) ... ");

                    try
                    {
                        await _source.DownloadTo(c.Name, b.Name, _target);
                        Console.WriteLine("completed.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }

        private async Task DeleteFiles(IList<Container> filesToDelete)
        {
            if (!filesToDelete.Any())
            {
                return;
            }
            
            Console.WriteLine("Deleting files from target ...");
            var totalCount = filesToDelete.Sum(x => x.Blobs.Count);
            var currentCount = 0;
            foreach (var c in filesToDelete)
            {
                foreach (var b in c.Blobs)
                {
                    Console.Write($"\t ({++currentCount}/{totalCount}) Deleting {c.Name}/{b.Name} ({b.Size.Bytes().Humanize("#.##")}) ... ");

                    try
                    {
                        await _target.Delete(c.Name, b.Name);
                        Console.WriteLine("completed.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }
        
        private IList<Container> FindFilesToCopy(IList<Container> source, IList<Container> target)
        {
            var result = new List<Container>();
            foreach (var c in source)
            {
                Container? container = null;

                foreach (var b in c.Blobs)
                {
                    var existing = target.FirstOrDefault(x => x.Name == c.Name)?.Blobs.FirstOrDefault(x => x.Name == b.Name);
                    if (existing != null && existing.Size == b.Size)
                    {
                        continue;
                    }

                    if (container == null)
                    {
                        container = new Container(c.Name);
                        result.Add(container);
                    }

                    container.Blobs.Add(new Blob(b.Name, b.Size));
                }
            }

            return result;
        }

        private IList<Container> FindFilesToDelete(IList<Container> source, IList<Container> target)
        {
            var result = new List<Container>();
            foreach (var c in target)
            {
                Container? container = null;

                foreach (var b in c.Blobs)
                {
                    var existing = source.FirstOrDefault(x => x.Name == c.Name && x.Blobs.Any(y => y.Name == b.Name));
                    if (existing != null)
                    {
                        continue;
                    }

                    if (container == null)
                    {
                        container = new Container(c.Name);
                        result.Add(container);
                    }

                    container.Blobs.Add(new Blob(b.Name, b.Size));
                }
            }

            return result;
        }

        private IList<string> FindContainersToDelete(IList<Container> source, IList<Container> target)
        {
            var result = new List<string>();
            foreach (var c in target)
            {
                var existing = source.FirstOrDefault(x => x.Name == c.Name);
                if (existing != null)
                {
                    continue;
                }
                result.Add(c.Name);
            }

            return result;
        }
        
        private IList<string> FindContainersToCreate(IList<Container> source, IList<Container> target)
        {
            var result = new List<string>();
            foreach (var c in source)
            {
                var existing = target.FirstOrDefault(x => x.Name == c.Name);
                if (existing != null)
                {
                    continue;
                }
                result.Add(c.Name);
            }

            return result;
        }
    }
}