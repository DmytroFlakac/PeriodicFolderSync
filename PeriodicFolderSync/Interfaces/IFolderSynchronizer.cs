using PeriodicFolderSync.Models;

namespace PeriodicFolderSync.Interfaces
{
    /// <summary>
    /// Interface for synchronizing folders between source and destination directories.
    /// This specialized component:
    /// - Handles folder structure synchronization
    /// - Creates, moves, or deletes directories as needed
    /// - Works alongside IFileSynchronizer to ensure complete synchronization
    /// - Tracks statistics about folder operations
    /// </summary>
    public interface IFolderSynchronizer
    {
        /// <summary>
        /// Synchronizes folders from source directory to destination directory.
        /// </summary>
        /// <param name="source">Source directory path.</param>
        /// <param name="destination">Destination directory path.</param>
        /// <param name="stats">Statistics object to track synchronization metrics.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SynchronizeFoldersAsync(string source, string destination, SyncStatistics stats);
    }
}