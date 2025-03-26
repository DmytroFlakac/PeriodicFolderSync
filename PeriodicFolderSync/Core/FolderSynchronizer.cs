using PeriodicFolderSync.Interfaces;
using PeriodicFolderSync.Models;
using Microsoft.Extensions.Logging;

namespace PeriodicFolderSync.Core
{
    public class FolderSynchronizer(
        IFolderOperator folderOperator,
        IFileSystem fileSystem,
        IMatchStrategy matchStrategy,
        ILogger<IFolderSynchronizer> logger
        )
        : IFolderSynchronizer
    {
        private readonly IFolderOperator _folderOperator = folderOperator ?? throw new ArgumentNullException(nameof(folderOperator));
        private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        private readonly IMatchStrategy _matchStrategy = matchStrategy ?? throw new ArgumentNullException(nameof(matchStrategy));
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
       
        public async Task SynchronizeFoldersAsync(string source, string destination, SyncStatistics stats){
            _fileSystem.CreateDirectoryIfNotExist(source);
            var sourceFolders = _fileSystem.GetAllFolders(source);
            var destFolders = _fileSystem.GetAllFolders(destination);
            var processedDestFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var movedFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sourceFolder in sourceFolders)
            {
                var relativePath = Path.GetRelativePath(source, sourceFolder);
                var destEquivalent = Path.Combine(destination, relativePath);
                bool destExists = destFolders.Contains(destEquivalent);
                
                if (!destExists)
                {
                    await HandleMissingDestinationFolder(
                        source,
                        destination,
                        sourceFolder, 
                        destEquivalent, 
                        destFolders, 
                        processedDestFolders, 
                        movedFolders,
                        stats);
                }
                else
                {
                    processedDestFolders.Add(destEquivalent);
                }
            }

            await ProcessExtraDestinationFolders(
                destination, 
                source, 
                destFolders, 
                processedDestFolders, 
                sourceFolders,
                stats);
        }

        /// <summary>
        /// Handles the case when a destination folder is missing by either finding a matching folder to move or creating a new one.
        /// </summary>
        /// <param name="source">The source directory path.</param>
        /// <param name="destination">The destination directory path.</param>
        /// <param name="sourceFolder">The source folder path.</param>
        /// <param name="destEquivalent">The destination folder path.</param>
        /// <param name="destFolders">Set of all destination folders.</param>
        /// <param name="processedDestFolders">Set of destination folders that have already been processed.</param>
        /// <param name="movedFolders">Dictionary tracking folders that have been moved.</param>
        /// <param name="stats">Statistics object to track synchronization metrics.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task HandleMissingDestinationFolder(
            string source,
            string destination,
            string sourceFolder,
            string destEquivalent,
            HashSet<string> destFolders,
            HashSet<string> processedDestFolders,
            Dictionary<string, string> movedFolders,
            SyncStatistics stats
   )
        {
            var sourceFiles = _fileSystem.GetAllFiles(sourceFolder);
            var sourceSubDirs = _fileSystem.GetDirectories(sourceFolder);
            bool hasContent = sourceFiles.Any() || sourceSubDirs.Any();
            
            if (hasContent)
            {
                foreach (var destFolder in destFolders.ToList())
                {
                    if (processedDestFolders.Contains(destFolder))
                        continue;
                    
                    try
                    {
                        if (await _matchStrategy.IsFolderMatchAsync(sourceFolder, destFolder, _fileSystem, source, destination))
                        {
                            _logger.LogInformation($"Moving/renaming FOLDER: {destFolder} to {destEquivalent}");
                            
                            string? parentDir = Path.GetDirectoryName(destEquivalent);
                            if (!string.IsNullOrEmpty(parentDir))
                            {
                                _fileSystem.CreateDirectoryIfNotExist(parentDir);
                            }
                            
                            try
                            {
                                var subfolderPaths = destFolders
                                    .Where(p => p.StartsWith(destFolder, StringComparison.OrdinalIgnoreCase) && 
                                           !p.Equals(destFolder, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                                    
                                try
                                {
                                    if (await _matchStrategy.IsFolderMatchAsync(sourceFolder, destFolder, _fileSystem, source, destination))
                                    {
                                        try
                                        {
                                            await _folderOperator.MoveFolderAsync(destFolder, destEquivalent);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning($"Failed to move folder: {ex.Message}. Creating new folder instead.");
                                            await CopyFolderAndUpdateTracking(sourceFolder, destEquivalent, destFolders, processedDestFolders, stats, sourceFiles);
                                            return;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"Error checking folder match: {ex.Message}");
                                }
                                
                                destFolders.Remove(destFolder);
                                destFolders.Add(destEquivalent);
                                movedFolders[destFolder] = destEquivalent;
                                
                                foreach (var subfolder in subfolderPaths)
                                {
                                    string relativePath = Path.GetRelativePath(destFolder, subfolder);
                                    string newPath = Path.Combine(destEquivalent, relativePath);
                                    
                                    destFolders.Remove(subfolder);
                                    destFolders.Add(newPath);
                                    
                                    if (processedDestFolders.Contains(subfolder))
                                    {
                                        processedDestFolders.Remove(subfolder);
                                        processedDestFolders.Add(newPath);
                                    }
                                    
                                    movedFolders[subfolder] = newPath;
                                }
                                
                                processedDestFolders.Add(destEquivalent);
                                stats.FoldersMovedCount++;
                                stats.FilesInMovedFolders += sourceFiles.Count();
                                return;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Failed to move folder: {ex.Message}. Creating new folder instead.");
                                await CopyFolderAndUpdateTracking(sourceFolder, destEquivalent, destFolders, processedDestFolders, stats, sourceFiles);
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error checking folder match: {ex.Message}");
                    }
                }
            }

            await CopyFolderAndUpdateTracking(sourceFolder, destEquivalent, destFolders, processedDestFolders, stats, sourceFiles);
        }

        /// <summary>
        /// Copies a folder from source to destination and updates tracking collections.
        /// </summary>
        /// <param name="sourceFolder">The source folder path.</param>
        /// <param name="destEquivalent">The destination folder path.</param>
        /// <param name="destFolders">Set of all destination folders.</param>
        /// <param name="processedDestFolders">Set of destination folders that have already been processed.</param>
        /// <param name="stats">Statistics object to track synchronization metrics.</param>
        /// <param name="sourceFiles">The list of files in the source folder.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task CopyFolderAndUpdateTracking(
            string sourceFolder, 
            string destEquivalent, 
            HashSet<string> destFolders, 
            HashSet<string> processedDestFolders, 
            SyncStatistics stats, 
            IEnumerable<string> sourceFiles)
        {
            try
            {
                _logger.LogInformation($"Copying FOLDER: {sourceFolder} to {destEquivalent}");
                await _folderOperator.CopyFolderAsync(sourceFolder, destEquivalent);
                
                destFolders.Add(destEquivalent);
                processedDestFolders.Add(destEquivalent);
                
                var newSubfolders = _fileSystem.GetAllFolders(destEquivalent);
                
                foreach (var subfolder in newSubfolders)
                {
                    destFolders.Add(subfolder);
                    processedDestFolders.Add(subfolder);
                }
                
                stats.ChangedCount += sourceFiles.Count();
                stats.FoldersChangedCount++;
                stats.FoldersChangedCount += newSubfolders.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error copying folder: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes folders in the destination that don't exist in the source, deleting them if necessary.
        /// </summary>
        /// <param name="destination">The destination directory path.</param>
        /// <param name="source">The source directory path.</param>
        /// <param name="destFolders">Set of all destination folders.</param>
        /// <param name="processedDestFolders">Set of destination folders that have already been processed.</param>
        /// <param name="sourceFolders">Set of all source folders.</param>
        /// <param name="stats">Statistics object to track synchronization metrics.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ProcessExtraDestinationFolders(
            string destination,
            string source,
            HashSet<string> destFolders,
            HashSet<string> processedDestFolders,
            HashSet<string> sourceFolders,
            SyncStatistics stats)
        {
            var orderedDestFolders = destFolders.OrderByDescending(f => f.Length).ToList();
    
            var deletedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    
            foreach (var destFolder in orderedDestFolders)
            {
                if (processedDestFolders.Contains(destFolder) || deletedFolders.Contains(destFolder))
                    continue;

                var relativePath = Path.GetRelativePath(destination, destFolder);
                var sourceEquivalent = Path.Combine(source, relativePath);
        
                if (!sourceFolders.Contains(sourceEquivalent))
                {
                    var subDirs = _fileSystem.GetAllFolders(destFolder);
            
                    _logger.LogInformation($"Deleting extra FOLDER: {destFolder} (not present in source)");
                    int filesCount = _fileSystem.GetAllFiles(destFolder).Count;
                    await _folderOperator.DeleteFolderAsync(destFolder);
            
                    foreach (var subDir in subDirs)
                    {
                        deletedFolders.Add(subDir);
                    }
            
                    stats.DeletedFolders++;
                    stats.DeletedFiles += filesCount;
                }
            }
        }
    }
}