namespace PeriodicFolderSync.Interfaces
{
    /// <summary>
    /// Interface for processing command-line arguments.
    /// This component:
    /// - Parses and validates command-line arguments
    /// - Configures the synchronization process based on user input
    /// - Provides a user-friendly interface for the application
    /// - Handles command-line specific error reporting
    /// - Manages interactive input when no arguments are provided
    /// - Coordinates with other components like synchronizers and schedulers
    /// </summary>
    public interface ICLIProcessor
    {
        /// <summary>
        /// Processes command-line arguments and executes the appropriate actions.
        /// This method:
        /// - Parses command-line arguments
        /// - Validates input parameters
        /// - Handles administrative privilege elevation if requested
        /// - Executes one-time synchronization or starts the scheduler
        /// - Returns appropriate exit codes
        /// </summary>
        /// <param name="args">Command-line arguments array.</param>
        /// <returns>A task representing the asynchronous operation with an exit code.</returns>
        Task<int> ProcessAsync(string?[] args);
        
        /// <summary>
        /// Gets interactive input from the user if no command-line arguments were provided.
        /// This method:
        /// - Prompts the user for required parameters (source, destination, interval)
        /// - Asks if administrative privileges are needed
        /// - Formats the input as command-line arguments
        /// - Handles special cases like one-time synchronization
        /// </summary>
        /// <param name="args">The original command-line arguments array.</param>
        /// <returns>A task that returns a new array of command-line arguments based on user input.</returns>
        Task<string?[]> GetInteractiveInputIfNeededAsync(string?[] args);
    }
}