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
        private string _source = string.Empty;
        private string _destination = string.Empty;

        public async Task SynchronizeFoldersAsync(string source, string destination, SyncStatistics stats, bool useOverwrite)
        {
            _source = source;
            _destination = destination;
            var sourceFolders = GetAllFolders(source);
            var destFolders = GetAllFolders(destination);
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
                        sourceFolder, 
                        destEquivalent, 
                        destFolders, 
                        processedDestFolders, 
                        movedFolders,
                        stats, 
                        useOverwrite);
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

        private async Task HandleMissingDestinationFolder(
            string sourceFolder,
            string destEquivalent,
            HashSet<string> destFolders,
            HashSet<string> processedDestFolders,
            Dictionary<string, string> movedFolders,
            SyncStatistics stats,
            bool useOverwrite)
        {
            var sourceDirInfo = _fileSystem.GetDirectoryInfo(sourceFolder);
            var sourceFileInfos = _fileSystem.GetFiles(sourceDirInfo);
            var sourceSubDirs = _fileSystem.GetDirectories(sourceDirInfo);
            bool hasContent = sourceFileInfos.Length > 0 || sourceSubDirs.Length > 0;
            
            if (hasContent)
            {
                foreach (var destFolder in destFolders.ToList())
                {
                    if (processedDestFolders.Contains(destFolder))
                        continue;
                    
                    try
                    {
                        if (await _matchStrategy.IsFolderMatchAsync(sourceFolder, destFolder, _fileSystem, _source, _destination))
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
                                    
                                await _folderOperator.MoveFolderAsync(destFolder, destEquivalent, useOverwrite);
                                
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
                                stats.FilesInMovedFolders += sourceFileInfos.Length;
                                return;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Failed to move folder: {ex.Message}. Creating new folder instead.");
                                await CopyFolderAndUpdateTracking(sourceFolder, destEquivalent, destFolders, processedDestFolders, stats, sourceFileInfos, useOverwrite);
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
            
            await CopyFolderAndUpdateTracking(sourceFolder, destEquivalent, destFolders, processedDestFolders, stats, sourceFileInfos, useOverwrite);
        }

        private async Task CopyFolderAndUpdateTracking(
            string sourceFolder, 
            string destEquivalent, 
            HashSet<string> destFolders, 
            HashSet<string> processedDestFolders, 
            SyncStatistics stats, 
            FileInfo[] sourceFileInfos, 
            bool useOverwrite)
        {
            _logger.LogInformation($"Copying FOLDER: {sourceFolder} to {destEquivalent}");
            await _folderOperator.CopyFolderAsync(sourceFolder, destEquivalent, useOverwrite);
            
            destFolders.Add(destEquivalent);
            processedDestFolders.Add(destEquivalent);
            
            var newSubfolders = GetAllFolders(destEquivalent);
            foreach (var subfolder in newSubfolders)
            {
                destFolders.Add(subfolder);
                processedDestFolders.Add(subfolder);
            }
            
            stats.ChangedCount += sourceFileInfos.Length;
            stats.FoldersChangedCount++;
        }

        private async Task ProcessExtraDestinationFolders(
            string destination,
            string source,
            HashSet<string> destFolders,
            HashSet<string> processedDestFolders,
            HashSet<string> sourceFolders,
            SyncStatistics stats)
        {
            var orderedDestFolders = destFolders.OrderByDescending(f => f.Length).ToList();
            
            foreach (var destFolder in orderedDestFolders)
            {
                if (processedDestFolders.Contains(destFolder))
                    continue;

                var relativePath = Path.GetRelativePath(destination, destFolder);
                var sourceEquivalent = Path.Combine(source, relativePath);
                
                if (!sourceFolders.Contains(sourceEquivalent))
                {
                    var subDirsCount = destFolders
                        .Count(p => p.StartsWith(destFolder, StringComparison.OrdinalIgnoreCase) && 
                               !p.Equals(destFolder, StringComparison.OrdinalIgnoreCase));
                    
                    _logger.LogInformation($"Deleting extra FOLDER: {destFolder} (not present in source)");
                    await _folderOperator.DeleteFolderAsync(destFolder);
                    
                    stats.DeletedFolders += 1 + subDirsCount;
                }
            }
        }

        private HashSet<string> GetAllFolders(string path)
        {
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!_fileSystem.DirectoryExists(path)) return folders;
            
            var dirInfo = _fileSystem.GetDirectoryInfo(path);
            var subDirs = _fileSystem.GetDirectories(dirInfo);
            
            foreach (var dir in subDirs)
            {
                folders.Add(dir.FullName);
                folders.UnionWith(GetAllFolders(dir.FullName));
            }
            return folders;
        }
    }
}