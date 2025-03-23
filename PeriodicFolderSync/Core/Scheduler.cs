using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PeriodicFolderSync.Interfaces;

namespace PeriodicFolderSync.Core
{
    public class Scheduler(ISynchronizer synchronizer, ILogger<IScheduler> logger)
        : IScheduler
    {
        private readonly ISynchronizer _synchronizer = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));
        private readonly ILogger<IScheduler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private Timer? _timer;
        private string _source = null!;
        private string _destination = null!;
        private bool _useOverwrite;
        private bool _isSyncing;
        private readonly object _syncLock = new object();

        public async Task Start(string source, string destination, TimeSpan interval, bool useOverwrite)
        {
            _source = source;
            _destination = destination;
            _useOverwrite = useOverwrite;

            _logger.LogInformation($"Starting scheduler with interval: {interval}");
            
            await SyncFolders();
            
            
            _timer = new Timer(async void (_) =>
            {
                try
                {
                    if (_isSyncing)
                    {
                        _logger.LogWarning("Previous sync operation still in progress. Skipping this scheduled sync.");
                        return;
                    }
                    
                    await SyncFolders();
                }
                catch (Exception e)
                {
                    _logger.LogError($"Error during scheduled sync: {e.Message}");
                }
            }, null, interval, interval);
            
        }

        public async Task Stop()
        {
            _logger.LogInformation("Stopping scheduler");
            if (_timer != null)
            {
                await _timer.DisposeAsync();
            }
            _timer = null;
            _source = null!;
            _destination = null!;
            _useOverwrite = false;
        }

        private async Task SyncFolders()
        {
            lock (_syncLock)
            {
                if (_isSyncing)
                {
                    _logger.LogInformation("Sync already in progress. Skipping this request.");
                    return;
                }
                _isSyncing = true;
            }
            
            try
            {
                _logger.LogInformation($"Scheduled sync starting at {DateTime.Now}");
                await _synchronizer.SynchronizeAsync(_source, _destination, _useOverwrite);
                _logger.LogInformation($"Scheduled sync completed at {DateTime.Now}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during scheduled sync: {ex.Message}");
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }
}
