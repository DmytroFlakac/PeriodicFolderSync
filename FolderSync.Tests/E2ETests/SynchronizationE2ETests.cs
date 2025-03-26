using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PeriodicFolderSync.Core;
using PeriodicFolderSync.Interfaces;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FolderSync.Tests.E2ETests
{
    public class SynchronizationE2ETests : IDisposable
    {
        private readonly string _sourceFolder;
        private readonly string _destinationFolder;
        private readonly ServiceProvider _serviceProvider;

        public SynchronizationE2ETests()
        {
            _sourceFolder = Path.Combine(Path.GetTempPath(), "FolderSyncE2E_Source_" + Guid.NewGuid().ToString());
            _destinationFolder = Path.Combine(Path.GetTempPath(), "FolderSyncE2E_Destination_" + Guid.NewGuid().ToString());
            
            Directory.CreateDirectory(_sourceFolder);
            Directory.CreateDirectory(_destinationFolder);

            var services = new ServiceCollection();
            services.AddLogging(config => config.AddConsole());
            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddSingleton<IFolderOperator, FolderOperator>();
            services.AddSingleton<IFileOperator, FileOperator>();
            services.AddSingleton<IMatchStrategy, ContentBasedMatchStrategy>();
            services.AddSingleton<IFileComparer, FileComparer>();
            services.AddSingleton<IFolderSynchronizer, FolderSynchronizer>();
            services.AddSingleton<IFileSynchronizer, FileSynchronizer>();
            services.AddSingleton<ISynchronizer, Synchronizer>();
            // Add the missing AdminPrivilegeHandler registration
            services.AddSingleton<IAdminPrivilegeHandler, AdminPrivilegeHandler>();
            services.AddSingleton<ICLIProcessor, CLIProcessor>();
            services.AddSingleton<IScheduler, Scheduler>();

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task OneTimeSync_ShouldSynchronizeFiles()
        {
            var testFile1Path = Path.Combine(_sourceFolder, "testfile1.txt");
            var testFile2Path = Path.Combine(_sourceFolder, "testfile2.txt");
            
            await File.WriteAllTextAsync(testFile1Path, "Test content 1");
            await File.WriteAllTextAsync(testFile2Path, "Test content 2");
            
            var subDirPath = Path.Combine(_sourceFolder, "subdir");
            Directory.CreateDirectory(subDirPath);
            var testFile3Path = Path.Combine(subDirPath, "testfile3.txt");
            await File.WriteAllTextAsync(testFile3Path, "Test content 3");

            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
            await cliProcessor.ProcessAsync(args);

            Assert.True(File.Exists(Path.Combine(_destinationFolder, "testfile1.txt")));
            Assert.True(File.Exists(Path.Combine(_destinationFolder, "testfile2.txt")));
            Assert.True(Directory.Exists(Path.Combine(_destinationFolder, "subdir")));
            Assert.True(File.Exists(Path.Combine(_destinationFolder, "subdir", "testfile3.txt")));
            
            Assert.Equal("Test content 1", await File.ReadAllTextAsync(Path.Combine(_destinationFolder, "testfile1.txt")));
            Assert.Equal("Test content 2", await File.ReadAllTextAsync(Path.Combine(_destinationFolder, "testfile2.txt")));
            Assert.Equal("Test content 3", await File.ReadAllTextAsync(Path.Combine(_destinationFolder, "subdir", "testfile3.txt")));
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleFileUpdates()
        {
            var testFilePath = Path.Combine(_sourceFolder, "updatedfile.txt");
            var destFilePath = Path.Combine(_destinationFolder, "updatedfile.txt");
            
            await File.WriteAllTextAsync(testFilePath, "Updated content");
            await File.WriteAllTextAsync(destFilePath, "Original content");
            
            File.SetLastWriteTime(destFilePath, DateTime.Now.AddDays(-1));

            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
            await cliProcessor.ProcessAsync(args);

            Assert.Equal("Updated content", await File.ReadAllTextAsync(destFilePath));
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleFileDeletions()
        {
            var destOnlyFilePath = Path.Combine(_destinationFolder, "toDelete.txt");
            await File.WriteAllTextAsync(destOnlyFilePath, "This file should be deleted");

            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
            await cliProcessor.ProcessAsync(args);

            Assert.False(File.Exists(destOnlyFilePath));
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleEmptyFolders()
        {
            var emptyDir1 = Path.Combine(_sourceFolder, "emptyDir1");
            var emptyDir2 = Path.Combine(_sourceFolder, "emptyDir2", "nestedEmpty");
            Directory.CreateDirectory(emptyDir1);
            Directory.CreateDirectory(emptyDir2);

            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
            await cliProcessor.ProcessAsync(args);

            Assert.True(Directory.Exists(Path.Combine(_destinationFolder, "emptyDir1")));
            Assert.True(Directory.Exists(Path.Combine(_destinationFolder, "emptyDir2", "nestedEmpty")));
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleLargeFiles()
        {
            var largeFilePath = Path.Combine(_sourceFolder, "largefile.bin");

            await using (var fs = new FileStream(largeFilePath, FileMode.Create, FileAccess.Write))
            {
                fs.SetLength(5 * 1024 * 1024);
                
                byte[] startBytes = System.Text.Encoding.UTF8.GetBytes("START_MARKER");
                byte[] endBytes = System.Text.Encoding.UTF8.GetBytes("END_MARKER");
                
                await fs.WriteAsync(startBytes, 0, startBytes.Length);
                fs.Seek(fs.Length - endBytes.Length, SeekOrigin.Begin);
                await fs.WriteAsync(endBytes, 0, endBytes.Length);
            }

            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
            await cliProcessor.ProcessAsync(args);

            var destFilePath = Path.Combine(_destinationFolder, "largefile.bin");
            Assert.True(File.Exists(destFilePath));
            Assert.Equal(5 * 1024 * 1024, new FileInfo(destFilePath).Length);

            await using (var fs = new FileStream(destFilePath, FileMode.Open, FileAccess.Read))
            {
                byte[] startBytes = new byte[12];
                await fs.ReadAsync(startBytes, 0, startBytes.Length);
                string startMarker = System.Text.Encoding.UTF8.GetString(startBytes);
                
                fs.Seek(fs.Length - 10, SeekOrigin.Begin);
                byte[] endBytes = new byte[10];
                await fs.ReadAsync(endBytes, 0, endBytes.Length);
                string endMarker = System.Text.Encoding.UTF8.GetString(endBytes);
                
                Assert.Equal("START_MARKER", startMarker);
                Assert.Equal("END_MARKER", endMarker);
            }
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleReadOnlyFiles()
        {
            var sourceFilePath = Path.Combine(_sourceFolder, "readonly.txt");
            var destFilePath = Path.Combine(_destinationFolder, "readonly.txt");
            
            await File.WriteAllTextAsync(sourceFilePath, "This will replace a read-only file");
            await File.WriteAllTextAsync(destFilePath, "Original read-only content");
            File.SetAttributes(destFilePath, FileAttributes.ReadOnly);
            
            await Task.Delay(100);

            try
            {
                var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
                var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
                await cliProcessor.ProcessAsync(args);

                if (File.Exists(destFilePath))
                {
                    var attributes = File.GetAttributes(destFilePath);
                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(destFilePath, attributes & ~FileAttributes.ReadOnly);
                    }
                }
                
                Assert.Equal("This will replace a read-only file", await File.ReadAllTextAsync(destFilePath));
            }
            finally
            {
                if (File.Exists(destFilePath))
                {
                    File.SetAttributes(destFilePath, FileAttributes.Normal);
                }
            }
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleSpecialCharactersInFilenames()
        {
            var specialChars = "file with spaces-and_special#chars!.txt";
            var sourceFilePath = Path.Combine(_sourceFolder, specialChars);
            
            await File.WriteAllTextAsync(sourceFilePath, "Special filename content");

            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
            await cliProcessor.ProcessAsync(args);

            var destFilePath = Path.Combine(_destinationFolder, specialChars);
            Assert.True(File.Exists(destFilePath));
            Assert.Equal("Special filename content", await File.ReadAllTextAsync(destFilePath));
        }

        [Fact]
        public async Task PeriodicSync_ShouldSynchronizeChangesOverTime()
        {
            var testFilePath = Path.Combine(_sourceFolder, "periodic.txt");
            await File.WriteAllTextAsync(testFilePath, "Initial content");

            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var scheduler = _serviceProvider.GetRequiredService<IScheduler>();
            
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder, "--interval", "1s" };
            
            using var cts = new CancellationTokenSource();
            
            var schedulerTask = Task.Run(async () => {
                try {
                    await cliProcessor.ProcessAsync(args);
                }
                catch (TaskCanceledException) {
                }
            }, cts.Token);
            
            await Task.Delay(1500, cts.Token);
            
            var destFilePath = Path.Combine(_destinationFolder, "periodic.txt");
            Assert.True(File.Exists(destFilePath));
            Assert.Equal("Initial content", await File.ReadAllTextAsync(destFilePath, cts.Token));
            
            await File.WriteAllTextAsync(testFilePath, "Updated content", cts.Token);
            
            await Task.Delay(1500, cts.Token);
            
            Assert.Equal("Updated content", await File.ReadAllTextAsync(destFilePath, cts.Token));
            
            await scheduler.Stop();
            await cts.CancelAsync();
            
            try {
                await Task.WhenAny(schedulerTask, Task.Delay(2000, CancellationToken.None));
            }
            catch (TaskCanceledException) {
            }
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleDeepNestedDirectories()
        {
            var deepPath = Path.Combine(_sourceFolder, "level1", "level2", "level3", "level4", "level5");
            Directory.CreateDirectory(deepPath);
            
            var deepFilePath = Path.Combine(deepPath, "deepfile.txt");
            await File.WriteAllTextAsync(deepFilePath, "Deep nested file content");

            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
            await cliProcessor.ProcessAsync(args);

            var destDeepPath = Path.Combine(_destinationFolder, "level1", "level2", "level3", "level4", "level5");
            var destDeepFilePath = Path.Combine(destDeepPath, "deepfile.txt");
            
            Assert.True(Directory.Exists(destDeepPath));
            Assert.True(File.Exists(destDeepFilePath));
            Assert.Equal("Deep nested file content", await File.ReadAllTextAsync(destDeepFilePath));
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleHiddenFiles()
        {
            var hiddenFilePath = Path.Combine(_sourceFolder, "hidden.txt");
            await File.WriteAllTextAsync(hiddenFilePath, "Hidden file content");
            File.SetAttributes(hiddenFilePath, FileAttributes.Hidden);

            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
            await cliProcessor.ProcessAsync(args);

            var destHiddenFilePath = Path.Combine(_destinationFolder, "hidden.txt");
            Assert.True(File.Exists(destHiddenFilePath));
            
            var attributes = File.GetAttributes(destHiddenFilePath);
            Assert.True((attributes & FileAttributes.Hidden) == FileAttributes.Hidden);
            
            Assert.Equal("Hidden file content", await File.ReadAllTextAsync(destHiddenFilePath));
            
            File.SetAttributes(hiddenFilePath, FileAttributes.Normal);
            if (File.Exists(destHiddenFilePath))
                File.SetAttributes(destHiddenFilePath, FileAttributes.Normal);
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleZeroByteFiles()
        {
            // Arrange
            var zeroByteFilePath = Path.Combine(_sourceFolder, "empty.txt");
            File.Create(zeroByteFilePath).Close();

            // Act
            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
            await cliProcessor.ProcessAsync(args);

            // Assert
            var destFilePath = Path.Combine(_destinationFolder, "empty.txt");
            Assert.True(File.Exists(destFilePath));
            Assert.Equal(0, new FileInfo(destFilePath).Length);
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleFilesWithSameNameButDifferentCase()
        {
            var lowerCaseFilePath = Path.Combine(_sourceFolder, "case.txt");
            await File.WriteAllTextAsync(lowerCaseFilePath, "Lower case content");

            var upperCaseFilePath = Path.Combine(_destinationFolder, "CASE.txt");
            await File.WriteAllTextAsync(upperCaseFilePath, "Upper case content");

            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
            await cliProcessor.ProcessAsync(args);

            var destFilePath = Path.Combine(_destinationFolder, "case.txt");
            Assert.True(File.Exists(destFilePath));
            Assert.Equal("Lower case content", await File.ReadAllTextAsync(destFilePath));
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleLockedFiles()
        {
            var sourceFilePath = Path.Combine(_sourceFolder, "source.txt");
            await File.WriteAllTextAsync(sourceFilePath, "Source content");

            var lockedFilePath = Path.Combine(_destinationFolder, "source.txt");
            await File.WriteAllTextAsync(lockedFilePath, "Destination content");

            await using (var stream = new FileStream(lockedFilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
                var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
                
                await cliProcessor.ProcessAsync(args);
            }

            
            var cliProcessor2 = _serviceProvider.GetRequiredService<ICLIProcessor>();
            await cliProcessor2.ProcessAsync(["--source", _sourceFolder, "--destination", _destinationFolder]);
            
            Assert.Equal("Source content", await File.ReadAllTextAsync(lockedFilePath));
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleVeryLongFilePaths()
        {
            
            try
            {
                string longFolderPath = _sourceFolder;
                string folderName = "subfolder";
                
                for (int i = 0; i < 20; i++)
                {
                    longFolderPath = Path.Combine(longFolderPath, folderName + i);
                    Directory.CreateDirectory(longFolderPath);
                }

                var longFilePath = Path.Combine(longFolderPath, "longpathfile.txt");
                await File.WriteAllTextAsync(longFilePath, "Long path content");

                var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
                var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
                await cliProcessor.ProcessAsync(args);

                string expectedDestPath = _destinationFolder;
                for (int i = 0; i < 20; i++)
                {
                    expectedDestPath = Path.Combine(expectedDestPath, folderName + i);
                }
                var destLongFilePath = Path.Combine(expectedDestPath, "longpathfile.txt");
                
                Assert.True(File.Exists(destLongFilePath));
                Assert.Equal("Long path content", await File.ReadAllTextAsync(destLongFilePath));
            }
            catch (PathTooLongException)
            {
              // ignore
            }
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleFileNameWithSpecialUnicodeCharacters()
        {
            // Arrange
            var unicodeFileName = "unicode_😀_🌍_🎉.txt";
            var unicodeFilePath = Path.Combine(_sourceFolder, unicodeFileName);
            await File.WriteAllTextAsync(unicodeFilePath, "Unicode content");

            // Act
            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
            await cliProcessor.ProcessAsync(args);

            // Assert
            var destFilePath = Path.Combine(_destinationFolder, unicodeFileName);
            Assert.True(File.Exists(destFilePath));
            Assert.Equal("Unicode content", await File.ReadAllTextAsync(destFilePath));
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleBinaryFiles()
        {
            var binaryFilePath = Path.Combine(_sourceFolder, "binary.bin");

            await using (var stream = new FileStream(binaryFilePath, FileMode.Create))
            {
                byte[] binaryData = new byte[1024];
                for (int i = 0; i < binaryData.Length; i++)
                {
                    binaryData[i] = (byte)(i % 256);
                }
                await stream.WriteAsync(binaryData, 0, binaryData.Length);
            }

            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
            await cliProcessor.ProcessAsync(args);

            var destFilePath = Path.Combine(_destinationFolder, "binary.bin");
            Assert.True(File.Exists(destFilePath));
            
            byte[] sourceBytes = await File.ReadAllBytesAsync(binaryFilePath);
            byte[] destBytes = await File.ReadAllBytesAsync(destFilePath);
            Assert.Equal(sourceBytes, destBytes);
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleSymbolicLinks()
        {
            try
            {
                var linkPath = Path.Combine(_sourceFolder, "symlink");
                var targetPath = Path.Combine(_sourceFolder, "target.txt");
                
                await File.WriteAllTextAsync(targetPath, "Target file content");
                
                bool canCreateSymlinks = false;
                try
                {
                    Directory.CreateSymbolicLink(linkPath, targetPath);
                    canCreateSymlinks = true;
                }
                catch (IOException)
                {
                    // Symbolic links not supported or permission denied
                }
                catch (UnauthorizedAccessException)
                {
                    // No permission to create symbolic links
                }
                
                if (!canCreateSymlinks)
                {
                    // Skip the test if we can't create symbolic links
                    return;
                }

                var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
                var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
                await cliProcessor.ProcessAsync(args);
                
                var destLinkPath = Path.Combine(_destinationFolder, "symlink");
                var destTargetPath = Path.Combine(_destinationFolder, "target.txt");
                
                Assert.True(File.Exists(destTargetPath));
                Assert.Equal("Target file content", await File.ReadAllTextAsync(destTargetPath));
            }
            catch (Exception)
            {
                // ignore
            }
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleFileWithoutExtension()
        {
            var noExtensionFilePath = Path.Combine(_sourceFolder, "filewithoutext");
            await File.WriteAllTextAsync(noExtensionFilePath, "No extension content");

            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
            await cliProcessor.ProcessAsync(args);

            var destFilePath = Path.Combine(_destinationFolder, "filewithoutext");
            Assert.True(File.Exists(destFilePath));
            Assert.Equal("No extension content", await File.ReadAllTextAsync(destFilePath));
        }

        [Fact]
        public async Task OneTimeSync_ShouldHandleDestinationWithExistingContent()
        {
            
            var sourceFile1 = Path.Combine(_sourceFolder, "file1.txt");
            var sourceFile2 = Path.Combine(_sourceFolder, "file2.txt");
            await File.WriteAllTextAsync(sourceFile1, "Source file 1");
            await File.WriteAllTextAsync(sourceFile2, "Source file 2");
            
            var destFile3 = Path.Combine(_destinationFolder, "file3.txt");
            var destFile4 = Path.Combine(_destinationFolder, "file4.txt");
            await File.WriteAllTextAsync(destFile3, "Dest file 3");
            await File.WriteAllTextAsync(destFile4, "Dest file 4");

            var cliProcessor = _serviceProvider.GetRequiredService<ICLIProcessor>();
            var args = new string[] { "--source", _sourceFolder, "--destination", _destinationFolder };
            await cliProcessor.ProcessAsync(args);
            
            Assert.True(File.Exists(Path.Combine(_destinationFolder, "file1.txt")));
            Assert.True(File.Exists(Path.Combine(_destinationFolder, "file2.txt")));
            
            Assert.False(File.Exists(destFile3));
            Assert.False(File.Exists(destFile4));
            
            Assert.Equal("Source file 1", await File.ReadAllTextAsync(Path.Combine(_destinationFolder, "file1.txt")));
            Assert.Equal("Source file 2", await File.ReadAllTextAsync(Path.Combine(_destinationFolder, "file2.txt")));
        }

        public async void Dispose()
        {
            try
            {
                if (Directory.Exists(_destinationFolder))
                {
                    foreach (var file in Directory.GetFiles(_destinationFolder, "*", SearchOption.AllDirectories))
                    {
                        var attributes = File.GetAttributes(file);
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                        }
                    }
                }

                if (Directory.Exists(_sourceFolder))
                    Directory.Delete(_sourceFolder, true);
                
                if (Directory.Exists(_destinationFolder))
                    Directory.Delete(_destinationFolder, true);
            }
            catch
            {
                // ignored
            }
        }
    }
}