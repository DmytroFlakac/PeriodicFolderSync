using Microsoft.Extensions.Logging;
using Xunit;
using PeriodicFolderSync.Core;
using PeriodicFolderSync.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Principal;

namespace FolderSync.Tests.IntegrationTests
{
    public class SynchronizerTests : IDisposable
    {
        private readonly string _sourceDir;
        private readonly string _destDir;
        private readonly Synchronizer _synchronizer;
        private readonly IFileSystem _fileSystem;

        public SynchronizerTests()
        {
            string testRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");
            Directory.CreateDirectory(testRoot);
            
            _sourceDir = Path.Combine(testRoot, "Source_" + Guid.NewGuid().ToString());
            _destDir = Path.Combine(testRoot, "Dest_" + Guid.NewGuid().ToString());
            
            Directory.CreateDirectory(_sourceDir);
            Directory.CreateDirectory(_destDir);

            _fileSystem = new FileSystem();
            
            var fileComparer = new FileComparer(NullLogger<IFileComparer>.Instance, _fileSystem);
            var fileOperator = new FileOperator(NullLogger<IFileOperator>.Instance, _fileSystem, fileComparer);
            var folderOperator = new FolderOperator(NullLogger<IFolderOperator>.Instance, _fileSystem);
            var matchStrategy = new ContentBasedMatchStrategy(NullLogger<IMatchStrategy>.Instance, fileComparer);
            
            var fileSynchronizer = new FileSynchronizer(
                fileOperator,
                _fileSystem,
                matchStrategy,
                NullLogger<IFileSynchronizer>.Instance);
                
            var folderSynchronizer = new FolderSynchronizer(
                folderOperator,
                _fileSystem,
                matchStrategy,
                NullLogger<IFolderSynchronizer>.Instance);

            _synchronizer = new Synchronizer(
                folderSynchronizer,
                fileSynchronizer,
                NullLogger<ISynchronizer>.Instance);
        }

        [Fact]
        public async Task SynchronizeAsync_EmptyDirectories_NoChanges()
        {
            await _synchronizer.SynchronizeAsync(_sourceDir, _destDir);

            Assert.Empty(Directory.GetFiles(_destDir, "*", SearchOption.AllDirectories));
            Assert.Empty(Directory.GetDirectories(_destDir, "*", SearchOption.AllDirectories));
        }

        [Fact]
        public async Task SynchronizeAsync_WithFiles_CopiesAllFiles()
        {
            string sourceFile1 = Path.Combine(_sourceDir, "file1.txt");
            string sourceFile2 = Path.Combine(_sourceDir, "file2.txt");
            
            await File.WriteAllTextAsync(sourceFile1, "Test content 1");
            await File.WriteAllTextAsync(sourceFile2, "Test content 2");

            await _synchronizer.SynchronizeAsync(_sourceDir, _destDir);

            string destFile1 = Path.Combine(_destDir, "file1.txt");
            string destFile2 = Path.Combine(_destDir, "file2.txt");
            
            Assert.True(File.Exists(destFile1));
            Assert.True(File.Exists(destFile2));
            Assert.Equal("Test content 1", await File.ReadAllTextAsync(destFile1));
            Assert.Equal("Test content 2", await File.ReadAllTextAsync(destFile2));
        }

        [Fact]
        public async Task SynchronizeAsync_WithSubfolders_CopiesAllFoldersAndFiles()
        {
            string sourceSubDir = Path.Combine(_sourceDir, "subdir");
            Directory.CreateDirectory(sourceSubDir);
            
            string sourceFile1 = Path.Combine(_sourceDir, "file1.txt");
            string sourceFile2 = Path.Combine(sourceSubDir, "file2.txt");
            
            await File.WriteAllTextAsync(sourceFile1, "Test content 1");
            await File.WriteAllTextAsync(sourceFile2, "Test content 2");

            await _synchronizer.SynchronizeAsync(_sourceDir, _destDir);

            string destSubDir = Path.Combine(_destDir, "subdir");
            string destFile1 = Path.Combine(_destDir, "file1.txt");
            string destFile2 = Path.Combine(destSubDir, "file2.txt");
            
            Assert.True(Directory.Exists(destSubDir));
            Assert.True(File.Exists(destFile1));
            Assert.True(File.Exists(destFile2));
            Assert.Equal("Test content 1", await File.ReadAllTextAsync(destFile1));
            Assert.Equal("Test content 2", await File.ReadAllTextAsync(destFile2));
        }

        [Fact]
        public async Task SynchronizeAsync_ModifiedFiles_UpdatesFiles()
        {
            // Arrange
            string sourceFile = Path.Combine(_sourceDir, "file.txt");
            string destFile = Path.Combine(_destDir, "file.txt");
            
            await File.WriteAllTextAsync(sourceFile, "Initial content");
            await File.WriteAllTextAsync(destFile, "Old content");

            await _synchronizer.SynchronizeAsync(_sourceDir, _destDir);

            Assert.True(File.Exists(destFile));
            Assert.Equal("Initial content", await File.ReadAllTextAsync(destFile));
        }

        [Fact]
        public async Task SynchronizeAsync_ExtraFilesInDestination_DeletesFiles()
        {
            string sourceFile = Path.Combine(_sourceDir, "file.txt");
            string destFile = Path.Combine(_destDir, "file.txt");
            string extraDestFile = Path.Combine(_destDir, "extra.txt");
            
            await File.WriteAllTextAsync(sourceFile, "Test content");
            await File.WriteAllTextAsync(destFile, "Old content");
            await File.WriteAllTextAsync(extraDestFile, "Extra content");
            
            await _synchronizer.SynchronizeAsync(_sourceDir, _destDir);

            Assert.True(File.Exists(destFile));
            Assert.False(File.Exists(extraDestFile));
            Assert.Equal("Test content", await File.ReadAllTextAsync(destFile));
        }

        [Fact]
        public async Task SynchronizeAsync_ExtraFoldersInDestination_DeletesFolders()
        {
            string extraDestDir = Path.Combine(_destDir, "extra");
            Directory.CreateDirectory(extraDestDir);
            await File.WriteAllTextAsync(Path.Combine(extraDestDir, "extra.txt"), "Extra content");

            await _synchronizer.SynchronizeAsync(_sourceDir, _destDir);

            Assert.False(Directory.Exists(extraDestDir));
        }

        [Fact]
        public async Task SynchronizeAsync_MovedFiles_DetectsAndMovesFiles()
        {
            string sourceSubDir = Path.Combine(_sourceDir, "subdir1");
            string destSubDir = Path.Combine(_destDir, "subdir2");
            Directory.CreateDirectory(sourceSubDir);
            Directory.CreateDirectory(destSubDir);
            
            string sourceFile = Path.Combine(sourceSubDir, "file.txt");
            string destFile = Path.Combine(destSubDir, "file.txt");
            
            string content = "Test content for moved file";
            await File.WriteAllTextAsync(sourceFile, content);
            await File.WriteAllTextAsync(destFile, content);

            await _synchronizer.SynchronizeAsync(_sourceDir, _destDir);

            string expectedDestFile = Path.Combine(_destDir, "subdir1", "file.txt");
            Assert.True(File.Exists(expectedDestFile));
            Assert.False(File.Exists(destFile)); // Original file should be moved
            Assert.Equal(content, await File.ReadAllTextAsync(expectedDestFile));
        }

        public void Dispose()
        {
            if (Directory.Exists(_sourceDir))
                Directory.Delete(_sourceDir, true);
            
            if (Directory.Exists(_destDir))
                Directory.Delete(_destDir, true);
        }
    }
    
}