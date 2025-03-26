using Microsoft.Extensions.Logging;
using Moq;
using PeriodicFolderSync.Core;
using PeriodicFolderSync.Interfaces;
using Xunit;
using FolderSync.Tests.Mocks;

namespace FolderSync.Tests.UnitTests
{
    public class CliProcessorTests
    {
        private readonly Mock<ISynchronizer> _mockSynchronizer;
        private readonly Mock<ILogger<ICLIProcessor>> _mockLogger;
        private readonly ICLIProcessor _cliProcessor;
        private readonly ILogConfigurationProvider _logConfigurationProvider;

        public CliProcessorTests()
        {
            _mockSynchronizer = new Mock<ISynchronizer>();
            Mock<IScheduler> mockScheduler = new();
            _mockLogger = new Mock<ILogger<ICLIProcessor>>();
            var adminHandler = new MockAdminPrivilegeHandler();
            _logConfigurationProvider = new Mock<ILogConfigurationProvider>().Object;

            _cliProcessor = new CLIProcessor(_mockSynchronizer.Object, mockScheduler.Object, _mockLogger.Object, adminHandler, _logConfigurationProvider);
        }

        [Fact]
        public async Task Process_WithValidSourceAndDestination_CallsSynchronizer()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            string[] args =
            [
                "--source", Path.Combine(tempDir, "src"),
                "--destination", Path.Combine(tempDir, "dest")
            ];
            Directory.CreateDirectory(args[1]);
            Directory.CreateDirectory(args[3]);

            _mockSynchronizer.Setup(s => s.SynchronizeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>())
                )
                .Returns(Task.CompletedTask);

            await _cliProcessor.ProcessAsync(args);

            _mockSynchronizer.Verify(s => s.SynchronizeAsync(
                    args[1],
                    args[3]),
                Times.Once);

            Directory.Delete(tempDir, true);
        }


        [Fact]
        public async Task Process_WithSynchronizerException_LogsError()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            string[] args =
            [
                "--source", Path.Combine(tempDir, "src"),
                "--destination", Path.Combine(tempDir, "dest")
            ];
            Directory.CreateDirectory(args[1]);
            Directory.CreateDirectory(args[3]);

            _mockSynchronizer.Setup(s => s.SynchronizeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ThrowsAsync(new Exception("Sync error"));

            await _cliProcessor.ProcessAsync(args);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Error: Sync error")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            Directory.Delete(tempDir, true);
        }

        [Fact]
        public async Task Process_WithInvalidInterval_LogsError()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            string[] args =
            [
                "--source", Path.Combine(tempDir, "src"),
                "--destination", Path.Combine(tempDir, "dest"),
                "--interval", "invalid"
            ];

            Directory.CreateDirectory(args[1]);
            Directory.CreateDirectory(args[3]);

            await _cliProcessor.ProcessAsync(args);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) =>
                        o.ToString().StartsWith("Invalid interval format:")), // Match exact message start
                    It.IsAny<ArgumentException>(), // Verify exception type
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            Directory.Delete(tempDir, true);
        }

        
        [Fact]
        public void ParseTimeInterval_WithValidFormats_ReturnsCorrectTimeSpan()
        {
            var privateMethod = typeof(CLIProcessor).GetMethod("ParseTimeInterval",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var result1 = privateMethod?.Invoke(_cliProcessor, ["30s"]) as TimeSpan?;
            Assert.NotNull(result1);
            Assert.Equal(TimeSpan.FromSeconds(30), result1.Value);

            var result2 = privateMethod?.Invoke(_cliProcessor, ["5m"]) as TimeSpan?;
            Assert.NotNull(result2);
            Assert.Equal(TimeSpan.FromMinutes(5), result2.Value);

            var result3 = privateMethod?.Invoke(_cliProcessor, ["2h"]) as TimeSpan?;
            Assert.NotNull(result3);
            Assert.Equal(TimeSpan.FromHours(2), result3.Value);

            var result4 = privateMethod?.Invoke(_cliProcessor, ["1d"]) as TimeSpan?;
            Assert.NotNull(result4);
            Assert.Equal(TimeSpan.FromDays(1), result4.Value);

            var result5 = privateMethod?.Invoke(_cliProcessor, ["1y"]) as TimeSpan?;
            Assert.NotNull(result5);
            Assert.Equal(TimeSpan.FromDays(365), result5.Value);
        }

        [Fact]
        public void ParseTimeInterval_WithInvalidFormat_ThrowsArgumentException()
        {
            var privateMethod = typeof(CLIProcessor).GetMethod("ParseTimeInterval",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(privateMethod);

            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                privateMethod.Invoke(_cliProcessor, ["invalid"]));

            Assert.IsType<ArgumentException>(ex.InnerException);
            Assert.Contains("Invalid time interval format", ex.InnerException.Message);
        }

        [Fact]
        public async Task GetInteractiveInputIfNeeded_WithExistingArgs_ReturnsSameArgs()
        {
            string[] args = ["--source", "src", "--destination", "dest"];

            var result = await _cliProcessor.GetInteractiveInputIfNeededAsync(args);

            Assert.Equal(args, result);
        }

        [Fact]
        public async Task GetInteractiveInputIfNeeded_WithNoArgs_PromptForInput()
        {
            string[] emptyArgs = [];

            var testCliProcessor = new TestCLIProcessorWithInput(
                _mockSynchronizer.Object,
                new Mock<IScheduler>().Object,
                _mockLogger.Object,
                new MockAdminPrivilegeHandler(),
                ["C:\\source", "C:\\destination", "5m", "n"] // Simulated user inputs
            );

            var result = await testCliProcessor.GetInteractiveInputIfNeededAsync(emptyArgs);

            Assert.Equal(6, result.Length);
            Assert.Equal("--source", result[0]);
            Assert.Equal("C:\\source", result[1]);
            Assert.Equal("--destination", result[2]);
            Assert.Equal("C:\\destination", result[3]);
            Assert.Equal("--interval", result[4]);
            Assert.Equal("5m", result[5]);
        }

        [Fact]
        public async Task GetInteractiveInputIfNeeded_WithNoArgsAndOnceInterval_ReturnsCorrectArgs()
        {
            string[] emptyArgs = [];

            var testCliProcessor = new TestCLIProcessorWithInput(
                _mockSynchronizer.Object,
                new Mock<IScheduler>().Object,
                _mockLogger.Object,
                new MockAdminPrivilegeHandler(),
                ["C:\\source", "C:\\destination", "once", "n"] // Simulated user inputs
            );
            
            var result = await testCliProcessor.GetInteractiveInputIfNeededAsync(emptyArgs);

            Assert.Equal(4, result.Length);
            Assert.Equal("--source", result[0]);
            Assert.Equal("C:\\source", result[1]);
            Assert.Equal("--destination", result[2]);
            Assert.Equal("C:\\destination", result[3]);
        }

        [Fact]
        public async Task GetInteractiveInputIfNeeded_WithNoArgsAndAdminFlag_ReturnsCorrectArgs()
        {
            string[] emptyArgs = [];

            var testCliProcessor = new TestCLIProcessorWithInput(
                _mockSynchronizer.Object,
                new Mock<IScheduler>().Object,
                _mockLogger.Object,
                new MockAdminPrivilegeHandler(),
                ["C:\\source", "C:\\destination", "5m", "y"]
            );

            var result = await testCliProcessor.GetInteractiveInputIfNeededAsync(emptyArgs);

            Assert.Equal(7, result.Length);
            Assert.Equal("--source", result[0]);
            Assert.Equal("C:\\source", result[1]);
            Assert.Equal("--destination", result[2]);
            Assert.Equal("C:\\destination", result[3]);
            Assert.Equal("--interval", result[4]);
            Assert.Equal("5m", result[5]);
            Assert.Equal("--admin", result[6]);
        }
    }

    public class TestCLIProcessorWithInput(
        ISynchronizer synchronizer,
        IScheduler scheduler,
        ILogger<ICLIProcessor> logger,
        IAdminPrivilegeHandler adminHandler,
        string[] simulatedInputs)
        : CLIProcessor(synchronizer, scheduler, logger, adminHandler, new Mock<ILogConfigurationProvider>().Object) 
    {
        private readonly Queue<string> _simulatedInputs = new(simulatedInputs);

        protected override string? ReadLineFromConsole()
        {
            return _simulatedInputs.Count > 0 ? _simulatedInputs.Dequeue() : null;
        }
    }
}