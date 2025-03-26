using PeriodicFolderSync.Interfaces;
using Microsoft.Extensions.Logging;

namespace PeriodicFolderSync.Core
{
    public class ContentBasedMatchStrategy(ILogger<IMatchStrategy> logger, IFileComparer fileComparer) : IMatchStrategy
    {
        private readonly ILogger<IMatchStrategy> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IFileComparer _fileComparer = fileComparer ?? throw new ArgumentNullException(nameof(fileComparer));

        public Task<bool> IsFolderMatchAsync(string sourceFolder, string destFolder, IFileSystem fileSystem, string source, string destination)
        {
            try
            {
                if (!fileSystem.DirectoryExists(sourceFolder) || !fileSystem.DirectoryExists(destFolder))
                {
                    return Task.FromResult(false);
                }
                
                string destRelativePath = Path.GetRelativePath(destination, destFolder);
                string expectedSourcePath = Path.Combine(source, destRelativePath);
                
                if (fileSystem.DirectoryExists(expectedSourcePath) && 
                    !string.Equals(expectedSourcePath, sourceFolder, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(false);
                
                

                var sourceDirInfo = fileSystem.GetDirectoryInfo(sourceFolder);
                var destDirInfo = fileSystem.GetDirectoryInfo(destFolder);
                
                TimeSpan creationTimeDifference = sourceDirInfo.CreationTimeUtc - destDirInfo.CreationTimeUtc;
                bool creationTimeMatch = Math.Abs(creationTimeDifference.TotalMinutes) < 5; 
                
                var sourceFolderFiles = fileSystem.GetFiles(sourceFolder).ToList();
                var destFolderFiles = fileSystem.GetFiles(destFolder).ToList();
                
                int sourceFolderFilesCount = sourceFolderFiles.Count;
                int destFolderFilesCount = destFolderFiles.Count;
                
                if (sourceFolderFilesCount != destFolderFilesCount)
                {
                    if (creationTimeMatch && Math.Abs(sourceFolderFilesCount - destFolderFilesCount) <= 2)
                    {
                        _logger.LogDebug($"Folders potentially matched by creation time despite file count difference: {sourceFolder} and {destFolder}");
                        return Task.FromResult(true);
                    }
                    return Task.FromResult(false);
                }
                
                if (sourceFolderFilesCount == 0)
                {
                    return Task.FromResult(creationTimeMatch);
                }
                
                var sourceSizes = sourceFolderFiles
                    .Select(f => fileSystem.GetFileInfo(f).Length)
                    .OrderBy(s => s)
                    .ToList();
                    
                var destSizes = destFolderFiles
                    .Select(f => fileSystem.GetFileInfo(f).Length)
                    .OrderBy(s => s)
                    .ToList();
                
                int matchCount = sourceSizes.Intersect(destSizes).Count();
                
               
                bool sizeMatch = matchCount >= Math.Max(1, sourceFolderFilesCount / 2);
                bool isMatch = sizeMatch || (creationTimeMatch && matchCount > 0);
                
                if (isMatch)
                {
                    if (creationTimeMatch)
                    {
                        _logger.LogDebug($"Folders matched: {sourceFolder} and {destFolder} (creation time match and {matchCount}/{sourceFolderFilesCount} files by size)");
                    }
                    else
                    {
                        _logger.LogDebug($"Folders matched: {sourceFolder} and {destFolder} (matched {matchCount}/{sourceFolderFilesCount} files by size)");
                    }
                }
                
                return Task.FromResult(isMatch);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during folder matching: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public async Task<bool> IsFileMatchAsync(string sourceFile, string destFile, IFileSystem fileSystem, string source, string destination)
        {
            
            if (!fileSystem.FileExists(sourceFile) || !fileSystem.FileExists(destFile))
                return false;
            
            string destRelativePath = Path.GetRelativePath(destination, destFile);
            string expectedSourcePath = Path.Combine(source, destRelativePath);
            if (fileSystem.FileExists(expectedSourcePath) &&
                !string.Equals(expectedSourcePath, sourceFile, StringComparison.OrdinalIgnoreCase))
                return false;
            
            try
            {
                return await _fileComparer.AreFilesIdenticalAsync(sourceFile, destFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error during file matching: {ex.Message}");
                return false;
            }
        }
    }
}