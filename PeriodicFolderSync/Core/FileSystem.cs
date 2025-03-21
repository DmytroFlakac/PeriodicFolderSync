using PeriodicFolderSync.Interfaces;

namespace PeriodicFolderSync.Core
{
    public class FileSystem : IFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);

        public Task<byte[]> ReadAllBytesAsync(string path) => File.ReadAllBytesAsync(path);

        public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);

        public Task WriteAllBytesAsync(string path, byte[] bytes) => File.WriteAllBytesAsync(path, bytes);

        public Task WriteAllTextAsync(string path, string contents) => File.WriteAllTextAsync(path, contents);

        public async Task CopyFileAsync(string sourcePath, string destPath, bool overwrite = false)
        {
            await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            await using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await sourceStream.CopyToAsync(destStream);
        }

        public async Task DeleteFileAsync(string path)
        {
            await Task.Run(() => File.Delete(path));
        }

        public async Task MoveFileAsync(string sourcePath, string destPath, bool overwrite = false)
        {
            await Task.Run(() => File.Move(sourcePath, destPath));
        }

        public long GetFileSize(string path) => new FileInfo(path).Length;
        public bool DirectoryExists(string path) => Directory.Exists(path);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public void CreateDirectoryIfNotExist(string directoryPath)
        {
            string? directory = Path.GetDirectoryName(directoryPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        public IEnumerable<string> GetFiles(string path) => Directory.EnumerateFiles(path);

        public IEnumerable<string> GetDirectories(string path) => Directory.EnumerateDirectories(path);

        public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

        public void MoveDirectory(string sourcePath, string destPath) => Directory.Move(sourcePath, destPath);
    }
}