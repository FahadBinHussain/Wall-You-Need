# Wall-You-Need

![Wakapi Badge](https://wakapi-qt1b.onrender.com/api/badge/fahad/interval:any/project:Wall-You-Need)

tldr;
download wallpapers from pexels, unsplash and wallpaper engine steam workshop and set random wallpaper every X minutes 
<br>
more features coming soon!

# how to prepare?
1. Please install [.NET 9.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0/runtime?cid=getdotnetcore&os=windows&arch=x64) first.
2. add your own api keys in .env
3. pip install requirements.txt


# how to use?
1. run main.py for choosing randomly between all platforms
2. run gui.py for choosing randomly between manually selected platforms
3. edit config.json using gui or manually as per your needs
4. must edit save location with folder location where you want to save wallpapers


# how to build?

1. pyinstaller Wall-You-Need.spec

# notes

- other programs like John’s Background Switcher might interfere with wall-you-need’s set lockscreen wallpaper feature, so you should turn off those programs when u use wall-you-need
- looks like images smaller than 500 kb cant be set on lock screens by registry

# Thanks to
https://github.com/oureveryday/WallpaperEngineWorkshopDownloader for acc
https://github.com/oureveryday/DepotDownloaderMod for wallpaper engine downloader
