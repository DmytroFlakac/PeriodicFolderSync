using Microsoft.Extensions.Logging;
using PeriodicFolderSync.Interfaces;

namespace PeriodicFolderSync.Core;

public class FolderOperator(
    IFileOperator fileOperator, 
    ILogger logger, 
    IFileSystem? fileSystem = null,
    int retryCount = 3, 
    TimeSpan? retryDelay = null) :
    FileSystemOperatorBase(logger, retryCount, retryDelay),
    IFolderOperator
{
    private readonly IFileSystem _fileSystem = fileSystem ?? new FileSystem();

    protected override void Validate(string sourcePath, string destPath, string operation, bool overwrite = false)
    {
        ValidatePaths(sourcePath, destPath, operation);
        if(!_fileSystem.DirectoryExists(sourcePath))
            throw new DirectoryNotFoundException($"Source directory not found {sourcePath}");
        if (_fileSystem.DirectoryExists(destPath) && !overwrite)
            throw new IOException($"Destination directory already exists: {destPath}");
    }

    public async Task CopyFolderAsync(string sourcePath, string destPath, bool overwrite = false, bool recursive = true)
    {
        Validate(sourcePath, destPath, nameof(CopyFolderAsync), overwrite);
        
        if (_fileSystem.DirectoryExists(destPath) && overwrite)
            await DeleteFolderAsync(destPath, recursive);

        _fileSystem.CreateDirectory(destPath);
        
        foreach (var file in _fileSystem.GetFiles(sourcePath))
        {
            string destFile = Path.Combine(destPath, Path.GetFileName(file));
            await fileOperator.CopyFileAsync(file, destFile, overwrite);
            // await WithRetryAsync(() => fileOperator.CopyFileAsync(file, destFile, overwrite), 
            //     $"Copy file from {file} to {destFile}");
        }
        
        if (recursive)
        {
            foreach (var dir in _fileSystem.GetDirectories(sourcePath))
            {
                string destDir = Path.Combine(destPath, Path.GetFileName(dir));
                await CopyFolderAsync(dir, destDir, overwrite, recursive);
                // await WithRetryAsync(() => CopyFolderAsync(dir, destDir, overwrite, recursive), 
                //     $"Copy folder from {dir} to {destDir}");
            }
        }
    }

    public async Task DeleteFolderAsync(string path, bool recursive = true)
    {
        ValidatePath(path, nameof(DeleteFolderAsync));
        if (!_fileSystem.DirectoryExists(path))
            return;

        await WithRetryAsync(() => Task.Run(() => _fileSystem.DeleteDirectory(path, recursive)), 
            $"Delete folder {path}{(recursive ? " recursively" : "")}");
    }

    public async Task MoveFolderAsync(string sourcePath, string destPath, bool overwrite = false)
    {
        Validate(sourcePath, destPath, nameof(MoveFolderAsync), overwrite);

        string? destParent = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destParent) && !_fileSystem.DirectoryExists(destParent))
            _fileSystem.CreateDirectory(destParent);

        if (_fileSystem.DirectoryExists(destPath) && overwrite)
            await DeleteFolderAsync(destPath, recursive: true);

        await WithRetryAsync(() => Task.Run(() => _fileSystem.MoveDirectory(sourcePath, destPath)), 
            $"Move folder from {sourcePath} to {destPath}");
    }

    public async Task RenameFolderAsync(string path, string newName, bool overwrite = false)
    {
        ValidatePath(path, nameof(RenameFolderAsync));
        if (string.IsNullOrEmpty(newName))
            throw new ArgumentException("New name cannot be null or empty.", nameof(newName));
        if (!_fileSystem.DirectoryExists(path))
            throw new DirectoryNotFoundException($"Folder not found: {path}");

        string newPath = Path.IsPathFullyQualified(newName) 
            ? newName 
            : Path.Combine(Path.GetDirectoryName(path) ?? "", newName);

        _fileSystem.CreateDirectoryIfNotExist(newPath);

        if (_fileSystem.DirectoryExists(newPath) && !overwrite)
            throw new IOException($"Folder already exists at {newPath}");
        if (_fileSystem.DirectoryExists(newPath) && overwrite)
            await DeleteFolderAsync(newPath, recursive: true);

        await WithRetryAsync(() => Task.Run(() => _fileSystem.MoveDirectory(path, newPath)), 
            $"Rename folder from {path} to {newPath}");
    }
}