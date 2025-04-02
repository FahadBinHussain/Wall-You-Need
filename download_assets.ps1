# Ensure we're in the right directory
Set-Location $PSScriptRoot

$assetsPath = "src\WallYouNeed\WallYouNeed.App\Assets"

# Create assets directory if it doesn't exist
if (-not (Test-Path $assetsPath)) {
    Write-Host "Creating directory: $assetsPath"
    New-Item -ItemType Directory -Path $assetsPath -Force
}

# Download the images
$webClient = New-Object System.Net.WebClient

Write-Host "Downloading assets to $((Get-Item $assetsPath).FullName)..."

# Download 5K logo
try {
    Write-Host "Downloading 5k_logo.png..."
    $webClient.DownloadFile(
        "https://backiee.com/assets/img/5k_logo.png",
        (Join-Path $assetsPath "5k_logo.png")
    )
    Write-Host "Successfully downloaded 5k_logo.png"
} catch {
    Write-Host "Error downloading 5k_logo.png: $_"
}

# Download AI generated icon
try {
    Write-Host "Downloading aigenerated-icon.png..."
    $webClient.DownloadFile(
        "https://backiee.com/assets/img/aigenerated-icon.png",
        (Join-Path $assetsPath "aigenerated-icon.png")
    )
    Write-Host "Successfully downloaded aigenerated-icon.png"
} catch {
    Write-Host "Error downloading aigenerated-icon.png: $_"
}

# Verify files exist
$files = @("5k_logo.png", "aigenerated-icon.png")
foreach ($file in $files) {
    $path = Join-Path $assetsPath $file
    if (Test-Path $path) {
        Write-Host "Verified: $file exists at $path"
    } else {
        Write-Host "WARNING: $file not found at $path"
    }
}

Write-Host "Assets download completed!" 