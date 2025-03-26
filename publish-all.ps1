param(
    [switch]$Windows,
    [switch]$Linux,
    [switch]$Mac,
    [switch]$All
)

# Default to all platforms if no flags specified
if (-not ($Windows -or $Linux -or $Mac)) {
    $All = $true
}

# Define platforms based on parameters
$platforms = @()
if ($All -or $Windows) {
    $platforms += "win-x64", "win-x86"
}
if ($All -or $Linux) {
    $platforms += "linux-x64"
}
if ($All -or $Mac) {
    $platforms += "osx-x64", "osx-arm64"
}

# Create output directory
$outputDir = "d:\PeriodicFolderSync\publish"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

Write-Host "Selected platforms: $($platforms -join ', ')" -ForegroundColor Cyan

# First, restore the project with all runtime identifiers
Write-Host "Restoring project dependencies..." -ForegroundColor Cyan
dotnet restore "d:\PeriodicFolderSync\PeriodicFolderSync\PeriodicFolderSync.csproj"

foreach ($platform in $platforms) {
    Write-Host "Publishing for $platform..." -ForegroundColor Green
    
    $platformDir = Join-Path $outputDir $platform
    if (-not (Test-Path $platformDir)) {
        New-Item -ItemType Directory -Path $platformDir | Out-Null
    }
    
    dotnet publish "d:\PeriodicFolderSync\PeriodicFolderSync\PeriodicFolderSync.csproj" -c Release -r $platform `
        -p:PublishSingleFile=true `
        -p:PublishReadyToRun=$($platform.StartsWith("win")) `
        -p:PublishTrimmed=true `
        --self-contained true `
        -o $platformDir
    
    $zipFile = Join-Path $outputDir "PeriodicFolderSync-$platform.zip"
    Compress-Archive -Path "$platformDir\*" -DestinationPath $zipFile -Force
    
    Write-Host "Published to $zipFile" -ForegroundColor Cyan
}

Write-Host "All platforms published successfully!" -ForegroundColor Green