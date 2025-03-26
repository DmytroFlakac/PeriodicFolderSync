using PeriodicFolderSync.Interfaces;
using PeriodicFolderSync.Models;
using Microsoft.Extensions.Logging;

namespace PeriodicFolderSync.Core
{
    public class FileSynchronizer(
        IFileOperator fileOperator,
        IFileSystem fileSystem,
        IMatchStrategy matchStrategy,
        ILogger<IFileSynchronizer> logger
    )
        : IFileSynchronizer
    {
        private readonly IFileOperator _fileOperator =
            fileOperator ?? throw new ArgumentNullException(nameof(fileOperator));

        private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

        private readonly IMatchStrategy _matchStrategy =
            matchStrategy ?? throw new ArgumentNullException(nameof(matchStrategy));

        private readonly ILogger<IFileSynchronizer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task SynchronizeFilesAsync(string source, string destination, SyncStatistics stats)
        {
            var sourceFiles = _fileSystem.GetAllFiles(source);
            var destFiles = _fileSystem.GetAllFiles(destination);
            var destFilesBySize = GetDestinationFilesBySize(destFiles);
            var processedDestFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int processedCount = 0;
            int totalFiles = sourceFiles.Count;

            foreach (var sourceFile in sourceFiles)
            {
                var relativePath = Path.GetRelativePath(source, sourceFile);
                var destFile = Path.Combine(destination, relativePath);

                EnsureDestinationDirectoryExists(destFile);

                if (!_fileSystem.FileExists(destFile))
                {
                    await HandleMissingDestinationFile(sourceFile, destFile, destFilesBySize, processedDestFiles, stats,
                        source, destination);
                }
                else
                {
                    processedDestFiles.Add(destFile);
                    await UpdateFileIfModified(sourceFile, destFile, stats);
                }

                processedCount++;
                if (processedCount % 100 == 0 || processedCount == totalFiles)
                {
                    _logger.LogInformation(
                        $"Processed {processedCount}/{totalFiles} files ({(int)(processedCount * 100.0 / totalFiles)}%)");
                }
            }

            await ProcessExtraDestinationFiles(destFiles, processedDestFiles, stats);
        }

        /// <summary>
        /// Ensures that the directory for the specified file path exists, creating it if necessary.
        /// </summary>
        /// <param name="filePath">The file path whose directory should exist.</param>
        private void EnsureDestinationDirectoryExists(string filePath)
        {
            string? destDir = Path.GetDirectoryName(filePath);
            if (destDir != null && !_fileSystem.DirectoryExists(destDir))
            {
                _logger.LogInformation($"Creating directory: {destDir}");
                _fileSystem.CreateDirectory(destDir);
            }
        }

        /// <summary>
        /// Handles the case when a destination file is missing by either finding a matching file to move or copying the source file.
        /// </summary>
        /// <param name="sourceFile">The source file path.</param>
        /// <param name="destFile">The destination file path.</param>
        /// <param name="destFilesBySize">Dictionary of destination files grouped by size.</param>
        /// <param name="processedDestFiles">Set of destination files that have already been processed.</param>
        /// <param name="stats">Statistics object to track synchronization metrics.</param>
        /// <param name="source">The source directory path.</param>
        /// <param name="destination">The destination directory path.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task HandleMissingDestinationFile(
            string sourceFile,
            string destFile,
            Dictionary<long, List<string>> destFilesBySize,
            HashSet<string> processedDestFiles,
            SyncStatistics stats,
            string source,
            string destination)
        {
            try
            {
                var sourceInfo = _fileSystem.GetFileInfo(sourceFile);
                if (destFilesBySize.TryGetValue(sourceInfo.Length, out var sizeMatches))
                {
                    foreach (var potentialMatch in sizeMatches.ToList())
                    {
                        if (processedDestFiles.Contains(potentialMatch))
                            continue;

                        try
                        {
                            if (await _matchStrategy.IsFileMatchAsync(sourceFile, potentialMatch, _fileSystem, source,
                                    destination))
                            {
                                _logger.LogInformation($"Moving/renaming FILE: {potentialMatch} to {destFile}");
                                try
                                {
                                    await _fileOperator.MoveFileAsync(potentialMatch, destFile);
                                    processedDestFiles.Add(potentialMatch);
                                    sizeMatches.Remove(potentialMatch);
                                    stats.FilesMoved++;
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex,
                                        $"Failed to move file from {potentialMatch} to {destFile}. Falling back to copy operation.");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                $"Error comparing files {sourceFile} and {potentialMatch}. Skipping this potential match.");
                        }
                    }
                }

                _logger.LogInformation($"Copying new file: {sourceFile} to {destFile}");
                await _fileOperator.CopyFileAsync(sourceFile, destFile);
                stats.ChangedCount++;
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, $"Source file not found: {sourceFile}");
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, $"Access denied when handling file: {sourceFile} to {destFile}");
                throw;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, $"IO error when handling file: {sourceFile} to {destFile}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error when handling file: {sourceFile} to {destFile}");
                throw;
            }
        }

        /// <summary>
        /// Updates a destination file if it differs from the source file.
        /// </summary>
        /// <param name="sourceFile">The source file path.</param>
        /// <param name="destFile">The destination file path.</param>
        /// <param name="stats">Statistics object to track synchronization metrics.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task UpdateFileIfModified(string sourceFile, string destFile, SyncStatistics stats)
        {
            try
            {
                if (_fileSystem.GetFileInfo(sourceFile).Length != _fileSystem.GetFileInfo(destFile).Length ||
                    _fileSystem.GetFileInfo(sourceFile).LastWriteTimeUtc !=
                    _fileSystem.GetFileInfo(destFile).LastWriteTimeUtc)
                {
                    _logger.LogInformation($"Updating modified file: {destFile}");
                    await _fileOperator.CopyFileAsync(sourceFile, destFile);
                    stats.ChangedCount++;
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Failed to update file: {destFile}");
            }
        }

    /// <summary>
        /// Processes files in the destination that don't exist in the source, deleting them if necessary.
        /// </summary>
        /// <param name="destFiles">Set of all destination files.</param>
        /// <param name="processedDestFiles">Set of destination files that have already been processed.</param>
        /// <param name="stats">Statistics object to track synchronization metrics.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ProcessExtraDestinationFiles(
            HashSet<string> destFiles,
            HashSet<string> processedDestFiles,
            SyncStatistics stats)
        {
            foreach (var destFile in destFiles)
            {
                if (processedDestFiles.Contains(destFile))
                    continue;

                _logger.LogInformation($"Deleting extra FILE: {destFile}");
                await _fileOperator.DeleteFileAsync(destFile);
                stats.DeletedFiles++;
            }
        }
        
        /// <summary>
        /// Groups destination files by their size for efficient matching.
        /// </summary>
        /// <param name="destFiles">Set of all destination files.</param>
        /// <returns>A dictionary mapping file sizes to lists of files with that size.</returns>
        private Dictionary<long, List<string>> GetDestinationFilesBySize(HashSet<string> destFiles)
        {
            return destFiles
                .GroupBy(f => _fileSystem.GetFileInfo(f).Length)
                .Where(g => g.Key > 0)
                .ToDictionary(g => g.Key, g => g.ToList());
        }
    }
}