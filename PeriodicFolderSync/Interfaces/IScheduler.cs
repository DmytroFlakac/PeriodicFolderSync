namespace PeriodicFolderSync.Interfaces
{
    /// <summary>
    /// Interface for scheduling periodic synchronization operations.
    /// This component:
    /// - Manages the timing of synchronization runs
    /// - Handles starting and stopping the synchronization process
    /// - Provides a way to run synchronization at specified intervals
    /// - Controls the lifecycle of the synchronization process
    /// </summary>
    public interface IScheduler
    {
        /// <summary>
        /// Starts the scheduler to perform periodic synchronization.
        /// </summary>
        /// <param name="source">Source directory path.</param>
        /// <param name="destination">Destination directory path.</param>
        /// <param name="interval">Time interval between synchronization operations.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Start(string source, string destination, TimeSpan interval);
        
        /// <summary>
        /// Stops the scheduler and any ongoing synchronization operations.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Stop();
    }
}