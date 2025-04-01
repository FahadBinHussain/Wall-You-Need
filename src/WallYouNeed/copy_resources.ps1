# This script copies essential resources to the output directories
# Usage: Run this script after building the application

# Get the script directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path

# Source file paths
$backieeSourcePath = Join-Path -Path $scriptPath -ChildPath "backiee_static_images.md"
Write-Host "Source path: $backieeSourcePath"

if (-not (Test-Path $backieeSourcePath)) {
    Write-Host "Error: backiee_static_images.md file not found at expected location."
    exit 1
}

# Find possible output directories
$outputDirs = @(
    (Join-Path -Path $scriptPath -ChildPath "WallYouNeed.App\bin\Debug\net6.0-windows"),
    (Join-Path -Path $scriptPath -ChildPath "WallYouNeed.App\bin\Release\net6.0-windows")
)

foreach ($outputDir in $outputDirs) {
    if (Test-Path $outputDir) {
        $targetPath = Join-Path -Path $outputDir -ChildPath "backiee_static_images.md"
        Write-Host "Copying to: $targetPath"
        Copy-Item -Path $backieeSourcePath -Destination $targetPath -Force
        Write-Host "File copied successfully."
    } else {
        Write-Host "Output directory not found: $outputDir"
    }
}

Write-Host "Resource copying completed." 