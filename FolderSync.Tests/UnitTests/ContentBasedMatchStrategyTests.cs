using FolderSync.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Moq;
using PeriodicFolderSync.Core;
using PeriodicFolderSync.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace FolderSync.Tests.UnitTests
{
    public class ContentBasedMatchStrategyTests
    {
        private readonly MockFileSystem _fileSystem;
        private readonly Mock<ILogger<IMatchStrategy>> _loggerMock;
        private readonly Mock<IFileComparer> _fileComparerMock;
        private readonly ContentBasedMatchStrategy _matchStrategy;

        public ContentBasedMatchStrategyTests()
        {
            _fileSystem = new MockFileSystem();
            _loggerMock = new Mock<ILogger<IMatchStrategy>>();
            _fileComparerMock = new Mock<IFileComparer>();
            _matchStrategy = new ContentBasedMatchStrategy(_loggerMock.Object, _fileComparerMock.Object);
        }

        [Fact]
        public async Task IsFolderMatchAsync_WhenSourceFolderDoesNotExist_ReturnsFalse()
        {
            // Arrange
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFolder = Path.Combine(source, "NonExistentFolder");
            string destFolder = Path.Combine(destination, "ExistingFolder");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            _fileSystem.CreateDirectory(destFolder);

            // Act
            bool result = await _matchStrategy.IsFolderMatchAsync(sourceFolder, destFolder, _fileSystem, source, destination);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsFolderMatchAsync_WhenDestFolderDoesNotExist_ReturnsFalse()
        {
            // Arrange
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFolder = Path.Combine(source, "ExistingFolder");
            string destFolder = Path.Combine(destination, "NonExistentFolder");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            _fileSystem.CreateDirectory(sourceFolder);

            // Act
            bool result = await _matchStrategy.IsFolderMatchAsync(sourceFolder, destFolder, _fileSystem, source, destination);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsFolderMatchAsync_WhenExpectedSourcePathExists_ReturnsFalse()
        {
            // Arrange
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFolder = Path.Combine(source, "Folder1");
            string destFolder = Path.Combine(destination, "Folder2");
            string expectedSourcePath = Path.Combine(source, "Folder2");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            _fileSystem.CreateDirectory(sourceFolder);
            _fileSystem.CreateDirectory(destFolder);
            _fileSystem.CreateDirectory(expectedSourcePath);

            // Act
            bool result = await _matchStrategy.IsFolderMatchAsync(sourceFolder, destFolder, _fileSystem, source, destination);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsFileMatchAsync_WhenSourceFileDoesNotExist_ReturnsFalse()
        {
            // Arrange
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFile = Path.Combine(source, "nonexistent.txt");
            string destFile = Path.Combine(destination, "existing.txt");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            await _fileSystem.WriteAllTextAsync(destFile, "content");

            // Act
            bool result = await _matchStrategy.IsFileMatchAsync(sourceFile, destFile, _fileSystem, source, destination);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsFileMatchAsync_WhenFilesHaveSameSizeButDifferentContent_ReturnsFalse()
        {
            // Arrange
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFile = Path.Combine(source, "file.txt");
            string destFile = Path.Combine(destination, "oldfile.txt");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            
            // Create files with same length but different content
            string content1 = "content-1";
            string content2 = "content-2";
            await _fileSystem.WriteAllTextAsync(sourceFile, content1);
            await _fileSystem.WriteAllTextAsync(destFile, content2);

            _fileComparerMock.Setup(fc => fc.AreFilesIdenticalAsync(sourceFile, destFile))
                .ReturnsAsync(false);

            // Act
            bool result = await _matchStrategy.IsFileMatchAsync(sourceFile, destFile, _fileSystem, source, destination);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsFolderMatchAsync_WhenFilesCountDiffersTooMuch_ReturnsFalse()
        {
            // Arrange
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFolder = Path.Combine(source, "Folder");
            string destFolder = Path.Combine(destination, "OldFolder");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            _fileSystem.CreateDirectory(sourceFolder);
            _fileSystem.CreateDirectory(destFolder);

            // Add 5 files to source folder
            for (int i = 1; i <= 5; i++)
            {
                await _fileSystem.WriteAllTextAsync(Path.Combine(sourceFolder, $"file{i}.txt"), $"content{i}");
            }

            // Add 1 file to destination folder
            await _fileSystem.WriteAllTextAsync(Path.Combine(destFolder, "file1.txt"), "content1");

            // Act
            bool result = await _matchStrategy.IsFolderMatchAsync(sourceFolder, destFolder, _fileSystem, source, destination);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsFolderMatchAsync_WhenCreationTimeMatchesAndFileCountDifferenceIsSmall_ReturnsTrue()
        {
            // Arrange
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFolder = Path.Combine(source, "Folder");
            string destFolder = Path.Combine(destination, "OldFolder");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            _fileSystem.CreateDirectory(sourceFolder);
            _fileSystem.CreateDirectory(destFolder);

            // Add 3 files to source folder
            for (int i = 1; i <= 3; i++)
            {
                await _fileSystem.WriteAllTextAsync(Path.Combine(sourceFolder, $"file{i}.txt"), $"content{i}");
            }

            // Add 2 files to destination folder
            for (int i = 1; i <= 2; i++)
            {
                await _fileSystem.WriteAllTextAsync(Path.Combine(destFolder, $"file{i}.txt"), $"content{i}");
            }

            // Act
            bool result = await _matchStrategy.IsFolderMatchAsync(sourceFolder, destFolder, _fileSystem, source, destination);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsFileMatchAsync_WhenDestFileDoesNotExist_ReturnsFalse()
        {
            // Arrange
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFile = Path.Combine(source, "existing.txt");
            string destFile = Path.Combine(destination, "nonexistent.txt");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            await _fileSystem.WriteAllTextAsync(sourceFile, "content");

            // Act
            bool result = await _matchStrategy.IsFileMatchAsync(sourceFile, destFile, _fileSystem, source, destination);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsFileMatchAsync_WhenExpectedSourcePathExists_ReturnsFalse()
        {
            // Arrange
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFile = Path.Combine(source, "file.txt");
            string destFile = Path.Combine(destination, "oldfile.txt");
            string expectedSourcePath = Path.Combine(source, "oldfile.txt");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            await _fileSystem.WriteAllTextAsync(sourceFile, "content");
            await _fileSystem.WriteAllTextAsync(destFile, "content");
            await _fileSystem.WriteAllTextAsync(expectedSourcePath, "content");

            // Act
            bool result = await _matchStrategy.IsFileMatchAsync(sourceFile, destFile, _fileSystem, source, destination);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsFileMatchAsync_WhenFilesAreIdentical_ReturnsTrue()
        {
            // Arrange
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFile = Path.Combine(source, "file.txt");
            string destFile = Path.Combine(destination, "oldfile.txt");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            
            string content = "identical content";
            await _fileSystem.WriteAllTextAsync(sourceFile, content);
            await _fileSystem.WriteAllTextAsync(destFile, content);

            _fileComparerMock.Setup(fc => fc.AreFilesIdenticalAsync(sourceFile, destFile))
                .ReturnsAsync(true);

            // Act
            bool result = await _matchStrategy.IsFileMatchAsync(sourceFile, destFile, _fileSystem, source, destination);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsFileMatchAsync_WhenExceptionOccurs_ReturnsFalse()
        {
            // Arrange
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFile = Path.Combine(source, "file.txt");
            string destFile = Path.Combine(destination, "oldfile.txt");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            
            string content = "content";
            await _fileSystem.WriteAllTextAsync(sourceFile, content);
            await _fileSystem.WriteAllTextAsync(destFile, content);

            _fileComparerMock.Setup(fc => fc.AreFilesIdenticalAsync(sourceFile, destFile))
                .ThrowsAsync(new IOException("Simulated IO error"));

            // Act
            bool result = await _matchStrategy.IsFileMatchAsync(sourceFile, destFile, _fileSystem, source, destination);

            // Assert
            Assert.False(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,  // Changed from Error to Warning
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error during file matching")),  // Changed message
                    It.IsAny<IOException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        
        
        [Fact]
        public async Task IsFolderMatchAsync_WhenFoldersHaveSameCreationTimeButDifferentNames_ReturnsTrue()
        {
            // Arrange
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFolder = Path.Combine(source, "SourceFolder");
            string destFolder = Path.Combine(destination, "DestFolder");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            _fileSystem.CreateDirectory(sourceFolder);
            _fileSystem.CreateDirectory(destFolder);
            
            // Add same number of files with same content to both folders
            await _fileSystem.WriteAllTextAsync(Path.Combine(sourceFolder, "file1.txt"), "content1");
            await _fileSystem.WriteAllTextAsync(Path.Combine(sourceFolder, "file2.txt"), "content2");
            
            await _fileSystem.WriteAllTextAsync(Path.Combine(destFolder, "file1.txt"), "content1");
            await _fileSystem.WriteAllTextAsync(Path.Combine(destFolder, "file2.txt"), "content2");

            // Act
            bool result = await _matchStrategy.IsFolderMatchAsync(sourceFolder, destFolder, _fileSystem, source, destination);

            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public async Task IsFolderMatchAsync_WhenFoldersMatchWithMultipleFiles_LogsDebugMessage()
        {
            // Arrange
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFolder = Path.Combine(source, "SourceFolder");
            string destFolder = Path.Combine(destination, "DestFolder");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            _fileSystem.CreateDirectory(sourceFolder);
            _fileSystem.CreateDirectory(destFolder);
            
            // Add identical files to both folders
            for (int i = 1; i <= 3; i++)
            {
                await _fileSystem.WriteAllTextAsync(Path.Combine(sourceFolder, $"file{i}.txt"), $"content{i}");
                await _fileSystem.WriteAllTextAsync(Path.Combine(destFolder, $"file{i}.txt"), $"content{i}");
            }
            
            // Setup file comparer to return true for all comparisons
            _fileComparerMock.Setup(fc => fc.AreFilesIdenticalAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            bool result = await _matchStrategy.IsFolderMatchAsync(sourceFolder, destFolder, _fileSystem, source, destination);

            // Assert
            Assert.True(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Folders matched")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        // [Fact]
        // public async Task IsFolderMatchAsync_WhenPartialFileMatch_LogsDebugWithMatchCount()
        // {
        //     // Arrange
        //     string source = @"C:\Source";
        //     string destination = @"C:\Destination";
        //     string sourceFolder = Path.Combine(source, "SourceFolder");
        //     string destFolder = Path.Combine(destination, "DestFolder");
        //     
        //
        //     _fileSystem.CreateDirectory(source);
        //     _fileSystem.CreateDirectory(destination);
        //     _fileSystem.CreateDirectory(sourceFolder);
        //     _fileSystem.CreateDirectory(destFolder);
        //     
        //     for (int i = 1; i <= 3; i++)
        //     {
        //         await _fileSystem.WriteAllTextAsync(Path.Combine(sourceFolder, $"file{i}.txt"), $"content{i}");
        //     }
        //     
        //     for (int i = 1; i <= 2; i++)
        //     {
        //         await _fileSystem.WriteAllTextAsync(Path.Combine(destFolder, $"file{i}.txt"), $"content{i}");
        //     }
        //     
        //     _loggerMock.Reset();
        //
        //     bool result = await _matchStrategy.IsFolderMatchAsync(sourceFolder, destFolder, _fileSystem, source, destination);
        //
        //     Assert.True(result);
        //     _loggerMock.Verify(
        //         x => x.Log(
        //             LogLevel.Debug,
        //             It.IsAny<EventId>(),
        //             It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Folders matched: {sourceFolder} and {destFolder} (matched")),
        //             null,
        //             It.IsAny<Func<It.IsAnyType, Exception, string>>()),
        //         Times.Once);
        // }
    }
}