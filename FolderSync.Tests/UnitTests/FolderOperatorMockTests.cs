using Microsoft.Extensions.Logging;
using Moq;
using PeriodicFolderSync.Core;
using FolderSync.Tests.Mocks;

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
            _fileOperator = new FileOperator(loggerMock.Object, _mockFileSystem);
            _folderOperator = new FolderOperator(_fileOperator, loggerMock.Object, _mockFileSystem);
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
            Assert.Equal("Content 2", await _mockFileSystem.ReadAllTextAsync(Path.Combine(destDir, "SubDir", "file2.txt")));
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

            await _folderOperator.MoveFolderAsync(sourceSubDir, destDir);

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
                _folderOperator.MoveFolderAsync(nonExistentDir, destDir));
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

            await _folderOperator.MoveFolderAsync(sourceDir, destDir);

            Assert.False(_mockFileSystem.DirectoryExists(sourceDir));
            Assert.True(_mockFileSystem.DirectoryExists(destParent));
            Assert.True(_mockFileSystem.DirectoryExists(destDir));
            Assert.True(_mockFileSystem.FileExists(Path.Combine(destDir, "file.txt")));
            Assert.Equal("Content", await _mockFileSystem.ReadAllTextAsync(Path.Combine(destDir, "file.txt")));
        }

        [Fact]
        public async Task RenameFolderAsync_ShouldRenameFolder_WhenNewNameDoesNotExist()
        {
            string dirToRename = CreateTestDirectory(_sourceDirectory, "ToRename");
            CreateTestFile(dirToRename, "file.txt", "Content");

            string newName = "Renamed";
            string expectedPath = Path.Combine(_sourceDirectory, newName);

            await _folderOperator.RenameFolderAsync(dirToRename, newName);

            Assert.False(_mockFileSystem.DirectoryExists(dirToRename));
            Assert.True(_mockFileSystem.DirectoryExists(expectedPath));
            Assert.True(_mockFileSystem.FileExists(Path.Combine(expectedPath, "file.txt")));
            Assert.Equal("Content", await _mockFileSystem.ReadAllTextAsync(Path.Combine(expectedPath, "file.txt")));
        }

        [Fact]
        public async Task RenameFolderAsync_ShouldAcceptFullPath_WhenNewNameIsFullyQualified()
        {
            string dirToRename = CreateTestDirectory(_sourceDirectory, "ToRename");
            CreateTestFile(dirToRename, "file.txt", "Content");

            string newPath = Path.Combine(_destinationDirectory, "Renamed");

            await _folderOperator.RenameFolderAsync(dirToRename, newPath);

            Assert.False(_mockFileSystem.DirectoryExists(dirToRename));
            Assert.True(_mockFileSystem.DirectoryExists(newPath));
            Assert.True(_mockFileSystem.FileExists(Path.Combine(newPath, "file.txt")));
            Assert.Equal("Content", await _mockFileSystem.ReadAllTextAsync(Path.Combine(newPath, "file.txt")));
        }

        [Fact]
        public async Task RenameFolderAsync_ShouldThrowException_WhenFolderDoesNotExist()
        {
            string nonExistentDir = Path.Combine(_sourceDirectory, "NonExistent");
            string newName = "Renamed";

            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => 
                _folderOperator.RenameFolderAsync(nonExistentDir, newName));
        }

        [Fact]
        public async Task RenameFolderAsync_ShouldThrowException_WhenNewNameIsEmpty()
        {
            string dirToRename = CreateTestDirectory(_sourceDirectory, "ToRename");
            string newName = "";

            await Assert.ThrowsAsync<ArgumentException>(() => 
                _folderOperator.RenameFolderAsync(dirToRename, newName));
        }

        [Fact]
        public async Task RenameFolderAsync_ShouldThrowException_WhenNewNameExists()
        {
            string dirToRename = CreateTestDirectory(_sourceDirectory, "ToRename");
            string existingDir = CreateTestDirectory(_sourceDirectory, "Existing");
            
            await Assert.ThrowsAsync<IOException>(() => 
                _folderOperator.RenameFolderAsync(dirToRename, "Existing", false));
        }

        [Fact]
        public async Task RenameFolderAsync_ShouldOverwriteDestination_WhenOverwriteIsTrue()
        {
            string dirToRename = CreateTestDirectory(_sourceDirectory, "ToRename");
            CreateTestFile(dirToRename, "file.txt", "New content");

            string existingName = "Existing";
            string existingDir = CreateTestDirectory(_sourceDirectory, existingName);
            CreateTestFile(existingDir, "file.txt", "Old content");

            await _folderOperator.RenameFolderAsync(dirToRename, existingName, true);

            Assert.False(_mockFileSystem.DirectoryExists(dirToRename));
            Assert.True(_mockFileSystem.DirectoryExists(existingDir));
            Assert.True(_mockFileSystem.FileExists(Path.Combine(existingDir, "file.txt")));
            Assert.Equal("New content", await _mockFileSystem.ReadAllTextAsync(Path.Combine(existingDir, "file.txt")));
        }

        [Fact]
        public void GetFiles_ShouldReturnAllFiles_InDirectory()
        {
            string file1 = CreateTestFile(_sourceDirectory, "file1.txt", "Content 1");
            string file2 = CreateTestFile(_sourceDirectory, "file2.txt", "Content 2");
            string subDir = CreateTestDirectory(_sourceDirectory, "SubDir");
            string file3 = CreateTestFile(subDir, "file3.txt", "Content 3");

            var files = _mockFileSystem.GetFiles(_sourceDirectory);

            Assert.Equal(2, files.Count());
            Assert.Contains(file1, files);
            Assert.Contains(file2, files);
            Assert.DoesNotContain(file3, files);
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

            Assert.Equal(2, directories.Count());
            Assert.Contains(subDir1, directories);
            Assert.Contains(subDir2, directories);
            Assert.DoesNotContain(nestedDir, directories);
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
    }
}
            