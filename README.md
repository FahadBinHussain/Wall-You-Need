# Wall-You-Need

<img src="https://wakapi-qt1b.onrender.com/api/badge/fahad/interval:any/project:Wall-You-Need" 
     alt="Wakapi Time Tracking" 
     title="Spent more than that amount of time spent on this project">

A comprehensive wallpaper management solution featuring **three different platform implementations**: WinUI 3, WPF, and Python/Tkinter - all for managing and automating wallpaper downloads and rotation from multiple sources.

## 🚀 Available Platforms

Wall-You-Need is available in three distinct implementations, each with its own strengths:

### 1. **WinUI 3 Application** (Modern Windows)
- ✨ **Latest Windows UI technology** - Modern, fluent design
- 🎨 Native Windows 11 look and feel
- 📍 Location: `winui/`
- **Recommended for**: Windows 11 users who want the latest UI experience

### 2. **WPF Application** (.NET Desktop)
- 🖥️ **Mature Windows desktop framework** - Stable and feature-rich
- 🎭 Custom themes and modern WPF UI library
- 📍 Location: `wpf/`
- **Recommended for**: Windows 10/11 users who prefer traditional desktop apps

### 3. **Python/Tkinter Application**
- 🐍 **Cross-platform Python** - Lightweight and portable
- 🔧 Easy to customize and extend
- 📍 Location: `python/`
- **Recommended for**: Users who want a lightweight solution or need to modify the code

## Features

All platforms support:
- **Multiple Wallpaper Sources**:
  - Unsplash
  - Pexels
  - Wallpaper Engine
  - Backiee
- **Automated Wallpaper Rotation**
- **Customizable Time Intervals**
- **Desktop & Lock Screen Wallpaper Management**
- **Collections Management**
- **Local Wallpaper Storage**
- **Auto-startup Option**
- **Wallpaper Search Functionality**

## 📦 Prerequisites

### For WinUI 3 Application
- Windows 10 (build 19041 or higher) or Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for building)
- [Windows App SDK Runtime 1.7](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads) (auto-included in self-contained builds)

### For WPF Application
- Windows 10 or Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for building)
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (for running pre-built)

### For Python Application
- Python 3.8 or higher
- Tkinter (usually included with Python)
- Required packages (see `python/requirements.txt`)

## 🚀 Installation & Running

### Option 1: WinUI 3 (Modern Windows)
## 🚀 Installation & Running

### Option 1: WinUI 3 (Modern Windows)

**Building and Running:**
```bash
# Navigate to winui directory
cd winui

# Publish with self-contained runtime (Recommended)
dotnet publish -c Release -p:Platform=x64 -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:WindowsAppSDKSelfContained=true -p:UseWinUI=true

# Run the application
.\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\Wall-You-Need.exe
```

**Alternative (Quick Run for Development):**
```bash
cd winui
dotnet run
```
> ⚠️ **Note**: `dotnet run` may fail with "Class not registered" error. Use the publish command above for reliable execution.

---

### Option 2: WPF (.NET Desktop)

**Building and Running:**
```bash
# Navigate to WPF app directory
cd wpf/WallYouNeed.App

# Restore packages (first time only)
dotnet restore

# Build the application
dotnet build

# Run the application
dotnet run
```

**Or open in Visual Studio/Rider:**
1. Open `wpf/WallYouNeed.sln`
2. Set `WallYouNeed.App` as startup project
3. Press F5 to run

---

### Option 3: Python/Tkinter

**Installation:**
```bash
# Navigate to python directory
cd python

# Install required packages
pip install -r requirements.txt
```

**Running:**
```bash
# Run the GUI application
python gui.py
```

## 📁 Project Structure

```
Wall-You-Need/
├── winui/                    # WinUI 3 Application
│   ├── App.xaml             # Application entry point
│   ├── MainWindow.xaml      # Main window UI
│   └── Wall-You-Need.csproj # Project file
│
├── wpf/                      # WPF Application
│   ├── WallYouNeed.App/     # Main WPF application
│   │   ├── Pages/           # UI pages (HomePage, SettingsPage, etc.)
│   │   ├── Services/        # App-specific services
│   │   └── MainWindow.xaml  # Main window
│   └── WallYouNeed.Core/    # Core business logic
│       ├── Services/        # Wallpaper services, scrapers
│       ├── Repositories/    # Data access layer
│       └── Models/          # Data models
│
└── python/                   # Python/Tkinter Application
    ├── gui.py               # Main GUI application
    ├── unsplash.py          # Unsplash API integration
    ├── pexels.py            # Pexels API integration
    ├── wallpaper_engine.py  # Wallpaper Engine integration
    ├── wallpaper_utils.py   # Utility functions
    ├── registry_utils.py    # Windows registry operations
    ├── startup_gui.py       # Auto-startup management
    └── requirements.txt     # Python dependencies
```

## ⚙️ Configuration

Configuration settings are managed through each application's settings interface:

### Key Settings (All Platforms):
- 🌐 Wallpaper sources (Unsplash, Pexels, Wallpaper Engine, Backiee)
- ⏱️ Update interval (rotation frequency)
- 📊 Maximum wallpaper count
- 💾 Save location for downloaded wallpapers
- 🚀 Auto-startup options
- 🎨 Display settings (desktop/lock screen)

## 🛠️ Development

### Building from Source

**WinUI 3:**
```bash
cd winui
dotnet restore
dotnet build -r win-x64
```

**WPF:**
```bash
cd wpf
dotnet restore
dotnet build
```

**Python:**
```bash
cd python
pip install -r requirements.txt
# No build step required for Python
```

### Running Tests
```bash
# For .NET projects
dotnet test

# For Python
python -m pytest
```

## 🤝 Contributing

Contributions are welcome! Whether you prefer working with:
- **WinUI 3** - Modern Windows development
- **WPF** - Traditional .NET desktop
- **Python** - Scripting and automation

Feel free to submit pull requests or open issues.

## 📝 License

This project is licensed under the terms specified in the LICENSE file.

## 🙏 Acknowledgments

- **Wallpaper Sources**: Unsplash, Pexels, Backiee, Wallpaper Engine
- **UI Frameworks**: WinUI 3 (Windows App SDK), WPF UI, ModernWpf, Tkinter
- **.NET**: Microsoft .NET 8.0
- **Python**: Python Software Foundation

## 📧 Support

For issues, questions, or suggestions:
- 🐛 Open an issue on GitHub
- 💬 Check existing discussions
- 📖 Review the documentation in this README

---

**Choose the version that works best for you and enjoy beautiful wallpapers! 🎨🖼️**

The application includes robust error handling for:
- Network connectivity issues
- API rate limiting
- Image file download problems
- File system errors

## License

This project is licensed under the terms included in the LICENSE file.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
