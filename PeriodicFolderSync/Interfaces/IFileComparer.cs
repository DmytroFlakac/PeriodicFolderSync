using System;
using System.Threading.Tasks;

namespace PeriodicFolderSync.Interfaces
{
    /// <summary>
    /// Interface for comparing file contents to determine if files are identical.
    /// This is used during synchronization to:
    /// - Avoid unnecessary file copies when content is identical
    /// - Determine which files need to be updated
    /// - Optimize performance by using different comparison strategies based on file size
    /// </summary>
    public interface IFileComparer
    {
        /// <summary>
        /// Determines whether two files have identical content.
        /// </summary>
        /// <param name="file1Path">Path to the first file.</param>
        /// <param name="file2Path">Path to the second file.</param>
        /// <returns>True if files are identical, false otherwise.</returns>
        Task<bool> AreFilesIdenticalAsync(string file1Path, string file2Path);
        
        /// <summary>
        /// Calculates MD5 hash for a file.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <returns>MD5 hash as a lowercase hex string.</returns>
        Task<string> CalculateFileHashAsync(string filePath);
    }
}