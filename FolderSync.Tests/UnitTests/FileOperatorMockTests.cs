using Microsoft.Extensions.Logging;
using Moq;
using PeriodicFolderSync.Core;
using FolderSync.Tests.Mocks;
using PeriodicFolderSync.Interfaces;
using Xunit;

namespace FolderSync.Tests.UnitTests
{
    public class FileOperatorMockTests
    {
        private readonly string _sourceDirectory;
        private readonly string _destinationDirectory;
        private readonly FileOperator _fileOperator;
        private readonly MockFileSystem _mockFileSystem;

        public FileOperatorMockTests()
        {
            var testDirectory = @"D:\TestDirectory";
            _sourceDirectory = Path.Combine(testDirectory, "Source");
            _destinationDirectory = Path.Combine(testDirectory, "Destination");

            _mockFileSystem = new MockFileSystem();
            _mockFileSystem.CreateDirectory(_sourceDirectory);
            _mockFileSystem.CreateDirectory(_destinationDirectory);

            Mock<ILogger<IFileOperator>> loggerMock = new();
            Mock<IFileComparer> fileComparerMock = new();
            fileComparerMock.Setup(fc => fc.AreFilesIdenticalAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>(async (file1, file2) => await CompareFiles(file1, file2));
            
            _fileOperator = new FileOperator(loggerMock.Object, _mockFileSystem, fileComparerMock.Object);
        }

        private string CreateTestFile(string directory, string fileName, string content = "Test content")
        {
            string filePath = Path.Combine(directory, fileName);
            _mockFileSystem.WriteAllTextAsync(filePath, content).Wait();
            return filePath;
        }

        private async Task<bool> CompareFiles(string file1, string file2)
        {
            if (!_mockFileSystem.FileExists(file1) || !_mockFileSystem.FileExists(file2))
                return false;

            byte[] file1Bytes = await _mockFileSystem.ReadAllBytesAsync(file1);
            byte[] file2Bytes = await _mockFileSystem.ReadAllBytesAsync(file2);

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

            Assert.True(_mockFileSystem.FileExists(destFile));
            Assert.True(await CompareFiles(sourceFile, destFile));
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
        public async Task CopyFileAsync_ShouldCreateDestinationDirectory_WhenItDoesNotExist()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "source.txt");
            string newDestDir = Path.Combine(_destinationDirectory, "NewDir");
            string destFile = Path.Combine(newDestDir, "dest.txt");

            await _fileOperator.CopyFileAsync(sourceFile, destFile);

            Assert.True(_mockFileSystem.DirectoryExists(newDestDir));
            Assert.True(_mockFileSystem.FileExists(destFile));
        }

        [Fact]
        public async Task DeleteFileAsync_ShouldDeleteFile_WhenFileExists()
        {
            string filePath = CreateTestFile(_sourceDirectory, "toDelete.txt");

            await _fileOperator.DeleteFileAsync(filePath);

            Assert.False(_mockFileSystem.FileExists(filePath));
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

            Assert.False(_mockFileSystem.FileExists(sourceFile));
            Assert.True(_mockFileSystem.FileExists(destFile));
            Assert.Equal("Move content", await _mockFileSystem.ReadAllTextAsync(destFile));
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
        public async Task CopyFileAsync_ShouldHandleLargeFiles()
        {
            string sourceFile = Path.Combine(_sourceDirectory, "large.bin");
            string destFile = Path.Combine(_destinationDirectory, "large_copy.bin");
            
            byte[] largeContent = new byte[5 * 1024 * 1024]; 
            new Random().NextBytes(largeContent);
            await _mockFileSystem.WriteAllBytesAsync(sourceFile, largeContent);

            await _fileOperator.CopyFileAsync(sourceFile, destFile);

            Assert.True(_mockFileSystem.FileExists(destFile));
            Assert.Equal(_mockFileSystem.GetFileInfo(sourceFile).Length, _mockFileSystem.GetFileInfo(destFile).Length);
            Assert.True(await CompareFiles(sourceFile, destFile));
        }
    }
}