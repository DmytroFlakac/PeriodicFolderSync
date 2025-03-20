using Microsoft.Extensions.Logging;
using PeriodicFolderSync.Interfaces;
using System.IO;

namespace PeriodicFolderSync.Core;

public class FileOperator(ILogger logger, int retryCount = 3, TimeSpan? retryDelay = null) :
    FileSystemOperatorBase(logger, retryCount, retryDelay), 
        IFileOperator
{
    
    protected override void Validate(string sourcePath, string destPath, string operation, bool overwrite = false)
    {
        ValidatePaths(sourcePath, destPath, operation);
        if(!File.Exists(sourcePath))
            throw new FileNotFoundException($"Source file not found {sourcePath}");
        if (File.Exists(destPath) && !overwrite)
            throw new IOException($"Destination file already exists: {destPath}");
    }
    public async Task CopyFileAsync(string sourcePath, string destPath, bool overwrite = false)
    {
        Validate(sourcePath, destPath, nameof(CopyFileAsync), overwrite);
        
        CreateDirectoryIfNotExist(destPath);

        await WithRetryAsync(async () =>
        {
            await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            await using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await sourceStream.CopyToAsync(destStream);
        }, $"Copy file from {sourcePath} to {destPath}");
    }

    public async Task DeleteFileAsync(string path)
    {
        ValidatePath( path, nameof(DeleteFileAsync));
        
        if (!File.Exists(path))
            return;

        await WithRetryAsync(() => 
                Task.Run(() => 
                    File.Delete(path)),
            $"Delete file {path}");
    }

    public async Task MoveFileAsync(string sourcePath, string destPath, bool overwrite = false)
    {
        Validate(sourcePath, destPath, nameof(CopyFileAsync), overwrite);

        CreateDirectoryIfNotExist(destPath);
        
        if (File.Exists(destPath) && overwrite)
            await DeleteFileAsync(destPath);

        await WithRetryAsync(() => 
                Task.Run(() => 
                    File.Move(sourcePath, destPath)), 
            $"Move file from {sourcePath} to {destPath}");
    }

    public async Task RenameFileAsync(string path, string newName, bool overwrite = false)
    {
        ValidatePath(path, nameof(RenameFileAsync));
    
        if (string.IsNullOrEmpty(newName))
            throw new ArgumentException("New name cannot be null or empty.", nameof(newName));
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        string newPath = Path.IsPathFullyQualified(newName) 
            ? newName 
            : Path.Combine(Path.GetDirectoryName(path) ?? "", newName);

        CreateDirectoryIfNotExist(newPath);


        if (File.Exists(newPath) && !overwrite)
            throw new IOException($"File already exists at {newPath}");
        if (File.Exists(newPath) && overwrite)
            await DeleteFileAsync(newPath);

        await WithRetryAsync(() => Task.Run(() => File.Move(path, newPath)), $"Rename file from {path} to {newPath}");
    }
}