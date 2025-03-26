using System;

namespace PeriodicFolderSync.Interfaces
{
    /// <summary>
    /// Provides configuration for application logging
    /// </summary>
    public interface ILogConfigurationProvider
    {
        /// <summary>
        /// Gets the current log file path
        /// </summary>
        /// <returns>The path where log files will be written</returns>
        string GetLogFilePath();

        /// <summary>
        /// Sets a custom log file path
        /// </summary>
        /// <param name="path">The path where log files should be written</param>
        void SetLogFilePath(string path);

        /// <summary>
        /// Creates a dynamic log file name based on the source and destination paths.
        /// This method:
        /// - Generates a unique log file name based on the source and destination paths
        /// - Ensures that log files are easily identifiable
        /// - Prevents overwriting of log files
        /// <param name="source">The source path for the synchronization</param>
        /// <param name="destination">The destination path for the synchronization</param>
        /// <returns>   A unique log file name based on the source and destination paths</returns>
        /// </summary>     
        string CreateDynamicLogFileName(string source, string destination);
    }
}