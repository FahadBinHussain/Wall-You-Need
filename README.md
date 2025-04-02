# Backiee Wallpaper Scraper

<img src="https://wakapi-qt1b.onrender.com/api/badge/fahad/interval:any/project:Wall-You-Need" 
     alt="Wakapi Time Tracking" 
     title="Minimum amount of time spent on this project">

This is a C# application that scrapes the Backiee website to extract wallpaper links. It saves the links in a markdown file.

## Prerequisites

- .NET SDK 7.0 or higher
- Internet connection

## How to Build and Run

### Building

Open a command prompt/terminal in the project directory and run:

```bash
dotnet build
```

### Running

After building, you can run the application with:

```bash
dotnet run
```

## Features

- Fetches the latest wallpapers from backiee.com
- Extracts up to 20 wallpaper links
- Saves links to a markdown file (backiee_wallpapers.md)
- Includes fallback mechanisms if the main extraction method fails
- Saves raw HTML for debugging purposes

## Output

The program will create:
1. `backiee_latest.html` - The raw HTML from the website
2. `backiee_wallpapers.md` - Markdown file with wallpaper links

## Error Handling

The application includes error handling to catch and display any exceptions that occur during execution.

## How It Works

1. The application sends an HTTP GET request to backiee.com
2. It extracts the HTML content
3. Using regular expressions, it finds wallpaper links and titles
4. If no wallpapers are found in the fresh content, it tries to use the existing backiee_content.html file
5. The links are saved to a markdown file
