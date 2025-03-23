using Microsoft.Extensions.Logging;
using PeriodicFolderSync.Interfaces;

namespace PeriodicFolderSync.Core;

public class FileOperator(
    ILogger<IFileOperator> logger, 
    IFileSystem fileSystem,
    IFileComparer fileComparer,
    int retryCount = 3, 
    TimeSpan? retryDelay = null) :
    FileSystemOperatorBase(logger, retryCount, retryDelay), 
    IFileOperator
{
    protected override void Validate(string sourcePath, string destPath, string operation, bool overwrite = false)
    {
        ValidatePaths(sourcePath, destPath, operation);
        if(!fileSystem.FileExists(sourcePath))
            throw new FileNotFoundException($"Source file not found {sourcePath}");
    }

    public async Task CopyFileAsync(string sourcePath, string destPath, bool overwrite = false)
    {
        Validate(sourcePath, destPath, nameof(CopyFileAsync), overwrite);
        
        fileSystem.CreateDirectoryIfNotExist(destPath);

        if (fileSystem.FileExists(destPath) && overwrite)
        {
            if (await fileComparer.AreFilesIdenticalAsync(sourcePath, destPath))
            {
                logger.LogInformation($"Skipping unchanged file: {destPath}");
                return;
            }
        }

        await WithRetryAsync(async () =>
        {
            await fileSystem.CopyFileAsync(sourcePath, destPath, overwrite);
        }, $"Copy file from {sourcePath} to {destPath}");
    }

    public async Task DeleteFileAsync(string path)
    {
        ValidatePath(path, nameof(DeleteFileAsync));
        
        if (!fileSystem.FileExists(path))
            return;

        await WithRetryAsync(async () => 
        {
            await fileSystem.DeleteFileAsync(path);
        }, $"Delete file {path}");
    }

    public async Task MoveFileAsync(string sourcePath, string destPath, bool overwrite = false)
    {
        Validate(sourcePath, destPath, nameof(MoveFileAsync), overwrite);

        fileSystem.CreateDirectoryIfNotExist(destPath);
        
        if (fileSystem.FileExists(destPath) && overwrite)
            await DeleteFileAsync(destPath);

        await WithRetryAsync(async () => 
        {
            await fileSystem.MoveFileAsync(sourcePath, destPath);
        }, $"Move file from {sourcePath} to {destPath}");
    }
    
}