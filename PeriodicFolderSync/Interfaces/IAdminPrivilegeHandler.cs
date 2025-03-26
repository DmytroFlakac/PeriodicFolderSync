using System.Diagnostics;

namespace PeriodicFolderSync.Interfaces
{
    /// <summary>
    /// Interface for handling administrative privileges across different operating systems.
    /// This component:
    /// - Detects if the application is running with elevated privileges
    /// - Provides methods to restart the application with elevated privileges
    /// - Handles platform-specific elevation mechanisms (UAC on Windows, sudo on Linux/macOS)
    /// - Manages the transition between non-elevated and elevated processes
    /// </summary>
    public interface IAdminPrivilegeHandler
    {
        /// <summary>
        /// Checks if the current process is running with administrative privileges.
        /// </summary>
        /// <returns>True if the process has administrative privileges, false otherwise.</returns>
        bool IsRunningAsAdmin();

        /// <summary>
        /// Restarts the current application with administrative privileges.
        /// </summary>
        /// <param name="args">The command-line arguments to pass to the new process.</param>
        /// <returns>True if the restart was initiated successfully, false otherwise.</returns>
        bool RestartAsAdmin(string?[] args);
    }
}