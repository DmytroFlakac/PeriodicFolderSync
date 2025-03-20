using Microsoft.Extensions.Logging;
using PeriodicFolderSync.Interfaces;

namespace PeriodicFolderSync.Core;

public class FolderOperator(IFileOperator fileOperator, ILogger logger, int retryCount = 3, TimeSpan? retryDelay = null) :
    FileSystemOperatorBase(logger, retryCount, retryDelay), 
    IFolderOperator
{
    protected override void Validate(string sourcePath, string destPath, string operation, bool overwrite = false)
    {
        ValidatePaths(sourcePath, destPath, operation);
        if(!File.Exists(sourcePath))
            throw new FileNotFoundException($"Source file not found {sourcePath}");
        if (File.Exists(destPath) && !overwrite)
            throw new IOException($"Destination file already exists: {destPath}");
    }

    public async Task CopyFolderAsync(string sourcePath, string destPath, bool overwrite = false, bool recursive = true)
    {
        Validate(sourcePath, destPath, nameof(CopyFolderAsync), overwrite);
        
        if (Directory.Exists(destPath) && overwrite)
            await DeleteFolderAsync(destPath, recursive);

        Directory.CreateDirectory(destPath);
        foreach (var file in Directory.EnumerateFiles(sourcePath))
        {
            string destFile = Path.Combine(destPath, Path.GetFileName(file));
            await fileOperator.CopyFileAsync(file, destFile, overwrite);
        }
        if (recursive)
        {
            foreach (var dir in Directory.EnumerateDirectories(sourcePath))
            {
                string destDir = Path.Combine(destPath, Path.GetFileName(dir));
                await CopyFolderAsync(dir, destDir, overwrite, recursive);
            }
        }
    }

    public async Task DeleteFolderAsync(string path, bool recursive = true)
    {
        ValidatePath(path, nameof(DeleteFolderAsync));
        if (!Directory.Exists(path))
            return;

        await WithRetryAsync(() => Task.Run(() => Directory.Delete(path, recursive)), 
            $"Delete folder {path}{(recursive ? " recursively" : "")}");
    }

    public async Task MoveFolderAsync(string sourcePath, string destPath, bool overwrite = false)
    {
        Validate(sourcePath, destPath, nameof(MoveFolderAsync), overwrite);

        string? destParent = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destParent) && !Directory.Exists(destParent))
            await WithRetryAsync(() => Task.Run(() => Directory.CreateDirectory(destParent)), 
                $"Create parent directory {destParent}");

        if (Directory.Exists(destPath) && overwrite)
            await DeleteFolderAsync(destPath, recursive: true);

        await WithRetryAsync(() => Task.Run(() => Directory.Move(sourcePath, destPath)), 
            $"Move folder from {sourcePath} to {destPath}");
    }

    public async Task RenameFolderAsync(string path, string newName, bool overwrite = false)
    {
        ValidatePath(path, nameof(RenameFolderAsync));
        if (string.IsNullOrEmpty(newName))
            throw new ArgumentException("New name cannot be null or empty.", nameof(newName));
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Folder not found: {path}");

        string newPath = Path.IsPathFullyQualified(newName) 
            ? newName 
            : Path.Combine(Path.GetDirectoryName(path) ?? "", newName);

        CreateDirectoryIfNotExist(newPath);

        if (Directory.Exists(newPath) && !overwrite)
            throw new IOException($"Folder already exists at {newPath}");
        if (Directory.Exists(newPath) && overwrite)
            await DeleteFolderAsync(newPath, recursive: true);

        await WithRetryAsync(() => Task.Run(() => Directory.Move(path, newPath)), 
            $"Rename folder from {path} to {newPath}");
    }
}