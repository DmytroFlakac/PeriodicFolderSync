using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using PeriodicFolderSync.Core;
using PeriodicFolderSync.Interfaces;
using FolderSync.Tests.Mocks;
using Xunit;
using System.Security.Cryptography;

namespace FolderSync.Tests.UnitTests
{
    public class FileComparerTests : IDisposable
    {
        private readonly Mock<ILogger<IFileComparer>> _loggerMock;
        private readonly MockFileSystem _fileSystemMock;
        private readonly FileComparer _fileComparer;
        private readonly string _testDirectory;

        public FileComparerTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "FileComparerTests_" + Guid.NewGuid().ToString("N"));
            
            Directory.CreateDirectory(_testDirectory);
            
            _loggerMock = new Mock<ILogger<IFileComparer>>();
            _fileSystemMock = new MockFileSystem();
            
            _fileSystemMock.CreateDirectory(_testDirectory);
            
            _fileComparer = new FileComparer(_loggerMock.Object, _fileSystemMock);
        }

        [Fact]
        public async Task AreFilesIdentical_SameFilePath_ReturnsTrue()
        {
            // Arrange
            string filePath = Path.Combine(_testDirectory, "file.txt");
            await _fileSystemMock.WriteAllTextAsync(filePath, "Test content");

            // Act
            var result = await _fileComparer.AreFilesIdenticalAsync(filePath, filePath);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task AreFilesIdentical_FileDoesNotExist_ReturnsFalse()
        {
            string file1Path = Path.Combine(_testDirectory, "file1.txt");
            string file2Path = Path.Combine(_testDirectory, "file2.txt");

            await _fileSystemMock.WriteAllTextAsync(file1Path, "Test content");
            
            var result = await _fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);
            
            Assert.False(result);
        }

        [Fact]
        public async Task AreFilesIdentical_DifferentFileSize_ReturnsFalse()
        {
            // Arrange
            string file1Path = Path.Combine(_testDirectory, "file1.txt");
            string file2Path = Path.Combine(_testDirectory, "file2.txt");

            await _fileSystemMock.WriteAllTextAsync(file1Path, "Short content");
            await _fileSystemMock.WriteAllTextAsync(file2Path, "This is a much longer content to ensure different file size");

            // Act
            var result = await _fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task AreFilesIdentical_SameContent_ReturnsTrue()
        {
            // Arrange
            string file1Path = Path.Combine(_testDirectory, "file1.txt");
            string file2Path = Path.Combine(_testDirectory, "file2.txt");
            string sameContent = "Same content for both files";

            await _fileSystemMock.WriteAllTextAsync(file1Path, sameContent);
            await _fileSystemMock.WriteAllTextAsync(file2Path, sameContent);

            // Act
            var result = await _fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task AreFilesIdentical_SameTimestamp_ReturnsTrue()
        {
            string file1Path = Path.Combine(_testDirectory, "file1.txt");
            string file2Path = Path.Combine(_testDirectory, "file2.txt");
            
            await _fileSystemMock.WriteAllTextAsync(file1Path, "Content for file 1");
            await _fileSystemMock.WriteAllTextAsync(file2Path, "Content for file 2");
            
            
            var timestamp = DateTime.UtcNow;
            
            
            _fileSystemMock.SetLastWriteTimeUtc(file1Path, timestamp);
            _fileSystemMock.SetLastWriteTimeUtc(file2Path, timestamp);

            var result = await _fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);

            Assert.True(result);
        }

        [Fact]
        public async Task AreFilesIdentical_DifferentContent_SameSize_DifferentTimestamp_ReturnsFalse()
        {
            string file1Path = Path.Combine(_testDirectory, "file1.txt");
            string file2Path = Path.Combine(_testDirectory, "file2.txt");

            string content1 = "Content for file 1 test";
            string content2 = "Different content file";
            
            while (content1.Length != content2.Length)
            {
                if (content1.Length < content2.Length)
                    content1 += " ";
                else
                    content2 += " ";
            }

            await _fileSystemMock.WriteAllTextAsync(file1Path, content1);
            await _fileSystemMock.WriteAllTextAsync(file2Path, content2);
            
            _fileSystemMock.SetLastWriteTimeUtc(file1Path, DateTime.UtcNow.AddHours(-1));
            _fileSystemMock.SetLastWriteTimeUtc(file2Path, DateTime.UtcNow);

            var result = await _fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);

            Assert.False(result);
        }

        [Fact]
        public async Task CalculateFileHash_ValidFile_ReturnsCorrectHash()
        {
            // Arrange
            string filePath = Path.Combine(_testDirectory, "hashtest.txt");
            string content = "Test content for hash calculation";
            
            // Create the file in both the mock file system
            await _fileSystemMock.WriteAllTextAsync(filePath, content);

            // Calculate expected hash
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            using var md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(contentBytes);
            string expectedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            // Act
            var actualHash = await _fileComparer.CalculateFileHashAsync(filePath);

            // Assert
            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public async Task CompareFileContents_LargeIdenticalFiles_ReturnsTrue()
        {
            // Arrange
            string file1Path = Path.Combine(_testDirectory, "large1.bin");
            string file2Path = Path.Combine(_testDirectory, "large2.bin");

            // Create two identical large files (larger than the buffer size)
            const int size = 10000; // Larger than typical buffer size
            var random = new Random(42); // Use same seed for reproducibility
            byte[] data = new byte[size];
            random.NextBytes(data);

            await _fileSystemMock.WriteAllBytesAsync(file1Path, data);
            await _fileSystemMock.WriteAllBytesAsync(file2Path, data);
            
            // Set different timestamps to force content comparison
            _fileSystemMock.SetLastWriteTimeUtc(file1Path, DateTime.UtcNow.AddHours(-1));
            _fileSystemMock.SetLastWriteTimeUtc(file2Path, DateTime.UtcNow);

            // Act
            var result = await _fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);

            // Assert
            Assert.True(result);
        }

        
        [Fact]
        public async Task AreFilesIdentical_ExceptionThrown_ReturnsFalse()
        {
            string file1Path = Path.Combine(_testDirectory, "file1.txt");
            string file2Path = Path.Combine(_testDirectory, "file2.txt");

            var mockFileSystem = new Mock<IFileSystem>();
            mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Throws(new IOException("Simulated IO error"));
            
            var fileComparer = new FileComparer((ILogger<IFileComparer>)_loggerMock.Object, mockFileSystem.Object);

            var result = await fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);

            Assert.False(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore any exceptions
                }
            }
        }
    }
}