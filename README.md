# Wall-You-Need

A modern wallpaper management application for Windows built with WPF and .NET 7.

## Features

- Manage your wallpaper collection in one place
- Automatically rotate wallpapers on a schedule
- Download wallpapers from Unsplash and Pexels
- Organize wallpapers in custom collections
- Apply wallpapers with a single click
- Beautiful Fluent UI design

## Requirements

- Windows 10 or higher
- .NET 7.0 Runtime
- Internet connection for downloading wallpapers (optional)

## Getting Started

1. Clone the repository
2. Open the solution in Visual Studio 2022
3. Build and run the application

```bash
git clone https://github.com/yourusername/Wall-You-Need.git
cd Wall-You-Need
dotnet build
dotnet run --project src/WallYouNeed/WallYouNeed.App/WallYouNeed.App.csproj
```

## Project Structure

- **WallYouNeed.Core**: Contains the business logic, services, models, and data access
- **WallYouNeed.App**: WPF UI application that consumes the Core library

## Architecture

The application follows a service-based architecture with dependency injection:

- **Services**: Handle business logic and operations
- **Repositories**: Manage data access and storage
- **Models**: Represent domain entities
- **UI**: Composed of pages that consume services

## Development

### Prerequisites

- Visual Studio 2022 or higher
- .NET 7.0 SDK
- Git

### Adding API Keys

To use the Unsplash and Pexels APIs for downloading wallpapers:

1. Sign up for API keys at:
   - [Unsplash Developer](https://unsplash.com/developers)
   - [Pexels API](https://www.pexels.com/api/)
2. Enter your API keys in the Settings page of the application

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- [WPF-UI](https://github.com/lepoco/wpfui) for the Fluent UI controls
- [LiteDB](https://www.litedb.org/) for embedded database
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) for MVVM utilities

# how to prepare?
1. install [.NET 9.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0/runtime?cid=getdotnetcore&os=windows&arch=x64)
2. run the gui and navigate to "API Settings", Enter your API keys/credentials in the provided fields, Click "Save Credentials" and restart the application
3. pip install requirements.txt

# how to use?
1. run gui.py for choosing randomly between manually selected platforms
2. edit config.json using gui or manually as per your needs
3. for wallpaper engine, you must fill save location field with wallpaper engine folder location
4. if you wanna stop the wallpaper engine's "safe start" dialog from popping up, then just uncheck "protect against crashes" checkbox and click ok.

# how to build?
1. pyinstaller Wall-You-Need.spec

# notes
- other programs like John's Background Switcher might interfere with wall-you-need's set lockscreen wallpaper feature, so you should turn off those programs when u use wall-you-need
- looks like images smaller than 500 kb cant be set on lock screens by registry

# Thanks to
https://github.com/oureveryday/WallpaperEngineWorkshopDownloader for acc
https://github.com/oureveryday/DepotDownloaderMod for wallpaper engine downloader

# Workshop Mode:
- Random Selection: Automatically chooses between collection pages and direct wallpaper pages
- Custom URL: Use a specific Steam Workshop URL you provide

# For custom URLs, use either:
- Collection browser pages (steamcommunity.com/workshop/browse/)
- Direct wallpaper links (steamcommunity.com/sharedfiles/filedetails)
