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

        public MockFileSystem()
        {
            // Add root directories for common drives
            _directories.Add("C:\\");
            _directories.Add("D:\\");
        }

        // File operations
        public bool FileExists(string path) => _files.ContainsKey(NormalizePath(path));

        public Task<byte[]> ReadAllBytesAsync(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (!_files.ContainsKey(normalizedPath))
                throw new FileNotFoundException($"File not found: {path}");

            return Task.FromResult(_files[normalizedPath]);
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
            _files[normalizedDestPath] = _files[normalizedSourcePath].ToArray(); // Create a copy of the byte array
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

        public long GetFileSize(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (!_files.ContainsKey(normalizedPath))
                throw new FileNotFoundException($"File not found: {path}");

            return _files[normalizedPath].Length;
        }

        // Directory operations
        public bool DirectoryExists(string path) 
        {
            string normalizedPath = NormalizePath(path);
            if (string.IsNullOrEmpty(normalizedPath))
                return false;
                
            // Check if this exact directory exists
            if (_directories.Contains(normalizedPath))
                return true;
                
            // Check if any file or directory starts with this path (as a parent directory)
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
                
            // Ensure path ends with directory separator
            if (!normalizedPath.EndsWith(Path.DirectorySeparatorChar))
                normalizedPath += Path.DirectorySeparatorChar;
                
            _directories.Add(normalizedPath);
            
            // Create parent directories
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
                
            // Ensure path ends with directory separator for proper matching
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
                
            // Ensure path ends with directory separator for proper matching
            if (!normalizedPath.EndsWith(Path.DirectorySeparatorChar))
                normalizedPath += Path.DirectorySeparatorChar;
                
            HashSet<string> result = new();
            
            // Find all directories that are direct children of this path
            foreach (var dir in _directories)
            {
                if (dir.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase) && 
                    dir != normalizedPath)
                {
                    // Get the next directory level only
                    string relativePath = dir.Substring(normalizedPath.Length);
                    int nextSeparator = relativePath.IndexOf(Path.DirectorySeparatorChar);
                    if (nextSeparator >= 0)
                    {
                        string directChild = normalizedPath + relativePath.Substring(0, nextSeparator + 1);
                        result.Add(directChild.TrimEnd(Path.DirectorySeparatorChar));
                    }
                }
            }
            
            // Also check files to infer directories
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
                
            // Ensure path ends with directory separator for proper matching
            if (!normalizedPath.EndsWith(Path.DirectorySeparatorChar))
                normalizedPath += Path.DirectorySeparatorChar;
                
            if (!recursive)
            {
                // Check if directory is empty
                bool hasFiles = _files.Keys.Any(f => f.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase));
                bool hasSubdirs = _directories
                    .Where(d => d != normalizedPath)
                    .Any(d => d.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase));
                    
                if (hasFiles || hasSubdirs)
                    throw new IOException($"Directory not empty: {path}");
            }
            
            // Remove this directory
            _directories.Remove(normalizedPath);
            
            // Remove all subdirectories
            var subdirsToRemove = _directories
                .Where(d => d.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            foreach (var dir in subdirsToRemove)
                _directories.Remove(dir);
                
            // Remove all files in this directory and subdirectories
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
                
            // Ensure paths end with directory separator for proper matching
            if (!normalizedSourcePath.EndsWith(Path.DirectorySeparatorChar))
                normalizedSourcePath += Path.DirectorySeparatorChar;
                
            if (!normalizedDestPath.EndsWith(Path.DirectorySeparatorChar))
                normalizedDestPath += Path.DirectorySeparatorChar;
                
            // Create destination directory
            CreateDirectory(normalizedDestPath);
            
            // Move all files
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
            
            // Move all subdirectories
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
            
            // Remove source directory
            _directories.Remove(normalizedSourcePath);
        }
        
        // Helper methods
        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
                
            // Convert to full path if not already
            if (!Path.IsPathFullyQualified(path))
            {
                try
                {
                    path = Path.GetFullPath(path);
                }
                catch (Exception)
                {
                    // If we can't get a full path, just use the original
                    // This can happen with invalid paths in tests
                }
            }
                
            // Normalize directory separators
            return path.Replace('/', Path.DirectorySeparatorChar);
        }
    }
}