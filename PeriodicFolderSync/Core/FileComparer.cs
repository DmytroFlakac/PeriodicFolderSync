using Microsoft.Extensions.Logging;
using PeriodicFolderSync.Interfaces;
using System.Security.Cryptography;

namespace PeriodicFolderSync.Core
{
    public class FileComparer(ILogger<IFileComparer> logger, IFileSystem? fileSystem = null) : IFileComparer
    {
        private readonly IFileSystem _fileSystem = fileSystem ?? new FileSystem();
        private const int BufferSize = 8192;

        public async Task<bool> AreFilesIdenticalAsync(string file1Path, string file2Path)
        {
            try
            {
                if (string.Equals(file1Path, file2Path, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!_fileSystem.FileExists(file1Path) || !_fileSystem.FileExists(file2Path))
                    return false;

                var file1Info = _fileSystem.GetFileInfo(file1Path);
                var file2Info = _fileSystem.GetFileInfo(file2Path);
                
                if (file1Info.Length != file2Info.Length)
                    return false;
                
                
                if (file1Info.LastWriteTimeUtc == file2Info.LastWriteTimeUtc)
                    return true;
                
                if (file1Info.Length < 1024 * 1024)
                {
                    string hash1 = await CalculateFileHashAsync(file1Path);
                    string hash2 = await CalculateFileHashAsync(file2Path);
                    return string.Equals(hash1, hash2, StringComparison.OrdinalIgnoreCase);
                }
                
                return await CompareFileContentsAsync(file1Path, file2Path);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error comparing files {file1Path} and {file2Path}");
                return false;
            }
        }

        public async Task<string> CalculateFileHashAsync(string filePath)
        {
            try
            {
                using var md5 = MD5.Create();
                // Use ReadAllBytesAsync from IFileSystem instead of File.OpenRead
                byte[] fileBytes = await _fileSystem.ReadAllBytesAsync(filePath);
                byte[] hash = md5.ComputeHash(fileBytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error calculating hash for file {filePath}");
                throw;
            }
        }

        private async Task<bool> CompareFileContentsAsync(string file1Path, string file2Path)
        {
            try
            {
                byte[] file1Bytes = await _fileSystem.ReadAllBytesAsync(file1Path);
                byte[] file2Bytes = await _fileSystem.ReadAllBytesAsync(file2Path);
                
                if (file1Bytes.Length != file2Bytes.Length)
                    return false;
                
                for (int i = 0; i < file1Bytes.Length; i += BufferSize)
                {
                    int chunkSize = Math.Min(BufferSize, file1Bytes.Length - i);
                    
                    for (int j = 0; j < chunkSize; j++)
                    {
                        if (file1Bytes[i + j] != file2Bytes[i + j])
                            return false;
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error comparing file contents between {file1Path} and {file2Path}");
                return false;
            }
        }
    }

    
}