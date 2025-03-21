using Microsoft.Extensions.Logging;
using PeriodicFolderSync.Interfaces;

namespace PeriodicFolderSync.Core;

public class FileSystemOperator : IFileOperator, IFolderOperator
{
    private readonly IFileOperator _fileOperator;
    private readonly IFolderOperator _folderOperator;

    public FileSystemOperator(ILogger logger, int retryCount = 3, TimeSpan? retryDelay = null, IFileSystem? fileSystem = null)
    {
        _fileOperator = new FileOperator(logger,fileSystem, retryCount, retryDelay);
        _folderOperator = new FolderOperator(_fileOperator, logger, fileSystem, retryCount, retryDelay);
    }

    public Task CopyFileAsync(string sourcePath, string destPath, bool overwrite = false) =>
        _fileOperator.CopyFileAsync(sourcePath, destPath, overwrite);

    public Task DeleteFileAsync(string path) =>
        _fileOperator.DeleteFileAsync(path);


    public Task MoveFileAsync(string sourcePath, string destPath, bool overwrite = false) =>
        _fileOperator.MoveFileAsync(sourcePath, destPath, overwrite);

    public Task RenameFileAsync(string path, string newName, bool overwrite = false) =>
        _fileOperator.RenameFileAsync(path, newName, overwrite);

    public Task CopyFolderAsync(string sourcePath, string destPath, bool overwrite = false, bool recursive = true) =>
        _folderOperator.CopyFolderAsync(sourcePath, destPath, overwrite, recursive);

    public Task DeleteFolderAsync(string path, bool recursive = true) =>
        _folderOperator.DeleteFolderAsync(path, recursive);

    public Task MoveFolderAsync(string sourcePath, string destPath, bool overwrite = false) =>
        _folderOperator.MoveFolderAsync(sourcePath, destPath, overwrite);

    public Task RenameFolderAsync(string path, string newName, bool overwrite = false) =>
        _folderOperator.RenameFolderAsync(path, newName, overwrite);
}