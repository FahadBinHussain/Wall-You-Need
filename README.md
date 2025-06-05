# Wall-You-Need

<img src="https://wakapi-qt1b.onrender.com/api/badge/fahad/interval:any/project:Wall-You-Need" 
     alt="Wakapi Time Tracking" 
     title="Spent more than that amount of time spent on this project">

A comprehensive wallpaper management solution featuring both a WPF desktop application and Python utilities for automating wallpaper downloads and rotation from multiple sources.

## Project Overview

Wall-You-Need is a multi-platform solution for wallpaper enthusiasts with the following components:

1. **.NET/WPF Application**: A modern desktop app for browsing, managing, and applying wallpapers
2. **Python Backend**: Utilities for fetching wallpapers from various sources
3. **Backiee Scraper**: A specialized component for scraping wallpapers from Backiee

## Features

- **Multiple Wallpaper Sources**:
  - Unsplash
  - Pexels
  - Wallpaper Engine
  - Backiee
- **Automated Wallpaper Rotation**
- **Customizable Time Intervals**
- **Desktop & Lock Screen Wallpaper Management**
- **Modern WPF UI with Theme Support**
- **Collections Management**
- **Local Wallpaper Storage**
- **Auto-startup Option**
- **Wallpaper Search Functionality**

## Project Structure

The project is organized into several components:

- **`src/WallYouNeed`**: Main .NET solution
  - **`WallYouNeed.App`**: WPF application with UI
  - **`WallYouNeed.Core`**: Core business logic and services
  - **`BackieeScraper`**: Component for scraping Backiee website

- **`python/`**: Python utilities
  - **`gui.py`**: Python-based GUI for wallpaper management
  - **`unsplash.py`**: Unsplash API integration
  - **`pexels.py`**: Pexels API integration
  - **`wallpaper_engine.py`**: Steam Wallpaper Engine integration
  - **`wallpaper_utils.py`**: Helper utilities for wallpaper operations
  - **`registry_utils.py`**: Windows registry utilities
  - **`startup_gui.py`**: Auto-startup functionality

## Prerequisites

### For Running the app
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-aspnetcore-8.0.14-windows-x64-installer)
- Windows operating system
### For Building the app
- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-8.0.407-windows-x64-installer)
- Windows operating system

### For Python Utilities
- Python 3.8+
- Required Python packages (listed in `python/requirements.txt`)

## Installation

### .NET Application
1. Clone the repository
2. Open the solution in Visual Studio or JetBrains Rider
3. Build the solution:
   ```bash
   dotnet build
   ```
4. Run the application:
   ```bash
   dotnet run --project src/WallYouNeed/WallYouNeed.App/WallYouNeed.App.csproj
   ```

### Python Utilities
1. Install required packages:
   ```bash
   pip install -r python/requirements.txt
   ```
2. Run the Python GUI:
   ```bash
   python python/gui.py
   ```

## Configuration

Configuration settings are managed through the application's settings page or by directly editing configuration files.

### Key Settings:
- Wallpaper sources (Unsplash, Pexels, Wallpaper Engine, Backiee)
- Update interval
- Maximum wallpaper count
- Save location
- Auto-startup options

## Error Handling

The application includes robust error handling for:
- Network connectivity issues
- API rate limiting
- Image file download problems
- File system errors

## License

This project is licensed under the terms included in the LICENSE file.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
