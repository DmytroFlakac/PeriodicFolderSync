using FolderSync.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Moq;
using PeriodicFolderSync.Core;
using PeriodicFolderSync.Interfaces;
using PeriodicFolderSync.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace FolderSync.Tests.UnitTests
{
    public class FileSynchronizerTests
    {
        private readonly MockFileSystem _fileSystem;
        private readonly Mock<ILogger<IFileSynchronizer>> _loggerMock;
        private readonly Mock<IMatchStrategy> _matchStrategyMock;
        private readonly FileSynchronizer _synchronizer;
        private readonly Mock<ILogger<IFileOperator>> _loggerFileOperator;
        private readonly Mock<IFileOperator> _fileOperatorMock;

        public FileSynchronizerTests()
        {
            _fileSystem = new MockFileSystem();
            _loggerMock = new Mock<ILogger<IFileSynchronizer>>();
            _matchStrategyMock = new Mock<IMatchStrategy>();
            _loggerFileOperator = new Mock<ILogger<IFileOperator>>();
            _fileOperatorMock = new Mock<IFileOperator>();

            _fileOperatorMock.Setup(f => f.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _fileOperatorMock.Setup(f => f.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _fileOperatorMock.Setup(f => f.DeleteFileAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _synchronizer = new FileSynchronizer(
                _fileOperatorMock.Object,
                _fileSystem,
                _matchStrategyMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task SynchronizeFiles_CopiesNewFiles_WhenNotInDestination()
        {
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFile = Path.Combine(source, "test.txt");
            string destFile = Path.Combine(destination, "test.txt");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            await _fileSystem.WriteAllTextAsync(sourceFile, "test content");

            var stats = new SyncStatistics();

            await _synchronizer.SynchronizeFilesAsync(source, destination, stats);

            _fileOperatorMock.Verify(f => f.CopyFileAsync(sourceFile, destFile), Times.Once);
            Assert.Equal(1, stats.ChangedCount);
        }

        [Fact]
        public async Task SynchronizeFiles_MovesFiles_WhenMatchFound()
        {
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFile = Path.Combine(source, "correct.txt");
            string destFile = Path.Combine(destination, "old.txt");
            string expectedDestFile = Path.Combine(destination, "correct.txt");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            await _fileSystem.WriteAllTextAsync(sourceFile, "test content");
            await _fileSystem.WriteAllTextAsync(destFile, "test content");

            _matchStrategyMock
                .Setup(m => m.IsFileMatchAsync(sourceFile, destFile, _fileSystem, source, destination))
                .ReturnsAsync(true);

            var stats = new SyncStatistics();

            await _synchronizer.SynchronizeFilesAsync(source, destination, stats);

            _fileOperatorMock.Verify(f => f.MoveFileAsync(destFile, expectedDestFile), Times.Once);
            Assert.Equal(1, stats.FilesMoved);
        }

        [Fact]
        public async Task SynchronizeFiles_UpdatesModifiedFiles()
        {
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFile = Path.Combine(source, "test.txt");
            string destFile = Path.Combine(destination, "test.txt");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            await _fileSystem.WriteAllTextAsync(sourceFile, "updated content");
            await _fileSystem.WriteAllTextAsync(destFile, "old content");

            var stats = new SyncStatistics();

            await _synchronizer.SynchronizeFilesAsync(source, destination, stats);

            _fileOperatorMock.Verify(f => f.CopyFileAsync(sourceFile, destFile), Times.Once);
            Assert.Equal(1, stats.ChangedCount);
        }
        

        [Fact]
        public async Task SynchronizeFiles_DeletesExtraDestinationFiles()
        {
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string extraDestFile = Path.Combine(destination, "extra.txt");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            await _fileSystem.WriteAllTextAsync(extraDestFile, "extra content");

            var stats = new SyncStatistics();

            await _synchronizer.SynchronizeFilesAsync(source, destination, stats);

            _fileOperatorMock.Verify(f => f.DeleteFileAsync(extraDestFile), Times.Once);
            Assert.Equal(1, stats.DeletedFiles);
        }

        [Fact]
        public async Task SynchronizeFiles_CreatesDestinationDirectories_WhenNeeded()
        {
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceSubDir = Path.Combine(source, "SubDir");
            string sourceFile = Path.Combine(sourceSubDir, "test.txt");
            string destSubDir = Path.Combine(destination, "SubDir");
            string destFile = Path.Combine(destSubDir, "test.txt");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(sourceSubDir);
            _fileSystem.CreateDirectory(destination);
            await _fileSystem.WriteAllTextAsync(sourceFile, "test content");

            var stats = new SyncStatistics();

            await _synchronizer.SynchronizeFilesAsync(source, destination, stats);

            Assert.True(_fileSystem.DirectoryExists(destSubDir));
            _fileOperatorMock.Verify(f => f.CopyFileAsync(sourceFile, destFile), Times.Once);
        }

        
        [Fact]
        public async Task SynchronizeFiles_HandlesNestedDirectoryStructure()
        {
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceLevel1 = Path.Combine(source, "Level1");
            string sourceLevel2 = Path.Combine(sourceLevel1, "Level2");
            string sourceLevel3 = Path.Combine(sourceLevel2, "Level3");
            string sourceFile = Path.Combine(sourceLevel3, "deep.txt");

            string destLevel1 = Path.Combine(destination, "Level1");
            string destLevel2 = Path.Combine(destLevel1, "Level2");
            string destLevel3 = Path.Combine(destLevel2, "Level3");
            string destFile = Path.Combine(destLevel3, "deep.txt");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(sourceLevel1);
            _fileSystem.CreateDirectory(sourceLevel2);
            _fileSystem.CreateDirectory(sourceLevel3);
            _fileSystem.CreateDirectory(destination);
            await _fileSystem.WriteAllTextAsync(sourceFile, "deep content");

            var stats = new SyncStatistics();

            await _synchronizer.SynchronizeFilesAsync(source, destination, stats);

            _fileOperatorMock.Verify(f => f.CopyFileAsync(sourceFile, destFile), Times.Once);
        }

        [Fact]
        public async Task SynchronizeFiles_HandlesExceptionsDuringFileMove()
        {
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFile = Path.Combine(source, "correct.txt");
            string destFile = Path.Combine(destination, "old.txt");
            string expectedDestFile = Path.Combine(destination, "correct.txt");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            await _fileSystem.WriteAllTextAsync(sourceFile, "test content");
            await _fileSystem.WriteAllTextAsync(destFile, "test content");

            _matchStrategyMock
                .Setup(m => m.IsFileMatchAsync(sourceFile, destFile, _fileSystem, source, destination))
                .ReturnsAsync(true);

            var mockFileOperator = new Mock<IFileOperator>();
            mockFileOperator
                .Setup(f => f.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new IOException("Simulated move error"));
            
            mockFileOperator
                .Setup(f => f.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            
            mockFileOperator
                .Setup(f => f.DeleteFileAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var synchronizer = new FileSynchronizer(
                mockFileOperator.Object,
                _fileSystem,
                _matchStrategyMock.Object,
                _loggerMock.Object
            );

            var stats = new SyncStatistics();
            
            await synchronizer.SynchronizeFilesAsync(source, destination, stats);

            mockFileOperator.Verify(f => f.MoveFileAsync(destFile, expectedDestFile), Times.Once);
            mockFileOperator.Verify(f => f.CopyFileAsync(sourceFile, expectedDestFile), Times.Once);
        }

        [Fact]
        public async Task SynchronizeFiles_HandlesMultipleFilesWithSameSize()
        {
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFile = Path.Combine(source, "correct.txt");
            string destFile1 = Path.Combine(destination, "old1.txt");
            string destFile2 = Path.Combine(destination, "old2.txt");
            string expectedDestFile = Path.Combine(destination, "correct.txt");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            await _fileSystem.WriteAllTextAsync(sourceFile, "test content");
            await _fileSystem.WriteAllTextAsync(destFile1, "test content 1");
            await _fileSystem.WriteAllTextAsync(destFile2, "test content 2");

            _matchStrategyMock
                .Setup(m => m.IsFileMatchAsync(sourceFile, destFile1, _fileSystem, source, destination))
                .ReturnsAsync(false);
            _matchStrategyMock
                .Setup(m => m.IsFileMatchAsync(sourceFile, destFile2, _fileSystem, source, destination))
                .ReturnsAsync(true);

            var stats = new SyncStatistics();

            await _synchronizer.SynchronizeFilesAsync(source, destination, stats);

            Assert.Equal(1, stats.ChangedCount);
            Assert.Equal(2, stats.DeletedFiles);
        }

        [Fact]
        public async Task SynchronizeFiles_LogsProgress()
        {   
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            
            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            
            for (int i = 1; i <= 200; i++)
            {
                string fileName = $"file{i}.txt";
                await _fileSystem.WriteAllTextAsync(Path.Combine(source, fileName), $"content {i}");
            }

            var stats = new SyncStatistics();

            await _synchronizer.SynchronizeFilesAsync(source, destination, stats);
            
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeast(2));
            
            Assert.Equal(200, stats.ChangedCount);
        }
        
    }
}