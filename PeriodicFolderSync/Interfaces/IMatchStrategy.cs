using System.Threading.Tasks;

namespace PeriodicFolderSync.Interfaces
{
    /// <summary>
    /// Defines methods for determining if folders and files match between source and destination.
    /// This strategy pattern allows for different matching algorithms to be implemented:
    /// - Content-based matching (using hashes or byte-by-byte comparison)
    /// - Metadata-based matching (size, timestamps)
    /// - Custom matching rules for specific use cases
    /// </summary>
    public interface IMatchStrategy
    {
        /// <summary>
        /// Determines if a source folder matches a destination folder based on content analysis.
        /// </summary>
        /// <param name="sourceFolder">The full path to the source folder.</param>
        /// <param name="destFolder">The full path to the destination folder.</param>
        /// <param name="fileSystem">The file system interface to use for operations.</param>
        /// <param name="source">Optional. The root source directory path for relative path comparison.</param>
        /// <param name="destination">Optional. The root destination directory path for relative path comparison.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a boolean value:
        /// true if the folders match, false otherwise.
        /// </returns>
        /// <remarks>
        /// When source and destination parameters are provided, the method will check if the folders
        /// exist at the same relative path in both locations. If they do, it will return false to
        /// prevent unnecessary folder moves.
        /// </remarks>
        Task<bool> IsFolderMatchAsync(string sourceFolder, string destFolder, IFileSystem fileSystem, string source, string destination);

        /// <summary>
        /// Determines if a source file matches a destination file based on content analysis.
        /// </summary>
        /// <param name="sourceFile">The full path to the source file.</param>
        /// <param name="destFile">The full path to the destination file.</param>
        /// <param name="fileSystem">The file system interface to use for operations.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a boolean value:
        /// true if the files match, false otherwise.
        /// </returns>
        /// <remarks>
        /// File matching typically compares file size and last write time to determine if files are identical.
        /// </remarks>
        Task<bool> IsFileMatchAsync(string sourceFile, string destFile, IFileSystem fileSystem);
    }
}