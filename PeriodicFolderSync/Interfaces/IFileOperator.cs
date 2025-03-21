namespace PeriodicFolderSync.Interfaces;

/// <summary>
/// Provides operations for managing files with retry capabilities.
/// </summary>
public interface IFileOperator
{
    /// <summary>
    /// Asynchronously copies a file from the source path to the destination path.
    /// </summary>
    /// <param name="sourcePath">The path of the file to copy.</param>
    /// <param name="destPath">The destination path where the file will be copied to.</param>
    /// <param name="overwrite">If true, overwrites the destination file if it exists; otherwise, throws an exception.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.IO.FileNotFoundException">Thrown when the source file does not exist.</exception>
    /// <exception cref="System.IO.IOException">Thrown when the destination file exists and overwrite is false.</exception>
    Task CopyFileAsync(string sourcePath, string destPath, bool overwrite = false);
    
    /// <summary>
    /// Asynchronously deletes a file at the specified path.
    /// </summary>
    /// <param name="path">The path of the file to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteFileAsync(string path);
    
    /// <summary>
    /// Asynchronously moves a file from the source path to the destination path.
    /// </summary>
    /// <param name="sourcePath">The path of the file to move.</param>
    /// <param name="destPath">The destination path where the file will be moved to.</param>
    /// <param name="overwrite">If true, overwrites the destination file if it exists; otherwise, throws an exception.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.IO.FileNotFoundException">Thrown when the source file does not exist.</exception>
    /// <exception cref="System.IO.IOException">Thrown when the destination file exists and overwrite is false.</exception>
    Task MoveFileAsync(string sourcePath, string destPath, bool overwrite = false);
    
    /// <summary>
    /// Asynchronously renames a file at the specified path.
    /// </summary>
    /// <param name="path">The path of the file to rename.</param>
    /// <param name="newName">The new name for the file. Can be a relative name or a fully qualified path.</param>
    /// <param name="overwrite">If true, overwrites the destination file if it exists; otherwise, throws an exception.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.ArgumentException">Thrown when newName is null or empty.</exception>
    /// <exception cref="System.IO.FileNotFoundException">Thrown when the file to rename does not exist.</exception>
    /// <exception cref="System.IO.IOException">Thrown when a file with the new name exists and overwrite is false.</exception>
    Task RenameFileAsync(string path, string newName, bool overwrite = false);
}