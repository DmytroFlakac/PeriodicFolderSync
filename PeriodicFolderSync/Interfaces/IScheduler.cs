namespace PeriodicFolderSync.Interfaces;

public interface IScheduler
{
    Task Start(string source, string destination, TimeSpan interval, bool useOverwrite);
    Task Stop();
}