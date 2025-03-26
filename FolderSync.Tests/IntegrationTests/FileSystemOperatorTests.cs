using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PeriodicFolderSync.Core;
using System.Text;
using PeriodicFolderSync.Interfaces;
using Xunit;

namespace FolderSync.Tests.IntegrationTests
{
    public class FileSystemOperatorTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _sourceDirectory;
        private readonly string _destinationDirectory;
        
        private readonly FileSystemOperator _fileSystemOperator;

        public FileSystemOperatorTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "FileSystemOperatorTests_" + Guid.NewGuid().ToString());
            _sourceDirectory = Path.Combine(_testDirectory, "Source");
            _destinationDirectory = Path.Combine(_testDirectory, "Destination");

            Directory.CreateDirectory(_sourceDirectory);
            Directory.CreateDirectory(_destinationDirectory);

            ILogger<FileSystemOperator> logger = new NullLogger<FileSystemOperator>();
            IFileComparer fileComparer = new FileComparer(new NullLogger<IFileComparer>());
            IFileSystem fileSystem = new FileSystem();
            _fileSystemOperator = new FileSystemOperator(logger, fileSystem, fileComparer);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
        }

        private string CreateTestFile(string directory, string fileName, string content = "Test content")
        {
            string filePath = Path.Combine(directory, fileName);
            File.WriteAllText(filePath, content);
            return filePath;
        }

        private string CreateTestDirectory(string parentDirectory, string directoryName)
        {
            string dirPath = Path.Combine(parentDirectory, directoryName);
            Directory.CreateDirectory(dirPath);
            return dirPath;
        }

        private async Task<bool> CompareFiles(string file1, string file2)
        {
            if (!File.Exists(file1) || !File.Exists(file2))
                return false;

            byte[] file1Bytes = await File.ReadAllBytesAsync(file1);
            byte[] file2Bytes = await File.ReadAllBytesAsync(file2);

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

            await _fileSystemOperator.CopyFileAsync(sourceFile, destFile);

            Assert.True(File.Exists(destFile));
            Assert.True(await CompareFiles(sourceFile, destFile));
        }

        [Fact]
        public async Task CopyFileAsync_ShouldThrowException_WhenSourceFileDoesNotExist()
        {
            string sourceFile = Path.Combine(_sourceDirectory, "nonexistent.txt");
            string destFile = Path.Combine(_destinationDirectory, "dest.txt");

            await Assert.ThrowsAsync<FileNotFoundException>(() => 
                _fileSystemOperator.CopyFileAsync(sourceFile, destFile));
        }

       
        [Fact]
        public async Task DeleteFileAsync_ShouldDeleteFile_WhenFileExists()
        {
            string filePath = CreateTestFile(_sourceDirectory, "toDelete.txt");

            await _fileSystemOperator.DeleteFileAsync(filePath);

            Assert.False(File.Exists(filePath));
        }

        [Fact]
        public async Task MoveFileAsync_ShouldMoveFile_WhenDestinationDoesNotExist()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "toMove.txt", "Move content");
            string destFile = Path.Combine(_destinationDirectory, "moved.txt");

            await _fileSystemOperator.MoveFileAsync(sourceFile, destFile);

            Assert.False(File.Exists(sourceFile));
            Assert.True(File.Exists(destFile));
            Assert.Equal("Move content", await File.ReadAllTextAsync(destFile));
        }

        [Fact]
        public async Task CopyFolderAsync_ShouldCopyFolder_WhenDestinationDoesNotExist()
        {
            string sourceSubDir = CreateTestDirectory(_sourceDirectory, "SubDir");
            string sourceFile1 = CreateTestFile(_sourceDirectory, "file1.txt", "Content 1");
            string sourceFile2 = CreateTestFile(sourceSubDir, "file2.txt", "Content 2");

            string destDir = Path.Combine(_destinationDirectory, "CopiedDir");

            await _fileSystemOperator.CopyFolderAsync(_sourceDirectory, destDir);

            Assert.True(Directory.Exists(destDir));
            Assert.True(Directory.Exists(Path.Combine(destDir, "SubDir")));
            Assert.True(File.Exists(Path.Combine(destDir, "file1.txt")));
            Assert.True(File.Exists(Path.Combine(destDir, "SubDir", "file2.txt")));
            
            Assert.Equal("Content 1", await File.ReadAllTextAsync(Path.Combine(destDir, "file1.txt")));
            Assert.Equal("Content 2", await File.ReadAllTextAsync(Path.Combine(destDir, "SubDir", "file2.txt")));
        }

        [Fact]
        public async Task DeleteFolderAsync_ShouldDeleteFolder_WhenFolderExists()
        {
            string dirToDelete = CreateTestDirectory(_sourceDirectory, "ToDelete");
            CreateTestFile(dirToDelete, "file.txt");

            await _fileSystemOperator.DeleteFolderAsync(dirToDelete);

            Assert.False(Directory.Exists(dirToDelete));
        }

        [Fact]
        public async Task MoveFolderAsync_ShouldMoveFolder_WhenDestinationDoesNotExist()
        {
            string sourceSubDir = CreateTestDirectory(_sourceDirectory, "ToMove");
            string sourceFile1 = CreateTestFile(sourceSubDir, "file1.txt", "Content 1");

            string destDir = Path.Combine(_destinationDirectory, "MovedDir");

            await _fileSystemOperator.MoveFolderAsync(sourceSubDir, destDir);

            Assert.False(Directory.Exists(sourceSubDir));
            Assert.True(Directory.Exists(destDir));
            Assert.True(File.Exists(Path.Combine(destDir, "file1.txt")));
            Assert.Equal("Content 1", await File.ReadAllTextAsync(Path.Combine(destDir, "file1.txt")));
        }
        
        [Fact]
        public async Task WithRetryAsync_ShouldRetryOperation_WhenIOExceptionOccurs()
        {
            string filePath = CreateTestFile(_sourceDirectory, "locked.txt", "Locked content");
            string destPath = Path.Combine(_destinationDirectory, "locked_copy.txt");

            await using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            await Assert.ThrowsAsync<IOException>(() =>
                _fileSystemOperator.CopyFileAsync(filePath, destPath));
                
            Assert.False(File.Exists(destPath));
        }

        [Fact]
        public async Task ValidatePaths_ShouldThrowArgumentException_WhenSourceIsNull()
        {
            string? nullSource = null;
            string destFile = Path.Combine(_destinationDirectory, "dest.txt");

            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _fileSystemOperator.CopyFileAsync(nullSource!, destFile));
                
            Assert.Contains("Source or destination path cannot be null or empty", exception.Message);
        }

        [Fact]
        public async Task ValidatePaths_ShouldThrowArgumentException_WhenDestinationIsEmpty()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "source.txt");
            string emptyDest = "";

            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _fileSystemOperator.CopyFileAsync(sourceFile, emptyDest));
                
            Assert.Contains("Source or destination path cannot be null or empty", exception.Message);
        }

        [Fact]
        public async Task ValidatePath_ShouldThrowArgumentException_WhenPathIsNull()
        {
            string? nullPath = null;

            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _fileSystemOperator.DeleteFileAsync(nullPath!));
                
            Assert.Contains("Path cannot be null or empty", exception.Message);
        }
        

        [Fact]
        public async Task ReadAllBytesAsync_ShouldReturnCorrectBytes()
        {
            string content = "Test binary content";
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            string filePath = Path.Combine(_sourceDirectory, "binary.dat");
            await File.WriteAllBytesAsync(filePath, contentBytes);
        
            byte[] readBytes = await File.ReadAllBytesAsync(filePath);
        
            Assert.Equal(contentBytes.Length, readBytes.Length);
            for (int i = 0; i < contentBytes.Length; i++)
            {
                Assert.Equal(contentBytes[i], readBytes[i]);
            }
        }

        [Fact]
        public async Task ReadAllTextAsync_ShouldReturnCorrectText()
        {
            string content = "Test text content with some unicode: 你好, 世界!";
            string filePath = Path.Combine(_sourceDirectory, "text.txt");
            await File.WriteAllTextAsync(filePath, content);
        
            string readText = await File.ReadAllTextAsync(filePath);
        
            Assert.Equal(content, readText);
        }

        [Fact]
        public async Task WriteAllBytesAsync_ShouldWriteCorrectBytes()
        {
            byte[] contentBytes = "Hello"u8.ToArray(); 
            string filePath = Path.Combine(_sourceDirectory, "written_binary.dat");
        
            await File.WriteAllBytesAsync(filePath, contentBytes);
        
            Assert.True(File.Exists(filePath));
            byte[] readBytes = await File.ReadAllBytesAsync(filePath);
            Assert.Equal(contentBytes, readBytes);
        }

        [Fact]
        public async Task WriteAllTextAsync_ShouldWriteCorrectText()
        {
            string content = "Text to write with unicode: 你好, 世界!";
            string filePath = Path.Combine(_sourceDirectory, "written_text.txt");
        
            await File.WriteAllTextAsync(filePath, content);
        
            Assert.True(File.Exists(filePath));
            string readText = await File.ReadAllTextAsync(filePath);
            Assert.Equal(content, readText);
        }

        [Fact]
        public void GetFileSize_ShouldReturnCorrectSize()
        {
            byte[] data = new byte[1024];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 256);
            }
            string filePath = Path.Combine(_sourceDirectory, "sized_file.dat");
            File.WriteAllBytes(filePath, data);
        
            long size = new FileInfo(filePath).Length;
        
            Assert.Equal(1024, size);
        }
    }
}