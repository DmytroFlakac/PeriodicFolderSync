using PeriodicFolderSync.Models;

namespace PeriodicFolderSync.Interfaces;

public interface IFileSynchronizer
{
    Task SynchronizeFilesAsync(string source, string destination, SyncStatistics stats, bool useOverwrite);
}