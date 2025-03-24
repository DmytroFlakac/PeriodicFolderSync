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
    protected override void Validate(string sourcePath, string destPath, string operation, bool overwrite = false)
    {
        ValidatePaths(sourcePath, destPath, operation);
        if (!fileSystem.DirectoryExists(sourcePath))
            throw new DirectoryNotFoundException($"Source directory not found {sourcePath}");
    }

    public async Task CopyFolderAsync(string sourcePath, string destPath, bool overwrite = false)
    {
        Validate(sourcePath, destPath, nameof(CopyFolderAsync), overwrite);

        await fileSystem.CopyFolderAsync(sourcePath, destPath, overwrite);
    }

    public async Task DeleteFolderAsync(string path, bool recursive = true)
    {
        ValidatePath(path, nameof(DeleteFolderAsync));
        if (!fileSystem.DirectoryExists(path))
            return;

        await WithRetryAsync(() => Task.Run(() => fileSystem.DeleteDirectory(path, recursive)),
            $"Delete folder {path}{(recursive ? " recursively" : "")}");
    }

    public async Task MoveFolderAsync(string sourcePath, string destPath, bool overwrite = false)
    {
        Validate(sourcePath, destPath, nameof(MoveFolderAsync), overwrite);

        string? destParent = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destParent) && !fileSystem.DirectoryExists(destParent))
            fileSystem.CreateDirectory(destParent);

        if (fileSystem.DirectoryExists(destPath) && overwrite)
            await DeleteFolderAsync(destPath, recursive: true);

        await WithRetryAsync(() => Task.Run(() => fileSystem.MoveDirectory(sourcePath, destPath)),
            $"Move folder from {sourcePath} to {destPath}");
    }

    
}