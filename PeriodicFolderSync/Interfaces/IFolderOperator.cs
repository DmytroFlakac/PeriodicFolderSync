namespace PeriodicFolderSync.Interfaces;

/// <summary>
/// Provides operations for managing folders with retry capabilities.
/// </summary>
public interface IFolderOperator
{
    /// <summary>
    /// Asynchronously copies a folder and its contents from the source path to the destination path.
    /// </summary>
    /// <param name="sourcePath">The path of the folder to copy.</param>
    /// <param name="destPath">The destination path where the folder will be copied to.</param>
    /// <param name="overwrite">If true, overwrites files and folders at the destination if they exist; otherwise, throws an exception.</param>
    /// <param name="recursive">If true, copies all subdirectories and their contents; otherwise, only copies files in the root directory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.IO.DirectoryNotFoundException">Thrown when the source folder does not exist.</exception>
    /// <exception cref="System.IO.IOException">Thrown when the destination folder exists and overwrite is false.</exception>
    Task CopyFolderAsync(string sourcePath, string destPath, bool overwrite = false, bool recursive = true);
    
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
    
    /// <summary>
    /// Asynchronously renames a folder at the specified path.
    /// </summary>
    /// <param name="path">The path of the folder to rename.</param>
    /// <param name="newName">The new name for the folder. Can be a relative name or a fully qualified path.</param>
    /// <param name="overwrite">If true, overwrites the destination folder if it exists; otherwise, throws an exception.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.ArgumentException">Thrown when newName is null or empty.</exception>
    /// <exception cref="System.IO.DirectoryNotFoundException">Thrown when the folder to rename does not exist.</exception>
    /// <exception cref="System.IO.IOException">Thrown when a folder with the new name exists and overwrite is false.</exception>
    Task RenameFolderAsync(string path, string newName, bool overwrite = false);
}