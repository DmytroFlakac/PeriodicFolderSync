using System.Threading.Tasks;
using PeriodicFolderSync.Models;

namespace PeriodicFolderSync.Interfaces;

/// <summary>
/// Interface for synchronizing directories
/// </summary>
public interface ISynchronizer
{
    /// <summary>
    /// Synchronizes files and folders from source to destination
    /// </summary>
    /// <param name="source">Source directory path</param>
    /// <param name="destination">Destination directory path</param>
    /// <param name="useOverwrite"></param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task SynchronizeAsync(string source, string destination, bool useOverwrite);
}