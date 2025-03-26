using System.Runtime.InteropServices;
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

        public async Task CopyFileAsync(string sourcePath, string destPath)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentNullException(nameof(sourcePath));
            if (string.IsNullOrEmpty(destPath))
                throw new ArgumentNullException(nameof(destPath));
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Source file not found.", sourcePath);
            
            if (File.Exists(destPath))
            {
                var destAttributes = File.GetAttributes(destPath);
                if ((destAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(destPath, destAttributes & ~FileAttributes.ReadOnly);
                }
            }
            
            try
            {
                await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                await using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                await sourceStream.CopyToAsync(destStream);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to copy file from {sourcePath} to {destPath}: {ex.Message}", ex);
            }
            
            try
            {
                File.SetLastWriteTime(destPath, File.GetLastWriteTime(sourcePath));

                File.SetLastAccessTime(destPath, File.GetLastAccessTime(sourcePath));

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || IsCreationTimeSupported(sourcePath))
                {
                    File.SetCreationTime(destPath, File.GetCreationTime(sourcePath));
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to copy file attributes from {sourcePath} to {destPath}: {ex.Message}", ex);
            }

            try
            {
                var sourceAttributes = File.GetAttributes(sourcePath);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    File.SetAttributes(destPath, sourceAttributes);
                }
                else
                {
                    if ((sourceAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(destPath, FileAttributes.ReadOnly);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to copy file attributes from {sourcePath} to {destPath}: {ex.Message}", ex);
            }
        }

        public async Task DeleteFileAsync(string path)
        {
            if (File.Exists(path))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
                }
            }
            
            await Task.Run(() => File.Delete(path));
        }

        public async Task MoveFileAsync(string sourcePath, string destPath)
        {
            await Task.Run(() => File.Move(sourcePath, destPath));
        }

        public FileInfo GetFileInfo(string path) => new FileInfo(path);
        public bool DirectoryExists(string path) => Directory.Exists(path);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public void CreateDirectoryIfNotExist(string directoryPath)
        {
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
        }

        public IEnumerable<string> GetFiles(string path) => Directory.EnumerateFiles(path);

        public IEnumerable<string> GetDirectories(string path) => Directory.EnumerateDirectories(path);

        public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

        public void MoveDirectory(string sourcePath, string destPath) => Directory.Move(sourcePath, destPath);
        
        public DirectoryInfo GetDirectoryInfo(string path) => new DirectoryInfo(path);
        
        
        public async Task CopyFolderAsync(string sourcePath, string destPath)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentNullException(nameof(sourcePath));
            if (string.IsNullOrEmpty(destPath))
                throw new ArgumentNullException(nameof(destPath));
            if (!Directory.Exists(sourcePath))
                throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
            
            CreateDirectoryIfNotExist(destPath);
            foreach (var file in Directory.GetFiles(sourcePath))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destPath, fileName);
                await CopyFileAsync(file, destFile);
            }
            
            foreach (var dir in Directory.GetDirectories(sourcePath))
            {
                string dirName = Path.GetFileName(dir);
                string destDir = Path.Combine(destPath, dirName);
                await CopyFolderAsync(dir, destDir);
            }
            
            try
            {
                Directory.SetLastWriteTime(destPath, Directory.GetLastWriteTime(sourcePath));
                Directory.SetLastAccessTime(destPath, Directory.GetLastAccessTime(sourcePath));
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || IsCreationTimeSupported(sourcePath))
                {
                    Directory.SetCreationTime(destPath, Directory.GetCreationTime(sourcePath));
                }
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    File.SetAttributes(destPath, File.GetAttributes(sourcePath));
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to copy directory attributes from {sourcePath} to {destPath}: {ex.Message}", ex);
            }
        }

        private bool IsCreationTimeSupported(string path)
        {
            try
            {
                File.GetCreationTime(path);
                return true;
            }
            catch (Exception)
            {

                return false;
            }
        }

        public HashSet<string> GetAllFiles(string path)
            {
                var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!Directory.Exists(path)) return files;
            
                var directories = new Stack<string>();
                directories.Push(path);
            
                while (directories.Count > 0)
                {
                    string currentDir = directories.Pop();
                
                    try
                    {
                        files.UnionWith(Directory.EnumerateFiles(currentDir));
                    
                        foreach (var dir in Directory.EnumerateDirectories(currentDir))
                        {
                            directories.Push(dir);
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        throw new UnauthorizedAccessException($"Access to directory {currentDir} is denied: {ex.Message}", ex);
                    }
                    catch (IOException ex)
                    {
                        throw new IOException($"Error accessing directory {currentDir}: {ex.Message}", ex);
                    }
                }
            
                return files;
            }

            public HashSet<string> GetAllFolders(string path)
            {
            
                var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!Directory.Exists(path)) return folders;
            
                var directories = new Stack<string>();
                directories.Push(path);
            
                while (directories.Count > 0)
                {
                    string currentDir = directories.Pop();
                
                    try
                    {
                        foreach (var dir in Directory.EnumerateDirectories(currentDir))
                        {
                            folders.Add(dir);
                            directories.Push(dir);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        throw new UnauthorizedAccessException($"Access to directory {currentDir} is denied");
                    }
                    catch (IOException)
                    {
                        throw new IOException($"Error accessing directory {currentDir}");
                    }
                }
            
                return folders;
            }
    }
}
