using Microsoft.Extensions.Logging;
using Moq;
using PeriodicFolderSync.Core;
using FolderSync.Tests.Mocks;
using PeriodicFolderSync.Interfaces;

namespace FolderSync.Tests.UnitTests
{
    public class FolderOperatorMockTests
    {
        private readonly string _testDirectory;
        private readonly string _sourceDirectory;
        private readonly string _destinationDirectory;
        private readonly FolderOperator _folderOperator;
        private readonly FileOperator _fileOperator;
        private readonly MockFileSystem _mockFileSystem;

        public FolderOperatorMockTests()
        {
            _testDirectory = @"D:\TestDirectory";
            _sourceDirectory = Path.Combine(_testDirectory, "Source");
            _destinationDirectory = Path.Combine(_testDirectory, "Destination");

            _mockFileSystem = new MockFileSystem();
            _mockFileSystem.CreateDirectory(_sourceDirectory);
            _mockFileSystem.CreateDirectory(_destinationDirectory);

            Mock<ILogger> loggerMock = new();
            Mock<IFileComparer> fileComparerMock = new();
            fileComparerMock.Setup(fc => fc.AreFilesIdenticalAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>(async (file1, file2) => await CompareFiles(file1, file2));
            
            _fileOperator = new FileOperator((ILogger<IFileOperator>)loggerMock.Object, _mockFileSystem, fileComparerMock.Object);
            _folderOperator = new FolderOperator((ILogger<IFolderOperator>)loggerMock.Object, _mockFileSystem);
        }

        private string CreateTestFile(string directory, string fileName, string content = "Test content")
        {
            string filePath = Path.Combine(directory, fileName);
            _mockFileSystem.WriteAllTextAsync(filePath, content).Wait();
            return filePath;
        }

        private string CreateTestDirectory(string parentDirectory, string directoryName)
        {
            string dirPath = Path.Combine(parentDirectory, directoryName);
            _mockFileSystem.CreateDirectory(dirPath);
            return dirPath;
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
        public async Task CopyFolderAsync_ShouldCopyFolder_WhenDestinationDoesNotExist()
        {
            string sourceSubDir = CreateTestDirectory(_sourceDirectory, "SubDir");
            string sourceFile1 = CreateTestFile(_sourceDirectory, "file1.txt", "Content 1");
            string sourceFile2 = CreateTestFile(sourceSubDir, "file2.txt", "Content 2");

            string destDir = Path.Combine(_destinationDirectory, "CopiedDir");

            await _folderOperator.CopyFolderAsync(_sourceDirectory, destDir);

            Assert.True(_mockFileSystem.DirectoryExists(destDir));
            Assert.True(_mockFileSystem.DirectoryExists(Path.Combine(destDir, "SubDir")));
            Assert.True(_mockFileSystem.FileExists(Path.Combine(destDir, "file1.txt")));
            Assert.True(_mockFileSystem.FileExists(Path.Combine(destDir, "SubDir", "file2.txt")));

            Assert.Equal("Content 1", await _mockFileSystem.ReadAllTextAsync(Path.Combine(destDir, "file1.txt")));
            Assert.Equal("Content 2",
                await _mockFileSystem.ReadAllTextAsync(Path.Combine(destDir, "SubDir", "file2.txt")));
        }

        [Fact]
        public async Task CopyFolderAsync_ShouldThrowException_WhenSourceDirectoryDoesNotExist()
        {
            string nonExistentDir = Path.Combine(_sourceDirectory, "NonExistent");
            string destDir = Path.Combine(_destinationDirectory, "CopiedDir");

            await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
                _folderOperator.CopyFolderAsync(nonExistentDir, destDir));
        }

        [Fact]
        public async Task CopyFolderAsync_ShouldThrowException_WhenDestinationDirectoryExists()
        {
            string destDir = CreateTestDirectory(_destinationDirectory, "ExistingDir");

            await Assert.ThrowsAsync<IOException>(() =>
                _folderOperator.CopyFolderAsync(_sourceDirectory, destDir, false));
        }

        [Fact]
        public async Task CopyFolderAsync_ShouldOverwriteDestination_WhenOverwriteIsTrue()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "file.txt", "New content");

            string destDir = CreateTestDirectory(_destinationDirectory, "ExistingDir");
            CreateTestFile(destDir, "file.txt", "Old content");
            CreateTestFile(destDir, "extra.txt", "Extra content");

            await _folderOperator.CopyFolderAsync(_sourceDirectory, destDir, true);

            Assert.True(_mockFileSystem.DirectoryExists(destDir));
            Assert.True(_mockFileSystem.FileExists(Path.Combine(destDir, "file.txt")));
            Assert.False(_mockFileSystem.FileExists(Path.Combine(destDir, "extra.txt")));
            Assert.Equal("New content", await _mockFileSystem.ReadAllTextAsync(Path.Combine(destDir, "file.txt")));
        }

        [Fact]
        public async Task CopyFolderAsync_ShouldNotCopySubdirectories_WhenRecursiveIsFalse()
        {
            string sourceSubDir = CreateTestDirectory(_sourceDirectory, "SubDir");
            string sourceFile1 = CreateTestFile(_sourceDirectory, "file1.txt", "Content 1");
            string sourceFile2 = CreateTestFile(sourceSubDir, "file2.txt", "Content 2");

            string destDir = Path.Combine(_destinationDirectory, "CopiedDir");

            await _folderOperator.CopyFolderAsync(_sourceDirectory, destDir, recursive: false);

            Assert.True(_mockFileSystem.DirectoryExists(destDir));
            Assert.False(_mockFileSystem.DirectoryExists(Path.Combine(destDir, "SubDir")));
            Assert.True(_mockFileSystem.FileExists(Path.Combine(destDir, "file1.txt")));
            Assert.Equal("Content 1", await _mockFileSystem.ReadAllTextAsync(Path.Combine(destDir, "file1.txt")));
        }

        [Fact]
        public async Task DeleteFolderAsync_ShouldDeleteFolder_WhenFolderExists()
        {
            string dirToDelete = CreateTestDirectory(_sourceDirectory, "ToDelete");
            CreateTestFile(dirToDelete, "file.txt");

            await _folderOperator.DeleteFolderAsync(dirToDelete);

            Assert.False(_mockFileSystem.DirectoryExists(dirToDelete));
        }

        [Fact]
        public async Task DeleteFolderAsync_ShouldNotThrowException_WhenFolderDoesNotExist()
        {
            string nonExistentDir = Path.Combine(_sourceDirectory, "NonExistent");

            await _folderOperator.DeleteFolderAsync(nonExistentDir);

            Assert.False(_mockFileSystem.DirectoryExists(nonExistentDir));
        }

        [Fact]
        public async Task MoveFolderAsync_ShouldMoveFolder_WhenDestinationDoesNotExist()
        {
            string sourceSubDir = CreateTestDirectory(_sourceDirectory, "ToMove");
            string sourceFile1 = CreateTestFile(sourceSubDir, "file1.txt", "Content 1");

            string destDir = Path.Combine(_destinationDirectory, "MovedDir");

            await _folderOperator.MoveFolderAsync(sourceSubDir, destDir, false);

            Assert.False(_mockFileSystem.DirectoryExists(sourceSubDir));
            Assert.True(_mockFileSystem.DirectoryExists(destDir));
            Assert.True(_mockFileSystem.FileExists(Path.Combine(destDir, "file1.txt")));
            Assert.Equal("Content 1", await _mockFileSystem.ReadAllTextAsync(Path.Combine(destDir, "file1.txt")));
        }

        [Fact]
        public async Task MoveFolderAsync_ShouldThrowException_WhenSourceDirectoryDoesNotExist()
        {
            string nonExistentDir = Path.Combine(_sourceDirectory, "NonExistent");
            string destDir = Path.Combine(_destinationDirectory, "MovedDir");

            await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
                _folderOperator.MoveFolderAsync(nonExistentDir, destDir , false));
        }

        [Fact]
        public async Task MoveFolderAsync_ShouldThrowException_WhenDestinationDirectoryExists()
        {
            string sourceDir = CreateTestDirectory(_sourceDirectory, "ToMove");
            string destDir = CreateTestDirectory(_destinationDirectory, "ExistingDir");

            await Assert.ThrowsAsync<IOException>(() =>
                _folderOperator.MoveFolderAsync(sourceDir, destDir, false));
        }

        [Fact]
        public async Task MoveFolderAsync_ShouldOverwriteDestination_WhenOverwriteIsTrue()
        {
            string sourceDir = CreateTestDirectory(_sourceDirectory, "ToMove");
            CreateTestFile(sourceDir, "file.txt", "New content");

            string destDir = CreateTestDirectory(_destinationDirectory, "ExistingDir");
            CreateTestFile(destDir, "file.txt", "Old content");
            CreateTestFile(destDir, "extra.txt", "Extra content");

            await _folderOperator.MoveFolderAsync(sourceDir, destDir, true);

            Assert.False(_mockFileSystem.DirectoryExists(sourceDir));
            Assert.True(_mockFileSystem.DirectoryExists(destDir));
            Assert.True(_mockFileSystem.FileExists(Path.Combine(destDir, "file.txt")));
            Assert.False(_mockFileSystem.FileExists(Path.Combine(destDir, "extra.txt")));
            Assert.Equal("New content", await _mockFileSystem.ReadAllTextAsync(Path.Combine(destDir, "file.txt")));
        }

        [Fact]
        public async Task MoveFolderAsync_ShouldCreateParentDirectories_WhenTheyDoNotExist()
        {
            string sourceDir = CreateTestDirectory(_sourceDirectory, "ToMove");
            CreateTestFile(sourceDir, "file.txt", "Content");

            string destParent = Path.Combine(_destinationDirectory, "NewParent");
            string destDir = Path.Combine(destParent, "MovedDir");

            await _folderOperator.MoveFolderAsync(sourceDir, destDir, false);

            Assert.False(_mockFileSystem.DirectoryExists(sourceDir));
            Assert.True(_mockFileSystem.DirectoryExists(destParent));
            Assert.True(_mockFileSystem.DirectoryExists(destDir));
            Assert.True(_mockFileSystem.FileExists(Path.Combine(destDir, "file.txt")));
            Assert.Equal("Content", await _mockFileSystem.ReadAllTextAsync(Path.Combine(destDir, "file.txt")));
        }

        [Fact]
        public void GetFiles_ShouldReturnAllFiles_InDirectory()
        {
            string file1 = CreateTestFile(_sourceDirectory, "file1.txt", "Content 1");
            string file2 = CreateTestFile(_sourceDirectory, "file2.txt", "Content 2");
            string subDir = CreateTestDirectory(_sourceDirectory, "SubDir");
            string file3 = CreateTestFile(subDir, "file3.txt", "Content 3");

            var files = _mockFileSystem.GetFiles(_sourceDirectory);

            var enumerable = files as string[] ?? files.ToArray();
            Assert.Equal(2, enumerable.Count());
            Assert.Contains(file1, enumerable);
            Assert.Contains(file2, enumerable);
            Assert.DoesNotContain(file3, enumerable);
        }

        [Fact]
        public void GetFiles_ShouldReturnEmptyCollection_WhenDirectoryIsEmpty()
        {
            string emptyDir = CreateTestDirectory(_sourceDirectory, "EmptyDir");

            var files = _mockFileSystem.GetFiles(emptyDir);

            Assert.Empty(files);
        }

        [Fact]
        public void GetFiles_ShouldThrowException_WhenDirectoryDoesNotExist()
        {
            string nonExistentDir = Path.Combine(_sourceDirectory, "NonExistent");

            Assert.Throws<DirectoryNotFoundException>(() =>
                _mockFileSystem.GetFiles(nonExistentDir));
        }

        [Fact]
        public void GetDirectories_ShouldReturnAllDirectories_InDirectory()
        {
            string subDir1 = CreateTestDirectory(_sourceDirectory, "SubDir1");
            string subDir2 = CreateTestDirectory(_sourceDirectory, "SubDir2");
            string nestedDir = CreateTestDirectory(subDir1, "NestedDir");

            var directories = _mockFileSystem.GetDirectories(_sourceDirectory);

            var enumerable = directories as string[] ?? directories.ToArray();
            Assert.Equal(2, enumerable.Count());
            Assert.Contains(subDir1, enumerable);
            Assert.Contains(subDir2, enumerable);
            Assert.DoesNotContain(nestedDir, enumerable);
        }

        [Fact]
        public void GetDirectories_ShouldReturnEmptyCollection_WhenDirectoryHasNoSubdirectories()
        {
            string emptyDir = CreateTestDirectory(_sourceDirectory, "EmptyDir");

            var directories = _mockFileSystem.GetDirectories(emptyDir);

            Assert.Empty(directories);
        }

        [Fact]
        public void GetDirectories_ShouldThrowException_WhenDirectoryDoesNotExist()
        {
            string nonExistentDir = Path.Combine(_sourceDirectory, "NonExistent");

            Assert.Throws<DirectoryNotFoundException>(() =>
                _mockFileSystem.GetDirectories(nonExistentDir));
        }

        [Fact]
        public void DirectoryExists_ShouldReturnTrue_WhenDirectoryExists()
        {
            bool exists = _mockFileSystem.DirectoryExists(_sourceDirectory);

            Assert.True(exists);
        }

        [Fact]
        public void DirectoryExists_ShouldReturnFalse_WhenDirectoryDoesNotExist()
        {
            string nonExistentDir = Path.Combine(_sourceDirectory, "NonExistent");

            bool exists = _mockFileSystem.DirectoryExists(nonExistentDir);

            Assert.False(exists);
        }

        [Fact]
        public void FileExists_ShouldReturnTrue_WhenFileExists()
        {
            string filePath = CreateTestFile(_sourceDirectory, "testFile.txt");

            bool exists = _mockFileSystem.FileExists(filePath);

            Assert.True(exists);
        }

        [Fact]
        public void FileExists_ShouldReturnFalse_WhenFileDoesNotExist()
        {
            string nonExistentFile = Path.Combine(_sourceDirectory, "nonExistent.txt");

            bool exists = _mockFileSystem.FileExists(nonExistentFile);

            Assert.False(exists);
        }

        [Fact]
        public async Task CopyFolderAsync_ShouldSkipUnchangedFiles_WhenFilesAreIdentical()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "unchanged.txt", "Test content");
            string destDir = Path.Combine(_destinationDirectory, "DestDir");
            _mockFileSystem.CreateDirectory(destDir);
            string destFile = Path.Combine(destDir, "unchanged.txt");

            await _mockFileSystem.WriteAllTextAsync(destFile, "Test content");

            var now = DateTime.Now;
            _mockFileSystem.SetLastWriteTimeUtc(sourceFile, now);
            _mockFileSystem.SetLastWriteTimeUtc(destFile, now);
            
            await _folderOperator.CopyFolderAsync(_sourceDirectory, destDir, overwrite: true);
            
            var sourceBytes = await _mockFileSystem.ReadAllBytesAsync(sourceFile);
            var destBytes = await _mockFileSystem.ReadAllBytesAsync(destFile);
            System.Diagnostics.Debug.WriteLine($"Source file size: {sourceBytes.Length}, content: {System.Text.Encoding.UTF8.GetString(sourceBytes)}");
            System.Diagnostics.Debug.WriteLine($"Dest file size: {destBytes.Length}, content: {System.Text.Encoding.UTF8.GetString(destBytes)}");
            
            var sourceInfo = _mockFileSystem.GetFileInfo(sourceFile);
            var destInfo = _mockFileSystem.GetFileInfo(destFile);
            
            Assert.True(await CompareFiles(sourceFile, destFile));
            Assert.Equal(sourceInfo.LastWriteTime, destInfo.LastWriteTime);
        }

        [Fact]
        public async Task CopyFolderAsync_ShouldCopyChangedFiles_WhenContentDiffers()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "changed.txt", "New content");
            string destDir = Path.Combine(_destinationDirectory, "DestDir");
            _mockFileSystem.CreateDirectory(destDir);
            string destFile = Path.Combine(destDir, "changed.txt");

            await _mockFileSystem.WriteAllTextAsync(destFile, "Old content");

            var originalDestContent = await _mockFileSystem.ReadAllTextAsync(destFile);
            Assert.Equal("Old content", originalDestContent);

            await _folderOperator.CopyFolderAsync(_sourceDirectory, destDir, overwrite: true);

            var newDestContent = await _mockFileSystem.ReadAllTextAsync(destFile);
            Assert.Equal("New content", newDestContent);
            Assert.True(await CompareFiles(sourceFile, destFile));
        }

        [Fact]
        public async Task CopyFolderAsync_ShouldCopyChangedFiles_WhenSizeDiffers()
        {
            string sourceFile = CreateTestFile(_sourceDirectory, "size_diff.txt", "Longer content with more text");
            string destDir = Path.Combine(_destinationDirectory, "DestDir");
            _mockFileSystem.CreateDirectory(destDir);
            string destFile = Path.Combine(destDir, "size_diff.txt");

            await _mockFileSystem.WriteAllTextAsync(destFile, "Short");

            var sourceInfo = _mockFileSystem.GetFileInfo(sourceFile);
            var destInfo = _mockFileSystem.GetFileInfo(destFile);
            
            byte[] sourceBytes = await _mockFileSystem.ReadAllBytesAsync(sourceFile);
            byte[] destBytes = await _mockFileSystem.ReadAllBytesAsync(destFile);
            Assert.NotEqual(sourceBytes.Length, destBytes.Length);

            await _folderOperator.CopyFolderAsync(_sourceDirectory, destDir, overwrite: true);

            Assert.True(await CompareFiles(sourceFile, destFile));
            var newDestInfo = _mockFileSystem.GetFileInfo(destFile);
            Assert.Equal(sourceInfo.Length, newDestInfo.Length);
        }
    }
}
            