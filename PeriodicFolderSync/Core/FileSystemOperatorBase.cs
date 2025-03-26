using Microsoft.Extensions.Logging;

namespace PeriodicFolderSync.Core;

/// <summary>
/// Base class for file system operations with retry logic and validation.
/// </summary>
/// <remarks>
/// Provides common functionality for file and folder operations including:
/// - Retry mechanism for transient I/O errors
/// - Path validation
/// - Directory creation
/// </remarks>
/// <param name="logger">Logger for operation tracking and error reporting</param>
/// <param name="retryCount">Number of retry attempts for failed operations</param>
/// <param name="retryDelay">Delay between retry attempts</param>
public abstract class FileSystemOperatorBase(ILogger logger, int retryCount = 3, TimeSpan? retryDelay = null)
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly TimeSpan _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
  
    /// <summary>
    /// Executes an asynchronous action with retry logic for handling transient I/O errors.
    /// </summary>
    /// <param name="action">The asynchronous action to execute</param>
    /// <param name="operationDescription">Description of the operation for logging purposes</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="IOException">Thrown when an I/O error occurs after all retry attempts</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied after all retry attempts</exception>
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
                if (attempts >= retryCount)
                {
                    _logger.LogError(ex, $"Failed after {attempts} attempts for {operationDescription}");
                    throw;
                }
                _logger.LogWarning(ex, $"Retry {attempts}/{retryCount} for {operationDescription}");
                await Task.Delay(_retryDelay);
            }
        }
    }
    
    /// <summary>
    /// Validates that both source and destination paths are not null or empty.
    /// </summary>
    /// <param name="source">The source path to validate</param>
    /// <param name="dest">The destination path to validate</param>
    /// <param name="operation">The name of the operation for error messages</param>
    /// <exception cref="ArgumentException">Thrown when either path is null or empty</exception>
    protected void ValidatePaths(string source, string dest, string operation)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(dest))
            throw new ArgumentException($"{operation}: Source or destination path cannot be null or empty.");
    }

    /// <summary>
    /// Validates that a single path is not null or empty.
    /// </summary>
    /// <param name="path">The path to validate</param>
    /// <param name="operation">The name of the operation for error messages</param>
    /// <exception cref="ArgumentException">Thrown when the path is null or empty</exception>
    protected void ValidatePath(string path, string operation)
    {
        if(string.IsNullOrEmpty(path))
            throw new ArgumentException($"{operation}: Path cannot be null or empty");
    }

    /// <summary>
    /// Creates the directory for a given path if it doesn't exist.
    /// </summary>
    /// <param name="path">The file path whose directory should be created</param>
    /// <remarks>
    /// This method extracts the directory name from the path and creates it if it doesn't exist.
    /// It handles nested directories by creating the full directory structure.
    /// </remarks>
    protected void CreateDirectoryIfNotExist(string path)
    {
        string? destDir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);
    }

    /// <summary>
    /// Performs implementation-specific validation for file system operations.
    /// </summary>
    /// <param name="source">The source path</param>
    /// <param name="dest">The destination path</param>
    /// <param name="operation">The name of the operation</param>
    /// <remarks>
    /// This method should be implemented by derived classes to provide
    /// operation-specific validation logic.
    /// </remarks>
    protected abstract void Validate(string source, string dest, string operation);
}