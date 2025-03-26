using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using PeriodicFolderSync.Core;
using PeriodicFolderSync.Interfaces;
using Xunit;

namespace FolderSync.Tests.UnitTests
{
    public class SchedulerTests
    {
        private readonly Mock<ISynchronizer> _synchronizerMock;
        private readonly Mock<ILogger<IScheduler>> _loggerMock;
        private readonly Scheduler _scheduler;
        private readonly string _sourceFolder = "D:\\TestSource";
        private readonly string _destinationFolder = "D:\\TestDestination";

        public SchedulerTests()
        {
            _synchronizerMock = new Mock<ISynchronizer>();
            _loggerMock = new Mock<ILogger<IScheduler>>();
            _scheduler = new Scheduler(_synchronizerMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task Start_CallsSynchronizeImmediately()
        {
            var interval = TimeSpan.FromSeconds(10);
            _synchronizerMock.Setup(s => s.SynchronizeAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            await _scheduler.Start(_sourceFolder, _destinationFolder, interval);

            _synchronizerMock.Verify(s => s.SynchronizeAsync(_sourceFolder, _destinationFolder), Times.Once);
        }

        [Fact]
        public async Task Stop_DisposesTimer()
        {
            var interval = TimeSpan.FromSeconds(10);
            await _scheduler.Start(_sourceFolder, _destinationFolder, interval);

            await _scheduler.Stop();

            _loggerMock.Verify(l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Stopping scheduler")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SyncFolders_HandlesExceptions()
        {
            var interval = TimeSpan.FromSeconds(10);
            _synchronizerMock.Setup(s => s.SynchronizeAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Test exception"));

            await _scheduler.Start(_sourceFolder, _destinationFolder, interval);

            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error during scheduled sync")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ScheduledSync_ExecutesAfterInterval()
        {
            var interval = TimeSpan.FromMilliseconds(100); 
            
            _synchronizerMock.Setup(s => s.SynchronizeAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            await _scheduler.Start(_sourceFolder, _destinationFolder, interval);
            
            await Task.Delay(150);
            
            _synchronizerMock.Verify(s => s.SynchronizeAsync(_sourceFolder, _destinationFolder), Times.AtLeast(2));
        }

        [Fact]
        public async Task ConcurrentSync_SkipsIfAlreadyRunning()
        {
            var interval = TimeSpan.FromMilliseconds(100);
            var syncDelay = TimeSpan.FromMilliseconds(200); 
            
            _synchronizerMock.Setup(s => s.SynchronizeAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(async () => {
                    await Task.Delay(syncDelay);
                });

            await _scheduler.Start(_sourceFolder, _destinationFolder, interval);
            
            await Task.Delay(350); 
            
            _loggerMock.Verify(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Previous sync operation still in progress")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public Task Constructor_WithNullParameters_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new Scheduler(null, _loggerMock.Object));
            Assert.Throws<ArgumentNullException>(() => new Scheduler(_synchronizerMock.Object, null));
            return Task.CompletedTask;
        }
    }
}