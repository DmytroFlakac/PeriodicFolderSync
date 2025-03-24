using System.Threading.Tasks;
using PeriodicFolderSync.Models;

namespace PeriodicFolderSync.Interfaces
{
    /// <summary>
    /// Interface for synchronizing directories, including both files and folders.
    /// This is the main orchestrator that:
    /// - Coordinates the synchronization process
    /// - Delegates to specialized components for file and folder operations
    /// - Handles the overall synchronization workflow
    /// </summary>
    public interface ISynchronizer
    {
        /// <summary>
        /// Synchronizes files and folders from source to destination.
        /// </summary>
        /// <param name="source">Source directory path.</param>
        /// <param name="destination">Destination directory path.</param>
        /// <param name="useOverwrite">If true, overwrites existing files and folders at the destination.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        Task SynchronizeAsync(string source, string destination, bool useOverwrite);
    }
}