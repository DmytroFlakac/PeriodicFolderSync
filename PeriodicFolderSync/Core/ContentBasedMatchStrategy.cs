using PeriodicFolderSync.Interfaces;
using Microsoft.Extensions.Logging;

namespace PeriodicFolderSync.Core
{
    public class ContentBasedMatchStrategy(ILogger<IMatchStrategy> logger) : IMatchStrategy
    {
        private readonly ILogger<IMatchStrategy> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public Task<bool> IsFolderMatchAsync(string sourceFolder, string destFolder, IFileSystem fileSystem, string source, string destination)
        {
            try
            {
                //check if the source and destination folders exist
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

        public Task<bool> IsFileMatchAsync(string sourceFile, string destFile, IFileSystem fileSystem)
        {
            try
            {
                var sourceInfo = fileSystem.GetFileInfo(sourceFile);
                var destInfo = fileSystem.GetFileInfo(destFile);
                
                // Check size first
                if (sourceInfo.Length != destInfo.Length)
                    return Task.FromResult(false);
                    
                // Check timestamps (exact match as in OldSynchronizer)
                bool timestampsMatch = sourceInfo.LastWriteTimeUtc == destInfo.LastWriteTimeUtc;
                
                return Task.FromResult(timestampsMatch);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error during file matching: {ex.Message}");
                return Task.FromResult(false);
            }
        }
    }
}