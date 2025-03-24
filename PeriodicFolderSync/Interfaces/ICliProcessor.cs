namespace PeriodicFolderSync.Interfaces
{
    /// <summary>
    /// Interface for processing command-line arguments.
    /// This component:
    /// - Parses and validates command-line arguments
    /// - Configures the synchronization process based on user input
    /// - Provides a user-friendly interface for the application
    /// - Handles command-line specific error reporting
    /// </summary>
    public interface ICliProcessor
    {
        /// <summary>
        /// Processes command-line arguments and executes the appropriate actions.
        /// </summary>
        /// <param name="args">Command-line arguments array.</param>
        /// <returns>A task representing the asynchronous operation with an exit code.</returns>
        Task<int> ProcessAsync(string[] args);
    }
}