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
    
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly IFileComparer _fileComparer = fileComparer ?? throw new ArgumentNullException(nameof(fileComparer));
    
    
    protected override void Validate(string sourcePath, string destPath, string operation)
    {
        ValidatePaths(sourcePath, destPath, operation);
        if(!_fileSystem.FileExists(sourcePath))
            throw new FileNotFoundException($"Source file not found {sourcePath}");
    }

    public async Task CopyFileAsync(string sourcePath, string destPath)
    {
        Validate(sourcePath, destPath, nameof(CopyFileAsync));
        
        await WithRetryAsync(async () =>
        {
            await _fileSystem.CopyFileAsync(sourcePath, destPath);
        }, $"Copy file from {sourcePath} to {destPath}");
    }

    public async Task DeleteFileAsync(string path)
    {
        ValidatePath(path, nameof(DeleteFileAsync));
        
        if (!_fileSystem.FileExists(path))
            return;

        await WithRetryAsync(async () => 
        {
            await _fileSystem.DeleteFileAsync(path);
        }, $"Delete file {path}");
    }

    public async Task MoveFileAsync(string sourcePath, string destPath)
    {
        Validate(sourcePath, destPath, nameof(MoveFileAsync));
        
        await WithRetryAsync(async () => 
        {
            await _fileSystem.MoveFileAsync(sourcePath, destPath);
        }, $"Move file from {sourcePath} to {destPath}");
    }
    
}