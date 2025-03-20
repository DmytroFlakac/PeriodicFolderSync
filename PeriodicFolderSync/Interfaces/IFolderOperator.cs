namespace PeriodicFolderSync.Interfaces;

public interface IFolderOperator
{
    Task CopyFolderAsync(string source, string dest, bool overwrite = false, bool recursive = true);
    Task DeleteFolderAsync(string path, bool recursive = true);
    Task MoveFolderAsync(string source, string dest, bool overwrite = false);
    Task RenameFolderAsync(string path, string newName, bool overwrite = false);
}