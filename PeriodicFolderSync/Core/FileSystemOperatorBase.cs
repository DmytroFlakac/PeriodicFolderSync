using Microsoft.Extensions.Logging;

namespace PeriodicFolderSync.Core;

public abstract class FileSystemOperatorBase(ILogger logger, int retryCount = 3, TimeSpan? retryDelay = null)
{
    protected readonly ILogger Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    protected readonly int RetryCount = retryCount;
    protected readonly TimeSpan RetryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
  
    
    protected async Task WithRetryAsync(Func<Task> action, string operationDescription)
    {
        int attempts = 0;
        while (true)
        {
            try
            {
                attempts++;
                await action();
                return;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                if (attempts >= RetryCount)
                {
                    Logger.LogError(ex, $"Failed after {attempts} attempts for {operationDescription}");
                    throw;
                }
                Logger.LogWarning(ex, $"Retry {attempts}/{RetryCount} for {operationDescription}");
                await Task.Delay(RetryDelay);
            }
        }
    }
    
    protected void ValidatePaths(string source, string dest, string operation)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(dest))
            throw new ArgumentException($"{operation}: Source or destination path cannot be null or empty.");
    }

    protected void ValidatePath(string path, string operation)
    {
        if(string.IsNullOrEmpty(path))
            throw new ArgumentException($"{operation}: Path cannot be null or empty");
    }

    protected void CreateDirectoryIfNotExist(string path)
    {
        string? destDir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);
    }

    protected abstract void Validate(string source, string dest, string operation, bool overwrite = false);
}