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
        public static async Task Main(string?[] args)
        {
            var services = new ServiceCollection();
            services.AddLogging(config => config.AddConsole());
            var tempProvider = services.BuildServiceProvider();
            var loggerFactory = tempProvider.GetRequiredService<ILoggerFactory>();
            
            var logConfigProvider = new LogConfigurationProvider(loggerFactory);
            string logFilePath = logConfigProvider.GetLogFilePath();
            
            var serviceProvider = ConfigureServices(logConfigProvider, loggerFactory, logFilePath);
            var cliProcessor = serviceProvider.GetRequiredService<ICLIProcessor>();
            
            try
            {
                args = await cliProcessor.GetInteractiveInputIfNeededAsync(args);
                
                if (args.Length == 0)
                {
                    Environment.Exit(1);
                }
                
                var exitCode = await cliProcessor.ProcessAsync(args);
                Environment.Exit(exitCode);
            }
            catch (Exception ex)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<object>>();
                logger.LogError(ex, "An unhandled exception occurred");
                Environment.Exit(-1);
            }
        }

        private static ServiceProvider ConfigureServices(ILogConfigurationProvider logConfigProvider, ILoggerFactory loggerFactory, string logFilePath)
        {
            var services = new ServiceCollection();

            services.AddSingleton(loggerFactory);
            services.AddLogging(config => 
            {
                (loggerFactory as LoggerFactory)?.AddFile(logFilePath);
            });
            
            services.AddSingleton<ILogConfigurationProvider>(logConfigProvider);
            
            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddSingleton<IFolderOperator, FolderOperator>();
            services.AddSingleton<IFileOperator, FileOperator>();
            services.AddSingleton<IMatchStrategy, ContentBasedMatchStrategy>();
            services.AddSingleton<IFileComparer, FileComparer>(); 
            services.AddSingleton<IFolderSynchronizer, FolderSynchronizer>();
            services.AddSingleton<IFileSynchronizer, FileSynchronizer>();
            services.AddSingleton<ISynchronizer, Synchronizer>();
            services.AddSingleton<ICLIProcessor, CLIProcessor>();
            services.AddSingleton<IScheduler, Scheduler>();
            services.AddSingleton<IAdminPrivilegeHandler, AdminPrivilegeHandler>();

            return services.BuildServiceProvider();
        }
    }
}