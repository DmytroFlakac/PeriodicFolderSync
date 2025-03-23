using PeriodicFolderSync.Models;

namespace PeriodicFolderSync.Interfaces;

public interface IFolderSynchronizer
{
    Task SynchronizeFoldersAsync(string source, string destination, SyncStatistics stats, bool useOverwrite);
}