using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PeriodicFolderSync.Core;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace FolderSync.Tests.UnitTests
{
    public class FileOperatorTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _sourceDirectory;
        private readonly string _destinationDirectory;
        private readonly FileOperator _fileOperator;

        public FileOperatorTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "FileOperatorTests_" + Guid.NewGuid().ToString());
            _sourceDirectory = Path.Combine(_testDirectory, "Source");
            _destinationDirectory = Path.Combine(_testDirectory, "Destination");

            Directory.CreateDirectory(_sourceDirectory);
            Directory.CreateDirectory(_destinationDirectory);
            Mock<ILogger> loggerMock = new();
            _fileOperator = new FileOperator(loggerMock.Object);
        }

        public void Dispose()
        {
            // Clean up test directories
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch (IOException)
                {
                    // Ignore cleanup errors
                }
            }
        }

        private string CreateTestFile(string directory, string fileName, string content = "Test content")
        {
            string filePath = Path.Combine(directory, fileName);
            File.WriteAllText(filePath, content);
            return filePath;
        }

        private bool CompareFiles(string file1, string file2)
        {
            if (!File.Exists(file1) || !File.Exists(file2))
                return false;

            byte[] file1Bytes = File.ReadAllBytes(file1);
            byte[] file2Bytes = File.ReadAllBytes(file2);

            if (file1Bytes.Length != file2Bytes.Length)
                return false;

            for (int i = 0; i < file1Bytes.Length; i++)
            {
                if (file1Bytes[i] != file2Bytes[i])
                    return false;
            }

            return true;
        }

        [Fact]
        public async Task CopyFileAsync_ShouldCopyFile_WhenFileDoesNotExistAtDestination()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "source.txt", "Test content");
            string destFile = Path.Combine(_destinationDirectory, "dest.txt");

            await _fileOperator.CopyFileAsync(sourceFile, destFile);

            Assert.True(File.Exists(destFile));
            Assert.True(CompareFiles(sourceFile, destFile));
        }

        [Fact]
        public async Task CopyFileAsync_ShouldThrowException_WhenSourceFileDoesNotExist()
        {
            string sourceFile = Path.Combine(_sourceDirectory, "nonexistent.txt");
            string destFile = Path.Combine(_destinationDirectory, "dest.txt");

            await Assert.ThrowsAsync<FileNotFoundException>(() => 
                _fileOperator.CopyFileAsync(sourceFile, destFile));
        }

        [Fact]
        public async Task CopyFileAsync_ShouldThrowException_WhenDestinationFileExists()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "source.txt");
            string destFile = CreateTestFile(_destinationDirectory, "dest.txt");

            await Assert.ThrowsAsync<IOException>(() => 
                _fileOperator.CopyFileAsync(sourceFile, destFile, false));
        }

        [Fact]
        public async Task CopyFileAsync_ShouldOverwriteDestination_WhenOverwriteIsTrue()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "source.txt", "New content");
            string destFile = CreateTestFile(_destinationDirectory, "dest.txt", "Old content");

            await _fileOperator.CopyFileAsync(sourceFile, destFile, true);

            Assert.True(File.Exists(destFile));
            Assert.Equal("New content", await File.ReadAllTextAsync(destFile));
        }

        [Fact]
        public async Task CopyFileAsync_ShouldCreateDestinationDirectory_WhenItDoesNotExist()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "source.txt");
            string newDestDir = Path.Combine(_destinationDirectory, "NewDir");
            string destFile = Path.Combine(newDestDir, "dest.txt");

            await _fileOperator.CopyFileAsync(sourceFile, destFile);

            Assert.True(Directory.Exists(newDestDir));
            Assert.True(File.Exists(destFile));
        }

        [Fact]
        public async Task DeleteFileAsync_ShouldDeleteFile_WhenFileExists()
        {
            string filePath = CreateTestFile(_sourceDirectory, "toDelete.txt");

            await _fileOperator.DeleteFileAsync(filePath);

            Assert.False(File.Exists(filePath));
        }

        [Fact]
        public async Task DeleteFileAsync_ShouldNotThrowException_WhenFileDoesNotExist()
        {
            string filePath = Path.Combine(_sourceDirectory, "nonexistent.txt");

            await _fileOperator.DeleteFileAsync(filePath);
        }

        [Fact]
        public async Task MoveFileAsync_ShouldMoveFile_WhenDestinationDoesNotExist()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "toMove.txt", "Move content");
            string destFile = Path.Combine(_destinationDirectory, "moved.txt");

            await _fileOperator.MoveFileAsync(sourceFile, destFile);

            Assert.False(File.Exists(sourceFile));
            Assert.True(File.Exists(destFile));
            Assert.Equal("Move content", File.ReadAllText(destFile));
        }

        [Fact]
        public async Task MoveFileAsync_ShouldThrowException_WhenSourceFileDoesNotExist()
        {
            string sourceFile = Path.Combine(_sourceDirectory, "nonexistent.txt");
            string destFile = Path.Combine(_destinationDirectory, "moved.txt");

            await Assert.ThrowsAsync<FileNotFoundException>(() => 
                _fileOperator.MoveFileAsync(sourceFile, destFile));
        }

        [Fact]
        public async Task MoveFileAsync_ShouldOverwriteDestination_WhenOverwriteIsTrue()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "toMove.txt", "New content");
            string destFile = CreateTestFile(_destinationDirectory, "moved.txt", "Old content");

            await _fileOperator.MoveFileAsync(sourceFile, destFile, true);

            Assert.False(File.Exists(sourceFile));
            Assert.True(File.Exists(destFile));
            Assert.Equal("New content", File.ReadAllText(destFile));
        }

        [Fact]
        public async Task RenameFileAsync_ShouldRenameFile_WhenNewNameDoesNotExist()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "original.txt", "Rename content");
            string newName = "renamed.txt";
            string expectedPath = Path.Combine(_sourceDirectory, newName);

            await _fileOperator.RenameFileAsync(sourceFile, newName);

            Assert.False(File.Exists(sourceFile));
            Assert.True(File.Exists(expectedPath));
            Assert.Equal("Rename content", File.ReadAllText(expectedPath));
        }

        [Fact]
        public async Task RenameFileAsync_ShouldAcceptFullPath_WhenNewNameIsFullyQualified()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "original.txt", "Rename content");
            string newPath = Path.Combine(_destinationDirectory, "renamed.txt");

            await _fileOperator.RenameFileAsync(sourceFile, newPath);

            Assert.False(File.Exists(sourceFile));
            Assert.True(File.Exists(newPath));
            Assert.Equal("Rename content", File.ReadAllText(newPath));
        }

        [Fact]
        public async Task RenameFileAsync_ShouldThrowException_WhenFileDoesNotExist()
        {
            string sourceFile = Path.Combine(_sourceDirectory, "nonexistent.txt");
            string newName = "renamed.txt";

            await Assert.ThrowsAsync<FileNotFoundException>(() => 
                _fileOperator.RenameFileAsync(sourceFile, newName));
        }

        [Fact]
        public async Task RenameFileAsync_ShouldThrowException_WhenNewNameIsEmpty()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "original.txt");
            string newName = "";

            await Assert.ThrowsAsync<ArgumentException>(() => 
                _fileOperator.RenameFileAsync(sourceFile, newName));
        }

        [Fact]
        public async Task RenameFileAsync_ShouldOverwriteDestination_WhenOverwriteIsTrue()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "original.txt", "New content");
            string newName = "renamed.txt";
            string newPath = Path.Combine(_sourceDirectory, newName);
            CreateTestFile(_sourceDirectory, newName, "Old content");

            await _fileOperator.RenameFileAsync(sourceFile, newName, true);

            Assert.False(File.Exists(sourceFile));
            Assert.True(File.Exists(newPath));
            Assert.Equal("New content", File.ReadAllText(newPath));
        }

        [Fact]
        public async Task RenameFileAsync_ShouldCreateDirectories_WhenNewPathContainsNonExistentDirectories()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "original.txt", "Content");
            string newDir = Path.Combine(_destinationDirectory, "NewSubDir");
            string newPath = Path.Combine(newDir, "renamed.txt");

            await _fileOperator.RenameFileAsync(sourceFile, newPath);

            Assert.False(File.Exists(sourceFile));
            Assert.True(Directory.Exists(newDir));
            Assert.True(File.Exists(newPath));
            Assert.Equal("Content", File.ReadAllText(newPath));
        }

        [Fact]
        public async Task CopyFileAsync_ShouldHandleLargeFiles()
        {
            string sourceFile = Path.Combine(_sourceDirectory, "large.bin");
            string destFile = Path.Combine(_destinationDirectory, "large_copy.bin");
            
            using (var fs = new FileStream(sourceFile, FileMode.Create))
            {
                fs.SetLength(5 * 1024 * 1024); // 5MB
                byte[] buffer = new byte[4096];
                new Random().NextBytes(buffer);
                
                for (int i = 0; i < 1280; i++) // 1280 * 4KB = 5MB
                {
                    fs.Write(buffer, 0, buffer.Length);
                }
            }

            await _fileOperator.CopyFileAsync(sourceFile, destFile);

            Assert.True(File.Exists(destFile));
            Assert.Equal(new FileInfo(sourceFile).Length, new FileInfo(destFile).Length);
            Assert.True(CompareFiles(sourceFile, destFile));
        }
    }
}