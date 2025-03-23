namespace PeriodicFolderSync.Interfaces;

public interface ICliProcessor
{
    Task<int> ProcessAsync(string[] args);
    
}