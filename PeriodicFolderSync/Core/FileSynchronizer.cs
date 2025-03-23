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
        private readonly IFileOperator _fileOperator = fileOperator ?? throw new ArgumentNullException(nameof(fileOperator));
        private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        private readonly IMatchStrategy _matchStrategy = matchStrategy ?? throw new ArgumentNullException(nameof(matchStrategy));
        private readonly ILogger<IFileSynchronizer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task SynchronizeFilesAsync(string source, string destination, SyncStatistics stats, bool useOverwrite)
        {
            var sourceFiles = GetAllFiles(source);
            var destFiles = GetAllFiles(destination);
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
                    await HandleMissingDestinationFile(sourceFile, destFile, destFilesBySize, processedDestFiles, stats, useOverwrite);
                }
                else
                {
                    processedDestFiles.Add(destFile);
                    await UpdateFileIfModified(sourceFile, destFile, stats);
                }

                processedCount++;
                if (processedCount % 100 == 0 || processedCount == totalFiles)
                {
                    _logger.LogInformation($"Processed {processedCount}/{totalFiles} files ({(int)(processedCount * 100.0 / totalFiles)}%)");
                }
            }

            await ProcessExtraDestinationFiles(destFiles, processedDestFiles, stats);
        }

        private void EnsureDestinationDirectoryExists(string filePath)
        {
            string? destDir = Path.GetDirectoryName(filePath);
            if (destDir != null && !_fileSystem.DirectoryExists(destDir))
            {
                _logger.LogInformation($"Creating directory: {destDir}");
                _fileSystem.CreateDirectory(destDir);
            }
        }

        private async Task HandleMissingDestinationFile(
            string sourceFile,
            string destFile,
            Dictionary<long, List<string>> destFilesBySize,
            HashSet<string> processedDestFiles,
            SyncStatistics stats,
            bool useOverwrite)
        {
            var sourceInfo = _fileSystem.GetFileInfo(sourceFile);
            if (destFilesBySize.TryGetValue(sourceInfo.Length, out var sizeMatches))
            {
                foreach (var potentialMatch in sizeMatches.ToList())
                {
                    if (processedDestFiles.Contains(potentialMatch))
                        continue;

                    if (await _matchStrategy.IsFileMatchAsync(sourceFile, potentialMatch, _fileSystem))
                    {
                        _logger.LogInformation($"Moving/renaming FILE: {potentialMatch} to {destFile}");
                        await _fileOperator.MoveFileAsync(potentialMatch, destFile, useOverwrite);
                        processedDestFiles.Add(potentialMatch);
                        sizeMatches.Remove(potentialMatch);
                        stats.FilesMoved++;
                        return;
                    }
                }
            }
            _logger.LogInformation($"Copying new file: {sourceFile} to {destFile}");
            await _fileOperator.CopyFileAsync(sourceFile, destFile, useOverwrite);
            stats.ChangedCount++;
        }

        private async Task UpdateFileIfModified(string sourceFile, string destFile, SyncStatistics stats)
        {
            var sourceInfo = _fileSystem.GetFileInfo(sourceFile);
            var destInfo = _fileSystem.GetFileInfo(destFile);

            if (sourceInfo.Length != destInfo.Length || sourceInfo.LastWriteTimeUtc != destInfo.LastWriteTimeUtc)
            {
                _logger.LogInformation($"Updating modified file: {destFile}");
                await _fileOperator.CopyFileAsync(sourceFile, destFile, true);
                stats.ChangedCount++;
            }
        }

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

        private HashSet<string> GetAllFiles(string path)
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!_fileSystem.DirectoryExists(path)) return files;
            files.UnionWith(_fileSystem.GetFiles(path));
            foreach (var dir in _fileSystem.GetDirectories(path))
                files.UnionWith(GetAllFiles(dir));
            return files;
        }

        private Dictionary<long, List<string>> GetDestinationFilesBySize(HashSet<string> destFiles)
        {
            return destFiles
                .GroupBy(f => _fileSystem.GetFileInfo(f).Length)
                .Where(g => g.Key > 0)
                .ToDictionary(g => g.Key, g => g.ToList());
        }
    }
}