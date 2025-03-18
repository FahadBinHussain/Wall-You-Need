# Wall-You-Need
<img src="https://wakapi-qt1b.onrender.com/api/badge/fahad/interval:any/project:Wall-You-Need" 
     alt="Wakapi Time Tracking" 
     title="Minimum amount of time spent on this project">

tldr;
download wallpapers from pexels, unsplash and wallpaper engine steam workshop and set random wallpaper every X minutes 
<br>
more features coming soon!

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
