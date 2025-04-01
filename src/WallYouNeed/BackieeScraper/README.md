# Backiee Wallpaper Scraper

This is a C# application that scrapes the Backiee website to extract wallpaper links. It saves the links in a markdown file.

## Prerequisites

- .NET SDK 7.0 or higher
- Internet connection

## How to Build and Run

### Building

Open a command prompt/terminal in the project directory and run:

```bash
# Navigate to the solution directory
cd src/WallYouNeed

# Build the specific project
dotnet build BackieeScraper/BackieeScraper.csproj
```

### Running

After building, you can run the application with:

```bash
# Navigate to the solution directory
cd src/WallYouNeed

# Run the specific project
dotnet run --project BackieeScraper/BackieeScraper.csproj
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

## Integration with Wall-You-Need

This project is part of the Wall-You-Need application and provides functionality to fetch wallpaper links from the Backiee website. These links can be used to download and manage wallpapers within the main application.

## Error Handling

The application includes error handling to catch and display any exceptions that occur during execution.

## How It Works

1. The application sends an HTTP GET request to backiee.com
2. It extracts the HTML content
3. Using regular expressions, it finds wallpaper links and titles
4. If no wallpapers are found in the fresh content, it tries to use the existing backiee_content.html file
5. The links are saved to a markdown file 