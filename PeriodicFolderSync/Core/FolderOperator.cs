using Microsoft.Extensions.Logging;
using PeriodicFolderSync.Interfaces;

namespace PeriodicFolderSync.Core;

public class FolderOperator(
    ILogger<IFolderOperator> logger,
    IFileSystem fileSystem,
    int retryCount = 3,
    TimeSpan? retryDelay = null) :
    FileSystemOperatorBase(logger, retryCount, retryDelay),
    IFolderOperator
{
    
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

   

    protected override void Validate(string sourcePath, string destPath, string operation)
    {
        ValidatePaths(sourcePath, destPath, operation);
        if (!_fileSystem.DirectoryExists(sourcePath))
            throw new DirectoryNotFoundException($"Source directory not found {sourcePath}");
    }

    public async Task CopyFolderAsync(string sourcePath, string destPath)
    {
        Validate(sourcePath, destPath, nameof(CopyFolderAsync));

       await WithRetryAsync(async () =>
        {
            await _fileSystem.CopyFolderAsync(sourcePath, destPath);
        }, $"Copy folder from {sourcePath} to {destPath}");
    }

    public async Task DeleteFolderAsync(string path, bool recursive = true)
    {
        ValidatePath(path, nameof(DeleteFolderAsync));
        if (!_fileSystem.DirectoryExists(path))
            return;

        await WithRetryAsync(() => Task.Run(() => _fileSystem.DeleteDirectory(path, recursive)),
            $"Delete folder {path}{(recursive ? " recursively" : "")}");
    }

    public async Task MoveFolderAsync(string sourcePath, string destPath)
    {
        Validate(sourcePath, destPath, nameof(MoveFolderAsync));
    
        string? destParent = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destParent))
        {
            try
            {
                if (!_fileSystem.DirectoryExists(destParent))
                    _fileSystem.CreateDirectory(destParent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to create parent directory for {destPath}");
                throw new IOException($"Failed to create parent directory for destination folder: {ex.Message}", ex);
            }
        }
        
    
        await WithRetryAsync(() => Task.Run(() => _fileSystem.MoveDirectory(sourcePath, destPath)),
            $"Move folder from {sourcePath} to {destPath}");
    }

    
}