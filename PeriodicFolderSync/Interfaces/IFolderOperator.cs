namespace PeriodicFolderSync.Interfaces;

/// <summary>
/// Interface for folder operations that abstracts common directory manipulation tasks.
/// This interface provides methods to:
/// - Copy folders with various options (recursive, overwrite)
/// - Delete folders safely
/// - Move folders between locations
/// - Handle error conditions like missing directories or permission issues
/// </summary>
public interface IFolderOperator
{
    /// <summary>
    /// Asynchronously copies a folder and its contents from the source path to the destination path.
    /// </summary>
    /// <param name="sourcePath">The path of the folder to copy.</param>
    /// <param name="destPath">The destination path where the folder will be copied to.</param>
    /// <param name="overwrite">If true, overwrites files and folders at the destination if they exist; otherwise, throws an exception.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.IO.DirectoryNotFoundException">Thrown when the source folder does not exist.</exception>
    /// <exception cref="System.IO.IOException">Thrown when the destination folder exists and overwrite is false.</exception>
    Task CopyFolderAsync(string sourcePath, string destPath, bool overwrite = false);
    
    /// <summary>
    /// Asynchronously deletes a folder at the specified path.
    /// </summary>
    /// <param name="path">The path of the folder to delete.</param>
    /// <param name="recursive">If true, deletes all subdirectories and files; otherwise, only deletes the folder if it's empty.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.IO.IOException">Thrown when recursive is false and the directory is not empty.</exception>
    Task DeleteFolderAsync(string path, bool recursive = true);
    
    /// <summary>
    /// Asynchronously moves a folder from the source path to the destination path.
    /// </summary>
    /// <param name="sourcePath">The path of the folder to move.</param>
    /// <param name="destPath">The destination path where the folder will be moved to.</param>
    /// <param name="overwrite">If true, overwrites the destination folder if it exists; otherwise, throws an exception.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.IO.DirectoryNotFoundException">Thrown when the source folder does not exist.</exception>
    /// <exception cref="System.IO.IOException">Thrown when the destination folder exists and overwrite is false.</exception>
    Task MoveFolderAsync(string sourcePath, string destPath, bool overwrite = false);
    
    
}