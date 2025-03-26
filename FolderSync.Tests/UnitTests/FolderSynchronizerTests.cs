using FolderSync.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Moq;
using PeriodicFolderSync.Core;
using PeriodicFolderSync.Interfaces;
using PeriodicFolderSync.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace FolderSync.Tests.UnitTests
{
    public class FolderSynchronizerTests
    {
        private readonly MockFileSystem _fileSystem;
        private readonly Mock<ILogger<IFolderSynchronizer>> _loggerMock;
        private readonly Mock<IMatchStrategy> _matchStrategyMock;
        private readonly FolderSynchronizer _synchronizer;

        public FolderSynchronizerTests()
        {
            _fileSystem = new MockFileSystem();
            _loggerMock = new Mock<ILogger<IFolderSynchronizer>>();
            _matchStrategyMock = new Mock<IMatchStrategy>();
            Mock<ILogger<IFolderOperator>> loggerFolderOperator = new();
            var folderOperator = new FolderOperator(loggerFolderOperator.Object, _fileSystem, 3, TimeSpan.FromSeconds(1));
            _synchronizer = new FolderSynchronizer(
                folderOperator,
                _fileSystem,
                _matchStrategyMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task SynchronizeFolders_CreatesDestinationFolderStructure_WhenSourceHasFolders()
        {
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceSubFolder = Path.Combine(source, "SubFolder");
            string destSubFolder = Path.Combine(destination, "SubFolder");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(sourceSubFolder);
            _fileSystem.CreateDirectory(destination);

            await _fileSystem.WriteAllTextAsync(Path.Combine(sourceSubFolder, "test.txt"), "test content");

            var stats = new SyncStatistics();

            await _synchronizer.SynchronizeFoldersAsync(source, destination, stats);

            Assert.True(_fileSystem.DirectoryExists(destSubFolder));
            Assert.True(_fileSystem.FileExists(Path.Combine(destSubFolder, "test.txt")));
            Assert.Equal(1, stats.ChangedCount);
            Assert.Equal(1, stats.FoldersChangedCount);
        }

        [Fact]
        public async Task SynchronizeFolders_DeletesExtraDestinationFolders_WhenNotInSource()
        {
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string destExtraFolder = Path.Combine(destination, "ExtraFolder");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(destination);
            _fileSystem.CreateDirectory(destExtraFolder);

            var stats = new SyncStatistics();

            await _synchronizer.SynchronizeFoldersAsync(source, destination, stats);

            Assert.False(_fileSystem.DirectoryExists(destExtraFolder));
            Assert.Equal(1, stats.DeletedFolders);
        }

        [Fact]
        public async Task SynchronizeFolders_MovesFolders_WhenMatchFound()
        {
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFolder = Path.Combine(source, "CorrectName");
            string destFolder = Path.Combine(destination, "OldName");
            string expectedDestFolder = Path.Combine(destination, "CorrectName");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(sourceFolder);
            _fileSystem.CreateDirectory(destination);
            _fileSystem.CreateDirectory(destFolder);

            await _fileSystem.WriteAllTextAsync(Path.Combine(sourceFolder, "test.txt"), "test content");
            await _fileSystem.WriteAllTextAsync(Path.Combine(destFolder, "test.txt"), "test content");

            _matchStrategyMock
                .Setup(m => m.IsFolderMatchAsync(sourceFolder, destFolder, _fileSystem, source, destination))
                .ReturnsAsync(true);

            var stats = new SyncStatistics();

            await _synchronizer.SynchronizeFoldersAsync(source, destination, stats);

            Assert.False(_fileSystem.DirectoryExists(destFolder));
            Assert.True(_fileSystem.DirectoryExists(expectedDestFolder));
            Assert.Equal(1, stats.FoldersMovedCount);
            Assert.Equal(1, stats.FilesInMovedFolders);
        }

        [Fact]
        public async Task SynchronizeFolders_HandlesNestedFolderStructure()
        {
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceLevel1 = Path.Combine(source, "Level1");
            string sourceLevel2 = Path.Combine(sourceLevel1, "Level2");
            string sourceLevel3 = Path.Combine(sourceLevel2, "Level3");

            string destLevel1 = Path.Combine(destination, "Level1");
            string destLevel2 = Path.Combine(destLevel1, "Level2");
            string destLevel3 = Path.Combine(destLevel2, "Level3");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(sourceLevel1);
            _fileSystem.CreateDirectory(sourceLevel2);
            _fileSystem.CreateDirectory(sourceLevel3);
            _fileSystem.CreateDirectory(destination);

            await _fileSystem.WriteAllTextAsync(Path.Combine(sourceLevel3, "deep.txt"), "deep content");

            var stats = new SyncStatistics();
            
            await _synchronizer.SynchronizeFoldersAsync(source, destination, stats);

            Assert.True(_fileSystem.DirectoryExists(destLevel1));
            Assert.True(_fileSystem.DirectoryExists(destLevel2));
            Assert.True(_fileSystem.DirectoryExists(destLevel3));
            Assert.True(_fileSystem.FileExists(Path.Combine(destLevel3, "deep.txt")));
        }

       
        [Fact]
        public async Task SynchronizeFolders_HandlesMovingFoldersWithSubfolders()
        {
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceParent = Path.Combine(source, "Parent");
            string sourceChild = Path.Combine(sourceParent, "Child");

            string destParentOld = Path.Combine(destination, "OldParent");
            string destChildOld = Path.Combine(destParentOld, "Child");

            string destParentExpected = Path.Combine(destination, "Parent");
            string destChildExpected = Path.Combine(destParentExpected, "Child");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(sourceParent);
            _fileSystem.CreateDirectory(sourceChild);
            _fileSystem.CreateDirectory(destination);
            _fileSystem.CreateDirectory(destParentOld);
            _fileSystem.CreateDirectory(destChildOld);

            await _fileSystem.WriteAllTextAsync(Path.Combine(sourceChild, "test.txt"), "test content");
            await _fileSystem.WriteAllTextAsync(Path.Combine(destChildOld, "test.txt"), "test content");

            _matchStrategyMock
                .Setup(m => m.IsFolderMatchAsync(sourceParent, destParentOld, _fileSystem, source, destination))
                .ReturnsAsync(true);

            var stats = new SyncStatistics();

            await _synchronizer.SynchronizeFoldersAsync(source, destination, stats);

            Assert.False(_fileSystem.DirectoryExists(destParentOld));
            Assert.False(_fileSystem.DirectoryExists(destChildOld));
            Assert.True(_fileSystem.DirectoryExists(destParentExpected));
            Assert.True(_fileSystem.DirectoryExists(destChildExpected));
            Assert.True(_fileSystem.FileExists(Path.Combine(destChildExpected, "test.txt")));
        }

        [Fact]
        public async Task SynchronizeFolders_HandlesExceptionsDuringFolderMove()
        {
            string source = @"C:\Source";
            string destination = @"C:\Destination";
            string sourceFolder = Path.Combine(source, "Folder");
            string destFolder = Path.Combine(destination, "OldFolder");
            string expectedDestFolder = Path.Combine(destination, "Folder");

            _fileSystem.CreateDirectory(source);
            _fileSystem.CreateDirectory(sourceFolder);
            _fileSystem.CreateDirectory(destination);
            _fileSystem.CreateDirectory(destFolder);

            await _fileSystem.WriteAllTextAsync(Path.Combine(sourceFolder, "test.txt"), "test content");

            _matchStrategyMock
                .Setup(m => m.IsFolderMatchAsync(sourceFolder, destFolder, _fileSystem, source, destination))
                .ReturnsAsync(true);

            var mockFolderOperator = new Mock<IFolderOperator>();
            mockFolderOperator
                .Setup(f => f.MoveFolderAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new IOException("Simulated move error"));

            mockFolderOperator
                .Setup(f => f.CopyFolderAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            mockFolderOperator
                .Setup(f => f.DeleteFolderAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(Task.CompletedTask);

            var synchronizer = new FolderSynchronizer(
                mockFolderOperator.Object,
                _fileSystem,
                _matchStrategyMock.Object,
                _loggerMock.Object
            );

            var stats = new SyncStatistics();

            await synchronizer.SynchronizeFoldersAsync(source, destination, stats);

            mockFolderOperator.Verify(f => f.MoveFolderAsync(destFolder, expectedDestFolder), Times.Once);
            mockFolderOperator.Verify(f => f.CopyFolderAsync(sourceFolder, expectedDestFolder), Times.Once);
        }
        
    }

}