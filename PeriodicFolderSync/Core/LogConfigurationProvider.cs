using Microsoft.Extensions.Logging;
using PeriodicFolderSync.Interfaces;
using System;
using System.IO;

namespace PeriodicFolderSync.Core
{
    public class LogConfigurationProvider(ILoggerFactory loggerFactory) : ILogConfigurationProvider
    {
        private string _logFilePath = "logs/foldersync.log";
        private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        private bool _customPathSet = false;

        public string GetLogFilePath()
        {
            return _logFilePath;
        }

        public void SetLogFilePath(string path)
        {
            
            if (!string.IsNullOrWhiteSpace(path))
            {
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                _logFilePath = path;
                _customPathSet = true;
                
                (_loggerFactory as LoggerFactory)?.AddFile(path);
            }
        }
        
        public string CreateDynamicLogFileName(string source, string destination)
        {
            if (_customPathSet)
            {
                return _logFilePath;
            }
            
            string sourceName = Path.GetFileName(source.TrimEnd('\\', '/'));
            string destName = Path.GetFileName(destination.TrimEnd('\\', '/'));
            
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string logFileName = $"logs/sync_{sourceName}_to_{destName}_{timestamp}.log";
            
            if (!Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }
            
            _logFilePath = logFileName;
            (_loggerFactory as LoggerFactory)?.AddFile(logFileName);
            
            return logFileName;
        }
    }
}