namespace PeriodicFolderSync.Interfaces;

public interface IFileOperator
{
    Task CopyFileAsync(string source, string dest, bool overwrite = false);
    Task DeleteFileAsync(string path);
    Task MoveFileAsync(string source, string dest, bool overwrite = false);
    Task RenameFileAsync(string path, string newName, bool overwrite = false);
}