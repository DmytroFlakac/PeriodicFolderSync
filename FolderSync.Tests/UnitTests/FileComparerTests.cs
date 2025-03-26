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
            string filePath = Path.Combine(_testDirectory, "file.txt");
            await _fileSystemMock.WriteAllTextAsync(filePath, "Test content");

            var result = await _fileComparer.AreFilesIdenticalAsync(filePath, filePath);

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
            string file1Path = Path.Combine(_testDirectory, "file1.txt");
            string file2Path = Path.Combine(_testDirectory, "file2.txt");

            await _fileSystemMock.WriteAllTextAsync(file1Path, "Short content");
            await _fileSystemMock.WriteAllTextAsync(file2Path, "This is a much longer content to ensure different file size");

            var result = await _fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);

            Assert.False(result);
        }

        [Fact]
        public async Task AreFilesIdentical_SameContent_ReturnsTrue()
        {
            string file1Path = Path.Combine(_testDirectory, "file1.txt");
            string file2Path = Path.Combine(_testDirectory, "file2.txt");
            string sameContent = "Same content for both files";

            await _fileSystemMock.WriteAllTextAsync(file1Path, sameContent);
            await _fileSystemMock.WriteAllTextAsync(file2Path, sameContent);

            var result = await _fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);

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
            string filePath = Path.Combine(_testDirectory, "hashtest.txt");
            string content = "Test content for hash calculation";
            
            await _fileSystemMock.WriteAllTextAsync(filePath, content);

            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            using var md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(contentBytes);
            string expectedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            var actualHash = await _fileComparer.CalculateFileHashAsync(filePath);

            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public async Task CompareFileContents_LargeIdenticalFiles_ReturnsTrue()
        {
            string file1Path = Path.Combine(_testDirectory, "large1.bin");
            string file2Path = Path.Combine(_testDirectory, "large2.bin");

            const int size = 10000; 
            var random = new Random(42); 
            byte[] data = new byte[size];
            random.NextBytes(data);

            await _fileSystemMock.WriteAllBytesAsync(file1Path, data);
            await _fileSystemMock.WriteAllBytesAsync(file2Path, data);
            
            _fileSystemMock.SetLastWriteTimeUtc(file1Path, DateTime.UtcNow.AddHours(-1));
            _fileSystemMock.SetLastWriteTimeUtc(file2Path, DateTime.UtcNow);

            var result = await _fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);
            
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

        [Fact]
        public async Task CompareFileContents_DifferentContentAtSpecificPosition_ReturnsFalse()
        {
            string file1Path = Path.Combine(_testDirectory, "diff1.bin");
            string file2Path = Path.Combine(_testDirectory, "diff2.bin");

            const int size = 8192; 
            byte[] data1 = new byte[size];
            byte[] data2 = new byte[size];
            
            var random = new Random(42);
            random.NextBytes(data1);
            Array.Copy(data1, data2, data1.Length);
            
            data2[5000] = (byte)(data1[5000] + 1);

            await _fileSystemMock.WriteAllBytesAsync(file1Path, data1);
            await _fileSystemMock.WriteAllBytesAsync(file2Path, data2);
            
            _fileSystemMock.SetLastWriteTimeUtc(file1Path, DateTime.UtcNow.AddHours(-1));
            _fileSystemMock.SetLastWriteTimeUtc(file2Path, DateTime.UtcNow);

            var result = await _fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);

            Assert.False(result);
        }

        [Fact]
        public async Task CompareFileContents_DifferentContentAtBufferBoundary_ReturnsFalse()
        {
            string file1Path = Path.Combine(_testDirectory, "boundary1.bin");
            string file2Path = Path.Combine(_testDirectory, "boundary2.bin");

            
            const int size = 8192;
            byte[] data1 = new byte[size];
            byte[] data2 = new byte[size];
            
            var random = new Random(42);
            random.NextBytes(data1);
            Array.Copy(data1, data2, data1.Length);
            
            data2[4096] = (byte)(data1[4096] + 1);

            await _fileSystemMock.WriteAllBytesAsync(file1Path, data1);
            await _fileSystemMock.WriteAllBytesAsync(file2Path, data2);
            
            _fileSystemMock.SetLastWriteTimeUtc(file1Path, DateTime.UtcNow.AddHours(-1));
            _fileSystemMock.SetLastWriteTimeUtc(file2Path, DateTime.UtcNow);
            
            var result = await _fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);

            Assert.False(result);
        }

        
        [Fact]
        public async Task CompareFileContents_VeryLargeFiles_HandlesCorrectly()
        {
            string file1Path = Path.Combine(_testDirectory, "large1.bin");
            string file2Path = Path.Combine(_testDirectory, "large2.bin");

            const int size = 100_000; 
            byte[] data = new byte[size];
            for (int i = 0; i < size; i++)
            {
                data[i] = (byte)(i % 256);
            }

            await _fileSystemMock.WriteAllBytesAsync(file1Path, data);
            await _fileSystemMock.WriteAllBytesAsync(file2Path, data);
            
            _fileSystemMock.SetLastWriteTimeUtc(file1Path, DateTime.UtcNow.AddHours(-1));
            _fileSystemMock.SetLastWriteTimeUtc(file2Path, DateTime.UtcNow);

            var result = await _fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);

            Assert.True(result);
        }

        [Fact]
        public async Task CompareFileContents_ErrorReadingFile_ReturnsFalse()
        {
            string file1Path = Path.Combine(_testDirectory, "error1.txt");
            string file2Path = Path.Combine(_testDirectory, "error2.txt");

            string content = new string('A', 100);
            
            var mockFileSystem = new Mock<IFileSystem>();
            
            mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
            
            var file1Info = new FileInfo(file1Path);
            var file2Info = new FileInfo(file2Path);
            
            mockFileSystem.Setup(fs => fs.GetFileInfo(file1Path)).Returns(file1Info);
            mockFileSystem.Setup(fs => fs.GetFileInfo(file2Path)).Returns(file2Info);
            
            mockFileSystem.Setup(fs => fs.ReadAllBytesAsync(It.IsAny<string>()))
                .ThrowsAsync(new IOException("Simulated error reading file"));
            
            var fileComparer = new FileComparer(_loggerMock.Object, mockFileSystem.Object);

            var result = await fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);

            Assert.False(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error comparing")),
                    It.IsAny<IOException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task AreFilesIdentical_SameSize_DifferentTimestamp_ErrorReadingContent_ReturnsFalse()
        {
            string file1Path = Path.Combine(_testDirectory, "error1.txt");
            string file2Path = Path.Combine(_testDirectory, "error2.txt");

            string content = new string('A', 100);
            
            var mockFileSystem = new Mock<IFileSystem>();
            
            mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
            
            var file1Info = new FileInfo(file1Path);
            var file2Info = new FileInfo(file2Path);
            
            mockFileSystem.Setup(fs => fs.GetFileInfo(file1Path)).Returns(file1Info);
            mockFileSystem.Setup(fs => fs.GetFileInfo(file2Path)).Returns(file2Info);
            
            mockFileSystem.Setup(fs => fs.ReadAllBytesAsync(It.IsAny<string>()))
                .ThrowsAsync(new IOException("Simulated error reading file"));
            
            var fileComparer = new FileComparer(_loggerMock.Object, mockFileSystem.Object);

            var result = await fileComparer.AreFilesIdenticalAsync(file1Path, file2Path);

            Assert.False(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error comparing")),
                    It.IsAny<IOException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
        

        [Fact]
        public async Task CompareFileContents_DirectTest_DifferentContent_UsingReflection()
        {
            string file1Path = Path.Combine(_testDirectory, "reflect_diff1.txt");
            string file2Path = Path.Combine(_testDirectory, "reflect_diff2.txt");
            
            await _fileSystemMock.WriteAllTextAsync(file1Path, "Content for file 1");
            await _fileSystemMock.WriteAllTextAsync(file2Path, "Different content for file 2");
            
            var method = typeof(FileComparer).GetMethod("CompareFileContentsAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var result = await (Task<bool>)method.Invoke(_fileComparer, new object[] { file1Path, file2Path });
            
            Assert.False(result);
        }

        [Fact]
        public async Task CompareFileContents_DirectTest_EmptyFiles_UsingReflection()
        {
            string file1Path = Path.Combine(_testDirectory, "reflect_empty1.txt");
            string file2Path = Path.Combine(_testDirectory, "reflect_empty2.txt");
            
            await _fileSystemMock.WriteAllTextAsync(file1Path, "");
            await _fileSystemMock.WriteAllTextAsync(file2Path, "");
            
            var method = typeof(FileComparer).GetMethod("CompareFileContentsAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var result = await (Task<bool>)method.Invoke(_fileComparer, new object[] { file1Path, file2Path });
            
            Assert.True(result);
        }

        [Fact]
        public async Task CompareFileContents_DirectTest_LargeFiles_UsingReflection()
        {
            string file1Path = Path.Combine(_testDirectory, "reflect_large1.bin");
            string file2Path = Path.Combine(_testDirectory, "reflect_large2.bin");
            
            const int size = 20000; 
            byte[] data = new byte[size];
            for (int i = 0; i < size; i++)
            {
                data[i] = (byte)(i % 256);
            }
            
            await _fileSystemMock.WriteAllBytesAsync(file1Path, data);
            await _fileSystemMock.WriteAllBytesAsync(file2Path, data);
            
            var method = typeof(FileComparer).GetMethod("CompareFileContentsAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var result = await (Task<bool>)method.Invoke(_fileComparer, new object[] { file1Path, file2Path });
            
            Assert.True(result);
        }

        [Fact]
        public async Task CompareFileContents_DirectTest_ExceptionHandling_UsingReflection()
        {
            string file1Path = Path.Combine(_testDirectory, "reflect_error1.txt");
            string file2Path = Path.Combine(_testDirectory, "reflect_error2.txt");
            
            var mockFileSystem = new Mock<IFileSystem>();
            mockFileSystem.Setup(fs => fs.ReadAllBytesAsync(It.IsAny<string>()))
                .ThrowsAsync(new IOException("Simulated error reading file"));
            
            var fileComparer = new FileComparer(_loggerMock.Object, mockFileSystem.Object);
            
            var method = typeof(FileComparer).GetMethod("CompareFileContentsAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var result = await (Task<bool>)method.Invoke(fileComparer, new object[] { file1Path, file2Path });
            
            Assert.False(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error comparing file contents")),
                    It.IsAny<IOException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CompareFileContents_DirectTest_MultipleBufferBoundaries_UsingReflection()
        {
            string file1Path = Path.Combine(_testDirectory, "reflect_multibuffer1.bin");
            string file2Path = Path.Combine(_testDirectory, "reflect_multibuffer2.bin");
            
            
            const int size = 12288;
            byte[] data1 = new byte[size];
            byte[] data2 = new byte[size];
            
            for (int i = 0; i < size; i++)
            {
                data1[i] = (byte)(i % 256);
                data2[i] = (byte)(i % 256);
            }
            
            await _fileSystemMock.WriteAllBytesAsync(file1Path, data1);
            await _fileSystemMock.WriteAllBytesAsync(file2Path, data2);
            
            var method = typeof(FileComparer).GetMethod("CompareFileContentsAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var result = await (Task<bool>)method.Invoke(_fileComparer, new object[] { file1Path, file2Path });
            
            Assert.True(result);
        }
        
        [Fact]
        public async Task CompareFileContents_DirectTest_DifferentSizes_UsingReflection()
        {
            string file1Path = Path.Combine(_testDirectory, "reflect_size1.bin");
            string file2Path = Path.Combine(_testDirectory, "reflect_size2.bin");
            
            byte[] data1 = new byte[100];
            byte[] data2 = new byte[200];
            
            for (int i = 0; i < data1.Length; i++)
                data1[i] = (byte)(i % 256);
                
            for (int i = 0; i < data2.Length; i++)
                data2[i] = (byte)(i % 256);
            
            await _fileSystemMock.WriteAllBytesAsync(file1Path, data1);
            await _fileSystemMock.WriteAllBytesAsync(file2Path, data2);
            
            var method = typeof(FileComparer).GetMethod("CompareFileContentsAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var result = await (Task<bool>)method.Invoke(_fileComparer, new object[] { file1Path, file2Path });
            
            Assert.False(result);
        }
        
        [Fact]
        public async Task CompareFileContents_DirectTest_SameContent_UsingReflection()
        {
            string file1Path = Path.Combine(_testDirectory, "reflect_same1.bin");
            string file2Path = Path.Combine(_testDirectory, "reflect_same2.bin");
        
            const int size = 10000; 
            byte[] data = new byte[size];
            for (int i = 0; i < size; i++)
            {
                data[i] = (byte)(i % 256);
            }
            await _fileSystemMock.WriteAllBytesAsync(file1Path, data);
            await _fileSystemMock.WriteAllBytesAsync(file2Path, data);
            
            var method = typeof(FileComparer).GetMethod("CompareFileContentsAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var result = await (Task<bool>)method.Invoke(_fileComparer, new object[] { file1Path, file2Path });
            
            Assert.True(result);
        }
        
        [Fact]
        public async Task CompareFileContents_DirectTest_SameSizeDifferentContent_UsingReflection()
        {
            string file1Path = Path.Combine(_testDirectory, "reflect_samesize1.bin");
            string file2Path = Path.Combine(_testDirectory, "reflect_samesize2.bin");
            
            const int size = 10000;
            byte[] data1 = new byte[size];
            byte[] data2 = new byte[size];
            
            for (int i = 0; i < size; i++)
            {
                data1[i] = (byte)(i % 256);
                data2[i] = (byte)((i + 128) % 256); 
            }
            
            await _fileSystemMock.WriteAllBytesAsync(file1Path, data1);
            await _fileSystemMock.WriteAllBytesAsync(file2Path, data2);
            
            var method = typeof(FileComparer).GetMethod("CompareFileContentsAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var result = await (Task<bool>)method.Invoke(_fileComparer, new object[] { file1Path, file2Path });
            
            Assert.False(result);
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