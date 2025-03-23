using PeriodicFolderSync.Interfaces;
using System.CommandLine;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace PeriodicFolderSync.Core
{
    public class CliProcessor(ISynchronizer synchronizer, IScheduler scheduler, ILogger<ICliProcessor> logger) : ICliProcessor
    {
        private readonly ISynchronizer _synchronizer = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));
        private readonly IScheduler _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        private readonly ILogger<ICliProcessor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<int> ProcessAsync(string[] args)
        {
            var rootCommand = new RootCommand("PeriodicFolderSync - Synchronize folders periodically");

            var sourceOption = new Option<string>(
                aliases: ["--source", "-s"],
                description: "Source directory path")
            {
                IsRequired = true
            };

            var destinationOption = new Option<string>(
                aliases: ["--destination", "-d"],
                description: "Destination directory path")
            {
                IsRequired = true
            };

            var intervalOption = new Option<string>(
                aliases: ["--interval", "-i"],
                description: "Sync interval in minutes or time format (15s, 1m, 1h, 1d, 1y)");

            var overwriteOption = new Option<bool>(
                aliases: ["--overwrite", "-o"],
                description: "Overwrite files if they exist in destination");

            rootCommand.AddOption(sourceOption);
            rootCommand.AddOption(destinationOption);
            rootCommand.AddOption(intervalOption);
            rootCommand.AddOption(overwriteOption);

            rootCommand.SetHandler(async (source, destination, interval, overwrite) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(source))
                    {
                        _logger.LogError("Source directory is required");
                        Environment.ExitCode = 1;
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(destination))
                    {
                        _logger.LogError("Destination directory is required");
                        Environment.ExitCode = 1;
                        return;
                    }

                    if (!Directory.Exists(source))
                    {
                        _logger.LogError($"Source directory does not exist: {source}");
                        Environment.ExitCode = 1;
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(interval))
                    {
                        TimeSpan intervalTimeSpan;
                        
                        try
                        {
                            intervalTimeSpan = ParseTimeInterval(interval);
                        }
                        catch
                        {
                            if (!int.TryParse(interval, out var minutes) || minutes <= 0)
                            {
                                _logger.LogError($"Invalid interval format: {interval}. Use a number of minutes or time format like 15s, 1h, 1d, 1y");
                                Environment.ExitCode = 1;
                                return;
                            }
                            intervalTimeSpan = TimeSpan.FromMinutes(minutes);
                        }
                        
                        await _scheduler.Start(source, destination, intervalTimeSpan, overwrite);
                        
                        _logger.LogInformation($"Press Ctrl+C to stop the scheduler");
                        
                        using var cts = new CancellationTokenSource();
                        Console.CancelKeyPress += (sender, e) => 
                        {
                            e.Cancel = true; 
                            _logger.LogInformation("Ctrl+C pressed. Stopping scheduler...");
                            _scheduler.Stop();
                            _logger.LogInformation("Scheduler stopped");
                            cts.Cancel();
                        };
                        
                        try
                        {
                            await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            // Task was cancelled, exit gracefully
                        }
                    }
                    else
                    {
                        await _synchronizer.SynchronizeAsync(source, destination, overwrite);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error: {ex.Message}");
                    Environment.ExitCode = 1;
                }
            }, sourceOption, destinationOption, intervalOption, overwriteOption);

            return await rootCommand.InvokeAsync(args);
        }

        /// <summary>
        /// Parses time intervals in formats like "15s", "1h", "1d", "1y"
        /// </summary>
        /// <param name="input">String representation of time interval</param>
        /// <returns>Parsed TimeSpan</returns>
        private TimeSpan ParseTimeInterval(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Time interval cannot be empty", nameof(input));

            var regex = new Regex(@"^(\d+)([smhdy])$", RegexOptions.IgnoreCase);
            var match = regex.Match(input);

            if (!match.Success)
                throw new ArgumentException($"Invalid time interval format: {input}. Expected formats: 15s, 1m, 1h, 1d, 1y", nameof(input));

            int value = int.Parse(match.Groups[1].Value);
            string unit = match.Groups[2].Value.ToLower();

            return unit switch
            {
                "s" => TimeSpan.FromSeconds(value),
                "m" => TimeSpan.FromMinutes(value),
                "h" => TimeSpan.FromHours(value),
                "d" => TimeSpan.FromDays(value),
                "y" => TimeSpan.FromDays(value * 365), 
                _ => throw new ArgumentException($"Unsupported time unit: {unit}", nameof(input))
            };
        }
    }
}