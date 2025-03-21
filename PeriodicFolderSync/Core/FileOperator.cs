using Microsoft.Extensions.Logging;
using PeriodicFolderSync.Interfaces;

namespace PeriodicFolderSync.Core;

public class FileOperator(
    ILogger logger, 
    IFileSystem? fileSystem = null,
    int retryCount = 3, 
    TimeSpan? retryDelay = null) :
    FileSystemOperatorBase(logger, retryCount, retryDelay), 
    IFileOperator
{
    private readonly IFileSystem _fileSystem = fileSystem ?? new FileSystem();
    
    protected override void Validate(string sourcePath, string destPath, string operation, bool overwrite = false)
    {
        ValidatePaths(sourcePath, destPath, operation);
        if(!_fileSystem.FileExists(sourcePath))
            throw new FileNotFoundException($"Source file not found {sourcePath}");
        if (_fileSystem.FileExists(destPath) && !overwrite)
            throw new IOException($"Destination file already exists: {destPath}");
    }

    public async Task CopyFileAsync(string sourcePath, string destPath, bool overwrite = false)
    {
        Validate(sourcePath, destPath, nameof(CopyFileAsync), overwrite);
        
        _fileSystem.CreateDirectoryIfNotExist(destPath);

        await WithRetryAsync(async () =>
        {
            await _fileSystem.CopyFileAsync(sourcePath, destPath, overwrite);
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

    public async Task MoveFileAsync(string sourcePath, string destPath, bool overwrite = false)
    {
        Validate(sourcePath, destPath, nameof(MoveFileAsync), overwrite);

        _fileSystem.CreateDirectoryIfNotExist(destPath);
        
        if (_fileSystem.FileExists(destPath) && overwrite)
            await DeleteFileAsync(destPath);

        await WithRetryAsync(async () => 
        {
            await _fileSystem.MoveFileAsync(sourcePath, destPath);
        }, $"Move file from {sourcePath} to {destPath}");
    }

    public async Task RenameFileAsync(string path, string newName, bool overwrite = false)
    {
        ValidatePath(path, nameof(RenameFileAsync));
    
        if (string.IsNullOrEmpty(newName))
            throw new ArgumentException("New name cannot be null or empty.", nameof(newName));
        if (!_fileSystem.FileExists(path))
            throw new FileNotFoundException($"File not found: {path}");

        string newPath = Path.IsPathFullyQualified(newName) 
            ? newName 
            : Path.Combine(Path.GetDirectoryName(path) ?? "", newName);

        _fileSystem.CreateDirectoryIfNotExist(newPath);

        if (_fileSystem.FileExists(newPath) && !overwrite)
            throw new IOException($"File already exists at {newPath}");
        if (_fileSystem.FileExists(newPath) && overwrite)
            await DeleteFileAsync(newPath);

        await WithRetryAsync(async () => 
        {
            await _fileSystem.MoveFileAsync(path, newPath);
        }, $"Rename file from {path} to {newPath}");
    }
}