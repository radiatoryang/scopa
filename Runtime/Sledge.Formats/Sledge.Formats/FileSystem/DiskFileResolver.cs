using System.IO;

namespace Sledge.Formats.FileSystem
{
    public class DiskFileResolver : IFileResolver
    {
        private readonly string _basePath;
        public DiskFileResolver(string basePath) => _basePath = basePath;
        public Stream OpenFile(string path) => File.Open(Path.Combine(_basePath, path), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        public string[] OpenFolder(string path) => Directory.GetFiles(Path.Combine(_basePath, path));
    }
}