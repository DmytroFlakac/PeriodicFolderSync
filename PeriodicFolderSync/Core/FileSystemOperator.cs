using Microsoft.Extensions.Logging;
using PeriodicFolderSync.Interfaces;

namespace PeriodicFolderSync.Core;

public class FileSystemOperator(
    ILogger<FileSystemOperator> logger,
    IFileSystem fileSystem,
    IFileComparer fileComparer,
    int retryCount = 3,
    TimeSpan? retryDelay = null)
    : IFileOperator, IFolderOperator
{
    private readonly IFileOperator _fileOperator = new FileOperator(logger, fileSystem, fileComparer, retryCount, retryDelay);
    private readonly IFolderOperator _folderOperator = new FolderOperator(logger, fileSystem, retryCount, retryDelay);

    public Task CopyFileAsync(string sourcePath, string destPath) =>
        _fileOperator.CopyFileAsync(sourcePath, destPath);

    public Task DeleteFileAsync(string path) =>
        _fileOperator.DeleteFileAsync(path);


    public Task MoveFileAsync(string sourcePath, string destPath) =>
        _fileOperator.MoveFileAsync(sourcePath, destPath);
    
    public Task CopyFolderAsync(string sourcePath, string destPath) =>
        _folderOperator.CopyFolderAsync(sourcePath, destPath);

    public Task DeleteFolderAsync(string path, bool recursive = true) =>
        _folderOperator.DeleteFolderAsync(path, recursive);

    public Task MoveFolderAsync(string sourcePath, string destPath) =>
        _folderOperator.MoveFolderAsync(sourcePath, destPath);
    
}