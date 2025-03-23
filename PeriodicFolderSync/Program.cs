using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PeriodicFolderSync.Core;
using PeriodicFolderSync.Interfaces;
using System;
using System.Threading.Tasks;

namespace PeriodicFolderSync
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var serviceProvider = ConfigureServices();
            var cliProcessor = serviceProvider.GetRequiredService<ICliProcessor>();
            var exitCode = await cliProcessor.ProcessAsync(args);
            Environment.Exit(exitCode);
        }

        private static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Register logging
            services.AddLogging(config => config.AddConsole());

            // Register file system and operators
            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddSingleton<IFolderOperator, FolderOperator>();
            services.AddSingleton<IFileOperator, FileOperator>();

            // Register match strategy
            services.AddSingleton<IMatchStrategy, ContentBasedMatchStrategy>();
            services.AddSingleton<IFileComparer, FileComparer>(); // Add this line
            // Register synchronizers
            services.AddSingleton<IFolderSynchronizer, FolderSynchronizer>();
            services.AddSingleton<IFileSynchronizer, FileSynchronizer>();
            services.AddSingleton<ISynchronizer, Synchronizer>();

            // Register CliProcessor
            services.AddSingleton<ICliProcessor, CliProcessor>();
            services.AddSingleton<IScheduler, Scheduler>();

            return services.BuildServiceProvider();
        }
    }
}