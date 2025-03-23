using PeriodicFolderSync.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FolderSync.Tests.Mocks
{
    public class MockFileSystem : IFileSystem
    {
        private readonly Dictionary<string, byte[]> _files = new();
        private readonly HashSet<string> _directories = new();
        private readonly Dictionary<string, DateTime> _fileTimestamps = new(StringComparer.OrdinalIgnoreCase);

        public MockFileSystem()
        {
            _directories.Add("C:\\");
            _directories.Add("D:\\");
        }

        public bool FileExists(string path) => _files.ContainsKey(NormalizePath(path));

        public Task<byte[]> ReadAllBytesAsync(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (!_files.TryGetValue(normalizedPath, out var file))
                throw new FileNotFoundException($"File not found: {path}");

            return Task.FromResult(file);
        }

        public async Task<string> ReadAllTextAsync(string path)
        {
            byte[] bytes = await ReadAllBytesAsync(path);
            return Encoding.UTF8.GetString(bytes);
        }

        public Task WriteAllBytesAsync(string path, byte[] bytes)
        {
            string normalizedPath = NormalizePath(path);
            CreateDirectoryIfNotExist(normalizedPath);
            _files[normalizedPath] = bytes;
            return Task.CompletedTask;
        }

        public Task WriteAllTextAsync(string path, string contents)
        {
            return WriteAllBytesAsync(path, Encoding.UTF8.GetBytes(contents));
        }

        public Task CopyFileAsync(string sourcePath, string destPath, bool overwrite = false)
        {
            string normalizedSourcePath = NormalizePath(sourcePath);
            string normalizedDestPath = NormalizePath(destPath);

            if (!_files.ContainsKey(normalizedSourcePath))
                throw new FileNotFoundException($"Source file not found: {sourcePath}");

            if (_files.ContainsKey(normalizedDestPath) && !overwrite)
                throw new IOException($"Destination file already exists: {destPath}");

            CreateDirectoryIfNotExist(normalizedDestPath);
            _files[normalizedDestPath] = _files[normalizedSourcePath].ToArray(); 
            return Task.CompletedTask;
        }

        public Task DeleteFileAsync(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (_files.ContainsKey(normalizedPath))
                _files.Remove(normalizedPath);
            return Task.CompletedTask;
        }

        public Task MoveFileAsync(string sourcePath, string destPath, bool overwrite = false)
        {
            string normalizedSourcePath = NormalizePath(sourcePath);
            string normalizedDestPath = NormalizePath(destPath);

            if (!_files.ContainsKey(normalizedSourcePath))
                throw new FileNotFoundException($"Source file not found: {sourcePath}");

            if (_files.ContainsKey(normalizedDestPath) && !overwrite)
                throw new IOException($"Destination file already exists: {destPath}");

            CreateDirectoryIfNotExist(normalizedDestPath);
            _files[normalizedDestPath] = _files[normalizedSourcePath];
            _files.Remove(normalizedSourcePath);
            return Task.CompletedTask;
        }
        
        public bool DirectoryExists(string path) 
        {
            string normalizedPath = NormalizePath(path);
            if (string.IsNullOrEmpty(normalizedPath))
                return false;
                
            if (_directories.Contains(normalizedPath))
                return true;
            
            string dirWithSeparator = normalizedPath.EndsWith(Path.DirectorySeparatorChar) 
                ? normalizedPath 
                : normalizedPath + Path.DirectorySeparatorChar;
                
            return _directories.Any(d => d.StartsWith(dirWithSeparator, StringComparison.OrdinalIgnoreCase)) ||
                   _files.Keys.Any(f => f.StartsWith(dirWithSeparator, StringComparison.OrdinalIgnoreCase));
        }

        public void CreateDirectory(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (string.IsNullOrEmpty(normalizedPath))
                return;
                
            if (!normalizedPath.EndsWith(Path.DirectorySeparatorChar))
                normalizedPath += Path.DirectorySeparatorChar;
                
            _directories.Add(normalizedPath);
            
            string? parent = Path.GetDirectoryName(normalizedPath.TrimEnd(Path.DirectorySeparatorChar));
            while (!string.IsNullOrEmpty(parent))
            {
                string normalizedParent = NormalizePath(parent);
                if (!normalizedParent.EndsWith(Path.DirectorySeparatorChar))
                    normalizedParent += Path.DirectorySeparatorChar;
                    
                _directories.Add(normalizedParent);
                parent = Path.GetDirectoryName(normalizedParent.TrimEnd(Path.DirectorySeparatorChar));
            }
        }

        public void CreateDirectoryIfNotExist(string directoryPath)
        {
            string? directory = Path.GetDirectoryName(directoryPath);
            if (!string.IsNullOrEmpty(directory))
                CreateDirectory(directory);
        }

        public IEnumerable<string> GetFiles(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (!DirectoryExists(normalizedPath))
                throw new DirectoryNotFoundException($"Directory not found: {path}");
                
            if (!normalizedPath.EndsWith(Path.DirectorySeparatorChar))
                normalizedPath += Path.DirectorySeparatorChar;
                
            return _files.Keys
                .Where(f => Path.GetDirectoryName(f)?.Equals(normalizedPath.TrimEnd(Path.DirectorySeparatorChar), 
                    StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (!DirectoryExists(normalizedPath))
                throw new DirectoryNotFoundException($"Directory not found: {path}");
                
            if (!normalizedPath.EndsWith(Path.DirectorySeparatorChar))
                normalizedPath += Path.DirectorySeparatorChar;
                
            HashSet<string> result = new();
            
            foreach (var dir in _directories)
            {
                if (dir.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase) && 
                    dir != normalizedPath)
                {
                    string relativePath = dir.Substring(normalizedPath.Length);
                    int nextSeparator = relativePath.IndexOf(Path.DirectorySeparatorChar);
                    if (nextSeparator >= 0)
                    {
                        string directChild = normalizedPath + relativePath.Substring(0, nextSeparator + 1);
                        result.Add(directChild.TrimEnd(Path.DirectorySeparatorChar));
                    }
                }
            }
            
            foreach (var file in _files.Keys)
            {
                if (file.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    string relativePath = file.Substring(normalizedPath.Length);
                    int nextSeparator = relativePath.IndexOf(Path.DirectorySeparatorChar);
                    if (nextSeparator >= 0)
                    {
                        string directChild = normalizedPath + relativePath.Substring(0, nextSeparator + 1);
                        result.Add(directChild.TrimEnd(Path.DirectorySeparatorChar));
                    }
                }
            }
            
            return result.ToList();
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            string normalizedPath = NormalizePath(path);
            if (!DirectoryExists(normalizedPath))
                return;
                
            if (!normalizedPath.EndsWith(Path.DirectorySeparatorChar))
                normalizedPath += Path.DirectorySeparatorChar;
                
            if (!recursive)
            {
                bool hasFiles = _files.Keys.Any(f => f.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase));
                bool hasSubdirs = _directories
                    .Where(d => d != normalizedPath)
                    .Any(d => d.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase));
                    
                if (hasFiles || hasSubdirs)
                    throw new IOException($"Directory not empty: {path}");
            }
            
            _directories.Remove(normalizedPath);
            
            var subdirsToRemove = _directories
                .Where(d => d.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            foreach (var dir in subdirsToRemove)
                _directories.Remove(dir);
                
            var filesToRemove = _files.Keys
                .Where(f => f.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            foreach (var file in filesToRemove)
                _files.Remove(file);
        }

        public void MoveDirectory(string sourcePath, string destPath)
        {
            string normalizedSourcePath = NormalizePath(sourcePath);
            string normalizedDestPath = NormalizePath(destPath);
            
            if (!DirectoryExists(normalizedSourcePath))
                throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
                
            if (DirectoryExists(normalizedDestPath))
                throw new IOException($"Destination directory already exists: {destPath}");
                
            if (!normalizedSourcePath.EndsWith(Path.DirectorySeparatorChar))
                normalizedSourcePath += Path.DirectorySeparatorChar;
                
            if (!normalizedDestPath.EndsWith(Path.DirectorySeparatorChar))
                normalizedDestPath += Path.DirectorySeparatorChar;
                
            CreateDirectory(normalizedDestPath);
            
            var filesToMove = _files.Keys
                .Where(f => f.StartsWith(normalizedSourcePath, StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            foreach (var file in filesToMove)
            {
                string relativePath = file.Substring(normalizedSourcePath.Length);
                string newPath = Path.Combine(normalizedDestPath, relativePath);
                _files[newPath] = _files[file];
                _files.Remove(file);
            }
            
            var dirsToMove = _directories
                .Where(d => d.StartsWith(normalizedSourcePath, StringComparison.OrdinalIgnoreCase) && 
                           d != normalizedSourcePath)
                .ToList();
                
            foreach (var dir in dirsToMove)
            {
                string relativePath = dir.Substring(normalizedSourcePath.Length);
                string newPath = Path.Combine(normalizedDestPath, relativePath);
                _directories.Add(newPath);
                _directories.Remove(dir);
            }
            
            _directories.Remove(normalizedSourcePath);
        }
        
        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
                
            if (!Path.IsPathFullyQualified(path))
            {
                try
                {
                    path = Path.GetFullPath(path);
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }
                
            return path.Replace('/', Path.DirectorySeparatorChar);
        }
        
        public FileInfo GetFileInfo(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (!_files.ContainsKey(normalizedPath))
                throw new FileNotFoundException($"File not found: {path}");
            
            string tempDir = Path.GetTempPath();
            string tempFile = Path.Combine(tempDir, Path.GetFileName(normalizedPath));
            
            try
            {
                File.WriteAllBytes(tempFile, _files[normalizedPath]);
                
                // If we have a stored timestamp for this file, apply it to the temp file
                if (_fileTimestamps.TryGetValue(normalizedPath, out var timestamp))
                {
                    File.SetLastWriteTimeUtc(tempFile, timestamp);
                }
                
                return new FileInfo(tempFile);
            }
            catch
            {
                return new FileInfo(normalizedPath);
            }
        }

        public void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)
        {
            string normalizedPath = NormalizePath(path);
            if (!_files.ContainsKey(normalizedPath))
                throw new FileNotFoundException($"File not found: {path}");
            
            // Store the timestamp in our dictionary
            _fileTimestamps[normalizedPath] = lastWriteTimeUtc;
        }
        
        public DirectoryInfo GetDirectoryInfo(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (!DirectoryExists(normalizedPath))
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            
            // Create a temporary directory to represent this mock directory
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            
            return new DirectoryInfo(tempDir);
        }
        
        public DirectoryInfo[] GetDirectories(DirectoryInfo directory)
        {
            // Convert the DirectoryInfo to a path string and use our existing method
            var dirPaths = GetDirectories(directory.FullName);
            
            // Convert each path to a DirectoryInfo
            return dirPaths.Select(path => new DirectoryInfo(path)).ToArray();
        }
        
        public FileInfo[] GetFiles(DirectoryInfo directory)
        {
            // Convert the DirectoryInfo to a path string and use our existing method
            var filePaths = GetFiles(directory.FullName);
            
            // Convert each path to a FileInfo by using our GetFileInfo method
            return filePaths.Select(path => GetFileInfo(path)).ToArray();
        }
        
        public async Task MoveFolderAsync(string sourcePath, string destPath, bool overwrite = false)
        {
            string normalizedSourcePath = NormalizePath(sourcePath);
            string normalizedDestPath = NormalizePath(destPath);
            
            if (!DirectoryExists(normalizedSourcePath))
                throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
                
            if (DirectoryExists(normalizedDestPath) && !overwrite)
                throw new IOException($"Destination directory already exists: {destPath}");
                
            if (DirectoryExists(normalizedDestPath) && overwrite)
                DeleteDirectory(normalizedDestPath, true);
                
            MoveDirectory(normalizedSourcePath, normalizedDestPath);
            
            await Task.CompletedTask;
        }
        
        public async Task CopyFolderAsync(string sourcePath, string destPath, bool overwrite = false)
        {
            string normalizedSourcePath = NormalizePath(sourcePath);
            string normalizedDestPath = NormalizePath(destPath);
            
            if (!DirectoryExists(normalizedSourcePath))
                throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
                
            if (DirectoryExists(normalizedDestPath) && !overwrite)
                throw new IOException($"Destination directory already exists: {destPath}");
                
            if (DirectoryExists(normalizedDestPath) && overwrite)
                DeleteDirectory(normalizedDestPath, true);
                
            // Create destination directory
            CreateDirectory(normalizedDestPath);
            
            // Copy all files
            foreach (var file in GetFiles(normalizedSourcePath))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(normalizedDestPath, fileName);
                await CopyFileAsync(file, destFile, overwrite);
            }
            
            // Copy all subdirectories
            foreach (var dir in GetDirectories(normalizedSourcePath))
            {
                string dirName = Path.GetFileName(dir);
                string destDir = Path.Combine(normalizedDestPath, dirName);
                await CopyFolderAsync(dir, destDir, overwrite);
            }
            
            // Copy directory metadata (timestamps)
            try
            {
                // In a mock environment, we would need to store and retrieve directory timestamps
                // For simplicity, we'll just simulate this by copying the current time
                // In a real implementation, you would store these in a dictionary similar to _fileTimestamps
                
                // If we had a dictionary for directory timestamps, we would do:
                // _directoryTimestamps[normalizedDestPath] = _directoryTimestamps[normalizedSourcePath];
                
                // For now, we'll just simulate that the metadata was copied
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to copy directory attributes from {sourcePath} to {destPath}: {ex.Message}", ex);
            }
        }
    }
}
