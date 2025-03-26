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
            var serviceProvider = ConfigureServices();
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
            catch (Exception)
            {
                Environment.Exit(-1);
            }
        }

        private static ServiceProvider ConfigureServices()
        {
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
            services.AddSingleton<ICLIProcessor, CLIProcessor>();
            services.AddSingleton<IScheduler, Scheduler>();
            services.AddSingleton<IAdminPrivilegeHandler, AdminPrivilegeHandler>();

            return services.BuildServiceProvider();
        }
    }
}