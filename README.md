markdown
# PeriodicFolderSync 🔄

![Version](https://img.shields.io/badge/version-2.2.1-blue)
![Platforms](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)
![License](https://img.shields.io/badge/license-MIT-green)
![.NET Version](https://img.shields.io/badge/.NET-8.0-blueviolet)

Enterprise-grade folder synchronization solution with robust scheduling capabilities.

## 🚀 Quick Setup  

For the fastest installation, use our PowerShell build script:

### Windows
```powershell
git clone https://github.com/DmytroFlakac/PeriodicFolderSync.git
cd PeriodicFolderSync
.\publish-all.ps1 -Windows
.\publish\win-x64\PeriodicFolderSync.exe -s "C:\Source" -d "D:\Backup"
 ```
```

### Linux
```bash
git clone https://github.com/DmytroFlakac/PeriodicFolderSync.git
cd PeriodicFolderSync
pwsh ./publish-all.ps1 -Linux
./publish/linux-x64/PeriodicFolderSync -s "/home/user/docs" -d "/mnt/backup"
 ```
```

### macOS
```bash
git clone https://github.com/DmytroFlakac/PeriodicFolderSync.git
cd PeriodicFolderSync
pwsh ./publish-all.ps1 -Mac
./publish/osx-x64/PeriodicFolderSync -s "/Users/username/Documents" -d "/Volumes/Backup"
 ```
```

## 🛠️ Manual Setup (For Advanced Users)
### Windows
```powershell
git clone https://github.com/DmytroFlakac/PeriodicFolderSync.git
cd PeriodicFolderSync
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
.\publish\win-x64\PeriodicFolderSync.exe -s "C:\Source" -d "D:\Backup"
 ```
```

### Linux
```bash
git clone https://github.com/DmytroFlakac/PeriodicFolderSync.git
cd PeriodicFolderSync
dotnet publish -c Release -r linux-x64 --self-contained true
chmod +x ./publish/linux-x64/PeriodicFolderSync
./publish/linux-x64/PeriodicFolderSync -s "/home/user/docs" -d "/mnt/backup"
 ```
```

### macOS
```bash
git clone https://github.com/DmytroFlakac/PeriodicFolderSync.git
cd PeriodicFolderSync
dotnet publish -c Release -r osx-x64 --self-contained true
chmod +x ./publish/osx-x64/PeriodicFolderSync
./publish/osx-x64/PeriodicFolderSync -s "/Users/username/Documents" -d "/Volumes/Backup"
 ```
```

## 🔧 Build Script Options
The publish-all.ps1 script provides flexible build options:

```powershell
# Build for specific platforms
.\publish-all.ps1 -Windows  # Windows (x64, x86)
.\publish-all.ps1 -Linux    # Linux (x64)
.\publish-all.ps1 -Mac      # macOS (x64, arm64)

# Build for all platforms
.\publish-all.ps1 -All      # All platforms
# or simply
.\publish-all.ps1
 ```

The script performs the following actions:

1. Restores dependencies for all platforms
2. Builds optimized binaries for each target platform
3. Creates self-contained executables that don't require .NET runtime
4. Packages each build into a ZIP archive in the publish directory
## 🌟 Core Features
PeriodicFolderSync offers a comprehensive set of features:

### File Synchronization
- One-way sync : Mirror source to destination
- Incremental sync : Only transfer changed files
### Scheduling
- Periodic sync : Run synchronization at specified intervals
- Flexible time formats : Support for seconds, minutes, hours, days, and years (e.g., 15s, 1m, 1h, 1d, 1y)
- Interactive setup : Guided setup when no arguments are provided
### Security
- Administrator mode : Run with elevated privileges when needed
## 📋 Command Reference
### Basic Commands
```powershell
# Run a one-time sync
.\PeriodicFolderSync.exe -s "C:\Source" -d "D:\Backup"

# Run with administrator privileges
.\PeriodicFolderSync.exe -s "C:\Source" -d "D:\Backup" --admin

# Run periodic sync (every 5 minutes)
.\PeriodicFolderSync.exe -s "C:\Source" -d "D:\Backup" -i 5m

# Run periodic sync (every hour)
.\PeriodicFolderSync.exe -s "C:\Source" -d "D:\Backup" -i 1h
 ```

### Command-Line Options Option Aliases Description --source , -s

Source directory path (required) --destination , -d

Destination directory path (required) --interval , -i

Sync interval (e.g., 15s, 1m, 1h, 1d, 1y) --admin

Run with administrator privileges
### Interactive Mode
If you run the application without arguments, it will prompt you for:

- Source folder path
- Destination folder path
- Sync interval (or "once" for one-time sync)
- Whether to run with administrator privileges
## ⚙️ Configuration Options
### Time Interval Formats
PeriodicFolderSync supports the following time interval formats:
 Format Description Example Ns

N seconds

15s = 15 seconds Nm

N minutes

5m = 5 minutes Nh

N hours

1h = 1 hour Nd

N days

2d = 2 days Ny

N years

1y = 365 days
You can also specify intervals in minutes by providing just a number:

```powershell
.\PeriodicFolderSync.exe -s "C:\Source" -d "D:\Backup" -i 30
 ```

### Stopping the Scheduler
To stop a running scheduler, press Ctrl+C. The application will gracefully shut down.

## 📄 License
MIT License

## 📚 Resources
📖 Technical Docs 🐞 Report Issues 📦 Download Latest Release

