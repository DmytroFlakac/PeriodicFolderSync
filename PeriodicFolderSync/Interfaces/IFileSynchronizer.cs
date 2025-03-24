using PeriodicFolderSync.Models;

namespace PeriodicFolderSync.Interfaces
{
    /// <summary>
    /// Interface for synchronizing files between source and destination directories.
    /// This specialized component:
    /// - Handles file-specific synchronization logic
    /// - Compares files using IFileComparer
    /// - Copies, updates, or deletes files as needed
    /// - Tracks statistics about file operations
    /// </summary>
    public interface IFileSynchronizer
    {
        /// <summary>
        /// Synchronizes files from source directory to destination directory.
        /// </summary>
        /// <param name="source">Source directory path.</param>
        /// <param name="destination">Destination directory path.</param>
        /// <param name="stats">Statistics object to track synchronization metrics.</param>
        /// <param name="useOverwrite">If true, overwrites existing files at the destination.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SynchronizeFilesAsync(string source, string destination, SyncStatistics stats, bool useOverwrite);
    }
}