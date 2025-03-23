using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PeriodicFolderSync.Interfaces
{
    /// <summary>
    /// Interface for file system operations to enable testability and abstraction from the physical file system.
    /// </summary>
    public interface IFileSystem
    {
        /// <summary>
        /// Determines whether the specified file exists.
        /// </summary>
        /// <param name="path">The file path to check.</param>
        /// <returns>True if the file exists; otherwise, false.</returns>
        bool FileExists(string path);

        /// <summary>
        /// Asynchronously reads all bytes from a file.
        /// </summary>
        /// <param name="path">The file path to read from.</param>
        /// <returns>A task that represents the asynchronous read operation, which wraps the byte array containing the contents of the file.</returns>
        Task<byte[]> ReadAllBytesAsync(string path);

        /// <summary>
        /// Asynchronously reads all text from a file.
        /// </summary>
        /// <param name="path">The file path to read from.</param>
        /// <returns>A task that represents the asynchronous read operation, which wraps the string containing the contents of the file.</returns>
        Task<string> ReadAllTextAsync(string path);

        /// <summary>
        /// Asynchronously writes the specified byte array to a file.
        /// </summary>
        /// <param name="path">The file path to write to.</param>
        /// <param name="bytes">The bytes to write to the file.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        Task WriteAllBytesAsync(string path, byte[] bytes);

        /// <summary>
        /// Asynchronously writes the specified string to a file.
        /// </summary>
        /// <param name="path">The file path to write to.</param>
        /// <param name="contents">The string to write to the file.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        Task WriteAllTextAsync(string path, string contents);

        /// <summary>
        /// Asynchronously copies a file to a new location.
        /// </summary>
        /// <param name="sourcePath">The source file path.</param>
        /// <param name="destPath">The destination file path.</param>
        /// <param name="overwrite">True to overwrite if the destination file already exists; otherwise, false.</param>
        /// <returns>A task that represents the asynchronous copy operation.</returns>
        Task CopyFileAsync(string sourcePath, string destPath, bool overwrite = false);

        /// <summary>
        /// Asynchronously copies a folder to a new location.
        /// </summary>
        /// <param name="sourcePath">The source file path.</param>
        /// <param name="destPath">The destination file path.</param>
        /// <param name="overwrite">True to overwrite if the destination file already exists; otherwise, false.</param>
        /// <returns>A task that represents the asynchronous copy operation.</returns>
        Task CopyFolderAsync(string sourcePath, string destPath, bool overwrite = false);

        /// <summary>
        /// Asynchronously deletes the specified file.
        /// </summary>
        /// <param name="path">The file path to delete.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        Task DeleteFileAsync(string path);

        /// <summary>
        /// Asynchronously moves a file to a new location.
        /// </summary>
        /// <param name="sourcePath">The source file path.</param>
        /// <param name="destPath">The destination file path.</param>
        /// <param name="overwrite">True to overwrite if the destination file already exists; otherwise, false.</param>
        /// <returns>A task that represents the asynchronous move operation.</returns>
        Task MoveFileAsync(string sourcePath, string destPath, bool overwrite = false);
        
        
        // Directory operations
        /// <summary>
        /// Determines whether the specified directory exists.
        /// </summary>
        /// <param name="path">The directory path to check.</param>
        /// <returns>True if the directory exists; otherwise, false.</returns>
        bool DirectoryExists(string path);

        /// <summary>
        /// Creates all directories and subdirectories in the specified path.
        /// </summary>
        /// <param name="path">The directory path to create.</param>
        void CreateDirectory(string path);

        /// <summary>
        /// Creates the directory for the specified file path if it doesn't exist.
        /// </summary>
        /// <param name="directoryPath">The file path whose directory should be created if it doesn't exist.</param>
        void CreateDirectoryIfNotExist(string directoryPath);

        /// <summary>
        /// Returns an enumerable collection of file names in a specified path.
        /// </summary>
        /// <param name="path">The directory path to search.</param>
        /// <returns>An enumerable collection of the full names (including paths) for the files in the directory.</returns>
        IEnumerable<string> GetFiles(string path);

        /// <summary>
        /// Returns an enumerable collection of directory names in a specified path.
        /// </summary>
        /// <param name="path">The directory path to search.</param>
        /// <returns>An enumerable collection of the full names (including paths) for the subdirectories in the directory.</returns>
        IEnumerable<string> GetDirectories(string path);

        /// <summary>
        /// Deletes the specified directory and, if indicated, any subdirectories and files in the directory.
        /// </summary>
        /// <param name="path">The directory path to delete.</param>
        /// <param name="recursive">True to remove directories, subdirectories, and files in path; otherwise, false.</param>
        void DeleteDirectory(string path, bool recursive);

        /// <summary>
        /// Moves a directory and its contents to a new location.
        /// </summary>
        /// <param name="sourcePath">The source directory path.</param>
        /// <param name="destPath">The destination directory path.</param>
        void MoveDirectory(string sourcePath, string destPath);
        
        /// <summary>
        /// Gets file information for the specified file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>A FileInfo object containing information about the file.</returns>
        FileInfo GetFileInfo(string path);
        
        /// <summary>
        /// Gets directory information for the specified directory.
        /// </summary>
        /// <param name="path">The directory path.</param>
        /// <returns>A DirectoryInfo object containing information about the directory.</returns>
        DirectoryInfo GetDirectoryInfo(string path);
        
        /// <summary>
        /// Gets all directories within the specified directory.
        /// </summary>
        /// <param name="path">The directory path to search.</param>
        /// <returns>An array of DirectoryInfo objects.</returns>
        DirectoryInfo[] GetDirectories(DirectoryInfo directory);
        
        /// <summary>
        /// Gets all files within the specified directory.
        /// </summary>
        /// <param name="directory">The DirectoryInfo object representing the directory to search.</param>
        /// <returns>An array of FileInfo objects.</returns>
        FileInfo[] GetFiles(DirectoryInfo directory);
    }
}