using Microsoft.Extensions.Logging;
using PeriodicFolderSync.Interfaces;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace PeriodicFolderSync.Core
{
    public class AdminPrivilegeHandler(ILogger<IAdminPrivilegeHandler> logger) : IAdminPrivilegeHandler
    {
        private readonly ILogger<IAdminPrivilegeHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));


        public bool IsRunningAsAdmin()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Environment.UserName == "root" || 
                       Environment.GetEnvironmentVariable("SUDO_USER") != null;
            }
            
            return false;
        }

        public bool RestartAsAdmin(string?[] args)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return RestartAsAdminWindows(args);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RestartAsAdminUnix(args);
            }
            else
            {
                _logger.LogError("Elevated privileges are not supported on this platform.");
                Console.WriteLine("Elevated privileges are not supported on this platform.");
                return false;
            }
        }

        private bool RestartAsAdminWindows(string?[] args)
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Process.GetCurrentProcess().MainModule?.FileName ?? 
                           throw new InvalidOperationException("Could not determine process filename"),
                Verb = "runas" 
            };
            
            foreach (var arg in args)
            {
                if (!string.IsNullOrEmpty(arg) && !arg.Equals("--admin", StringComparison.OrdinalIgnoreCase))
                {
                    startInfo.ArgumentList.Add(arg);
                }
            }
            
            
            try
            {
                _logger.LogInformation("Restarting application with administrator privileges...");
                var process = Process.Start(startInfo);
                
                if (process != null)
                {
                    Thread.Sleep(500);
                }
                
                Environment.Exit(0); 
                return true; 
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger.LogError($"Administrator privileges were denied: {ex.Message}");
                Console.WriteLine("The operation requires administrator privileges. Please run the program as administrator.");
                return false;
            }
        }

        private bool RestartAsAdminUnix(string?[] args)
        {
            var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(executablePath))
            {
                _logger.LogError("Could not determine process filename");
                Console.WriteLine("Error: Could not determine process filename");
                return false;
            }

            var arguments = new List<string>();
            foreach (var arg in args)
            {
                if (!string.IsNullOrEmpty(arg) && !arg.Equals("--admin", StringComparison.OrdinalIgnoreCase))
                {
                    arguments.Add(arg.Contains(' ') ? $"\"{arg}\"" : arg);
                }
            }
            

            Console.WriteLine("This operation requires elevated privileges.");
            Console.WriteLine("Please enter your password when prompted by the system.");
            
            string elevationCommand = "sudo";
            try
            {
                var checkPkexec = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "pkexec",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                
                using var checkProcess = Process.Start(checkPkexec);
                checkProcess?.WaitForExit();
                if (checkProcess?.ExitCode == 0)
                {
                    elevationCommand = "pkexec";
                }
            }
            catch
            {
                // If 'which' command fails, stick with sudo
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = elevationCommand,
                Arguments = $"\"{executablePath}\" {string.Join(" ", arguments)}",
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory
            };

            try
            {
                _logger.LogInformation($"Restarting application with {elevationCommand} privileges...");
                Process.Start(startInfo);
                
                Thread.Sleep(1000);
                
                Environment.Exit(0);
                return true; 
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to restart with elevated privileges: {ex.Message}");
                Console.WriteLine($"Failed to restart with elevated privileges: {ex.Message}");
                Console.WriteLine("Please try running the application manually with sudo or as root.");
                return false;
            }
        }
    }
}