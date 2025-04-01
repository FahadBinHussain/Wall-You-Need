using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WallYouNeed.Core.Configuration;
using WallYouNeed.Core.Models;
using WallYouNeed.Core.Services.Interfaces;
using WallYouNeed.Core.Utils;
using System.IO;
using HtmlAgilityPack;
using System.Net;
using System.Collections.Concurrent;
using System.Text;

namespace WallYouNeed.Core.Services
{
    public class BackieeScraperService : IBackieeScraperService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BackieeScraperService> _logger;
        private readonly IWallpaperConfigurationService _configService;
        private readonly HtmlDownloader _htmlDownloader;
        private Timer _timer;
        private bool _isScrapingInProgress;
        private readonly object _lock = new object();
        
        // Circuit breaker pattern implementation
        private int _consecutiveFailures = 0;
        private DateTime _circuitOpenUntil = DateTime.MinValue;
        private const int MaxConsecutiveFailures = 5;
        private const int CircuitBreakTimeMinutes = 15;

        // Cache recently successful wallpaper IDs to minimize redundant scraping
        private readonly ConcurrentDictionary<string, DateTime> _recentlyScrapedIds = new ConcurrentDictionary<string, DateTime>();
        
        // Keep track of HTML structure changes
        private string _lastSuccessfulHtmlStructureHash;
        
        // File-based logging
        private readonly string _logFilePath;

        public event EventHandler<List<WallpaperModel>> NewWallpapersAdded;

        public BackieeScraperService(
            HttpClient httpClient,
            ILogger<BackieeScraperService> logger,
            IWallpaperConfigurationService configService,
            HtmlDownloader htmlDownloader)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configService = configService;
            _htmlDownloader = htmlDownloader;
            
            // Set up file-based logging in project directory
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backiee_scraper.log");
            LogToFile("BackieeScraperService initialized");
        }

        /// <summary>
        /// Writes a message to the log file
        /// </summary>
        private void LogToFile(string message)
        {
            try
            {
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";
                
                // Ensure log file doesn't get too large
                if (File.Exists(_logFilePath) && new FileInfo(_logFilePath).Length > 5 * 1024 * 1024) // 5MB
                {
                    // Append to beginning of file and truncate if it gets too large
                    string existingContent = File.ReadAllText(_logFilePath);
                    string truncatedContent = existingContent.Substring(0, Math.Min(existingContent.Length, 1024 * 1024)); // Keep last 1MB
                    File.WriteAllText(_logFilePath, truncatedContent);
                }
                
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore errors when writing to log file
            }
        }

        /// <summary>
        /// Starts the timer to periodically scrape wallpapers
        /// </summary>
        public async Task StartPeriodicUpdates()
        {
            var config = await _configService.GetBackieeConfigAsync();
            
            lock (_lock)
            {
                if (_timer == null)
                {
                    // Dispose any existing timer
                    _timer?.Dispose();
                    
                    // Use a more reliable Timer setup
                    var callbackHandler = new TimerCallback(async state => 
                    {
                        try 
                        {
                            // Don't queue up multiple scrape operations
                            if (_isScrapingInProgress)
                            {
                                _logger.LogInformation("Skipping scrape operation because previous one is still running");
                                return;
                            }
                            
                            await ScrapeLatestWallpapers();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in timer callback for scraping");
                        }
                    });
                    
                    // Start with a delay to avoid immediate scraping on startup
                    _timer = new Timer(
                        callbackHandler, 
                        null, 
                        TimeSpan.FromSeconds(10), // Initial delay
                        TimeSpan.FromMilliseconds(config.ScrapingInterval)); // Recurring interval
                    
                    _logger.LogInformation("Backiee scraper periodic updates started with interval of {Interval}ms", config.ScrapingInterval);
                }
            }
        }

        /// <summary>
        /// Stops the periodic updates
        /// </summary>
        public void StopPeriodicUpdates()
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
                _logger.LogInformation("Backiee scraper periodic updates stopped");
            }
        }

        /// <summary>
        /// Scrapes the latest wallpapers from the homepage with smart retry logic
        /// </summary>
        public async Task<List<WallpaperModel>> ScrapeLatestWallpapers()
        {
            // Use a CancellationTokenSource with timeout to prevent hanging
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // Hard 30-second timeout
            
            // Log that we're starting a scrape attempt
            _logger.LogInformation("Starting to scrape latest wallpapers");
            
            // Initialize an empty list for wallpapers
            var wallpapers = new List<WallpaperModel>();
            
            try 
            {
                // Try to scrape from backiee.com first
                wallpapers = await TryBackieeScraping(cts.Token, tryUnsplashFallback: true);
                
                // Raise event for any wallpapers found
                if (wallpapers.Count > 0)
                {
                    // Raise the event with the new wallpapers
                    NewWallpapersAdded?.Invoke(this, wallpapers);
                }
                
                return wallpapers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping latest wallpapers. Using fallback images.");
                return await GetFallbackWallpapers(10);
            }
        }
        
        /// <summary>
        /// Attempts to scrape from backiee.com with all the original logic
        /// </summary>
        private async Task<List<WallpaperModel>> TryBackieeScraping(CancellationToken ct, bool tryUnsplashFallback = false)
        {
            // Circuit breaker check
            if (DateTime.Now < _circuitOpenUntil)
            {
                _logger.LogWarning("Circuit breaker open until {Time}, skipping scraping", _circuitOpenUntil);
                return tryUnsplashFallback ? await GetFallbackWallpapers(10) : new List<WallpaperModel>();
            }

            // Prevent concurrent scraping
            if (_isScrapingInProgress)
            {
                _logger.LogInformation("Scraping is already in progress, skipping this request");
                return tryUnsplashFallback ? await GetFallbackWallpapers(10) : new List<WallpaperModel>();
            }

            // Use a thread-safe way to set the flag
            lock (_lock)
            {
                if (_isScrapingInProgress)
                    return tryUnsplashFallback ? GetFallbackWallpapers(10).Result : new List<WallpaperModel>();
                _isScrapingInProgress = true;
            }

            var wallpapers = new List<WallpaperModel>();

            try
            {
                // Get a list of known good IDs to try
                var knownWorkingIds = new List<string>
                {
                    "318542", "318541", "318540", "318539", "318538",
                    "318137", "318124", "318123", "318122", "318116"
                };
                
                // Try direct wallpaper creation first - fastest method
                foreach (var id in knownWorkingIds.Take(3))
                {
                    // Fast direct creation without HTML
                    var wallpaper = CreateDirectWallpaper(id);
                    if (wallpaper != null && await _htmlDownloader.VerifyImageUrl(wallpaper.ImageUrl))
                    {
                        _logger.LogInformation("Successfully created wallpaper directly with ID {Id}", id);
                        wallpapers.Add(wallpaper);
                    }
                }
                
                // If we got some valid wallpapers directly, return them
                if (wallpapers.Count >= 3)
                {
                    _consecutiveFailures = 0; // Reset failure counter
                    _logger.LogInformation("Successfully got {Count} wallpapers using direct creation", wallpapers.Count);
                    return wallpapers;
                }
                
                // If direct creation didn't yield enough results, try extraction
                _logger.LogInformation("Direct creation yielded only {Count} wallpapers, trying extraction", wallpapers.Count);
                
                // Try to extract wallpapers by direct IDs as the second approach
                var extractedWallpapers = await ExtractWallpapersByDirectIds(knownWorkingIds);
                if (extractedWallpapers.Any())
                {
                    wallpapers.AddRange(extractedWallpapers);
                    _logger.LogInformation("Added {Count} wallpapers from extraction", extractedWallpapers.Count);
                }
                
                if (wallpapers.Count < 3 && tryUnsplashFallback)
                {
                    _logger.LogWarning("Extraction yielded only {Count} wallpapers. Using fallback.", wallpapers.Count);
                    int needed = 10 - wallpapers.Count;
                    
                    if (needed > 0)
                    {
                        var fallbackWallpapers = await GetFallbackWallpapers(needed);
                        wallpapers.AddRange(fallbackWallpapers);
                        _logger.LogInformation("Added {Count} fallback wallpapers", fallbackWallpapers.Count);
                    }
                }
                
                // If we were able to get wallpapers, reset the consecutive failures
                if (wallpapers.Count > 0)
                {
                    _consecutiveFailures = 0;
                }
                else
                {
                    _consecutiveFailures++;
                    _logger.LogWarning("No wallpapers found. Consecutive failures: {Count}", _consecutiveFailures);
                    
                    if (_consecutiveFailures >= MaxConsecutiveFailures)
                    {
                        _circuitOpenUntil = DateTime.Now.AddMinutes(CircuitBreakTimeMinutes);
                        _logger.LogWarning("Circuit breaker opened until {DateTime} after {Failures} consecutive failures", 
                            _circuitOpenUntil, _consecutiveFailures);
                            
                        if (tryUnsplashFallback)
                        {
                            var fallbackWallpapers = await GetFallbackWallpapers(10);
                            wallpapers.AddRange(fallbackWallpapers);
                            _logger.LogInformation("Added {Count} fallback wallpapers via circuit breaker", fallbackWallpapers.Count);
                        }
                    }
                }
                
                return wallpapers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TryBackieeScraping: {Message}", ex.Message);
                _consecutiveFailures++;
                
                if (_consecutiveFailures >= MaxConsecutiveFailures)
                {
                    _circuitOpenUntil = DateTime.Now.AddMinutes(CircuitBreakTimeMinutes);
                    _logger.LogWarning("Circuit breaker opened until {DateTime} after {Failures} consecutive failures", 
                        _circuitOpenUntil, _consecutiveFailures);
                }
                
                if (tryUnsplashFallback)
                {
                    return await GetFallbackWallpapers(10);
                }
                
                return new List<WallpaperModel>();
            }
            finally
            {
                _isScrapingInProgress = false;
            }
        }
        
        /// <summary>
        /// Gets high-quality fallback wallpapers from Unsplash
        /// </summary>
        private List<WallpaperModel> GetUnsplashFallbackWallpapers(int count)
        {
            var wallpapers = new List<WallpaperModel>();
            var random = new Random();

            // Stable list of high-quality Unsplash wallpapers by category
            var unsplashWallpapers = new Dictionary<string, List<(string Url, string Title)>>
            {
                ["Nature"] = new List<(string, string)>
                {
                    ("https://images.unsplash.com/photo-1506744038136-46273834b3fb", "Beautiful Mountain Landscape"),
                    ("https://images.unsplash.com/photo-1494500764479-0c8f2919a3d8", "Starry Night Sky"),
                    ("https://images.unsplash.com/photo-1511300636408-a63a89df3482", "Peaceful Forest"),
                    ("https://images.unsplash.com/photo-1497436072909-60f360e1d4b1", "Green Mountains"),
                    ("https://images.unsplash.com/photo-1507525428034-b723cf961d3e", "Serene Beach")
                },
                ["City"] = new List<(string, string)>
                {
                    ("https://images.unsplash.com/photo-1518391846015-55a9cc003b25", "Tokyo Street"),
                    ("https://images.unsplash.com/photo-1480714378408-67cf0d13bc1b", "New York City"),
                    ("https://images.unsplash.com/photo-1449824913935-59a10b8d2000", "City Sunset"),
                    ("https://images.unsplash.com/photo-1444723121867-7a241cacace9", "Chicago"),
                    ("https://images.unsplash.com/photo-1514565131-fce0801e5785", "London Bridge")
                },
                ["Abstract"] = new List<(string, string)>
                {
                    ("https://images.unsplash.com/photo-1541701494587-cb58502866ab", "Abstract Colors"),
                    ("https://images.unsplash.com/photo-1523821741446-edb2b68bb7a0", "Colorful Smoke"),
                    ("https://images.unsplash.com/photo-1507608616759-54f48f0af0ee", "Geometric Patterns"),
                    ("https://images.unsplash.com/photo-1486520299386-6d106b22014b", "Blue Wave"),
                    ("https://images.unsplash.com/photo-1508614999368-9260051292e5", "Neon Lights")
                },
                ["Tech"] = new List<(string, string)>
                {
                    ("https://images.unsplash.com/photo-1518770660439-4636190af475", "Tech Hardware"),
                    ("https://images.unsplash.com/photo-1526374965328-7f61d4dc18c5", "Code Screen"),
                    ("https://images.unsplash.com/photo-1496065187959-7f07b8353c55", "Laptop on Desk"),
                    ("https://images.unsplash.com/photo-1460925895917-afdab827c52f", "Website Design"),
                    ("https://images.unsplash.com/photo-1517433367423-c7e5b0f35086", "Coding")
                }
            };

            // Generate a list of wallpapers to return
            var categories = unsplashWallpapers.Keys.ToList();
            var addedWallpapers = 0;
            
            for (int i = 0; i < count; i++)
            {
                string category = categories[random.Next(categories.Count)];
                var wallpaperList = unsplashWallpapers[category];
                var (imageUrl, title) = wallpaperList[random.Next(wallpaperList.Count)];
                
                var wallpaper = new WallpaperModel
                {
                    Title = title,
                    ImageUrl = imageUrl,
                    ThumbnailUrl = imageUrl,
                    Source = "Unsplash (Fallback)",
                    SourceUrl = "https://unsplash.com",
                    Width = 1920,
                    Height = 1080,
                    Category = category,
                    Rating = 5
                };
                
                wallpapers.Add(wallpaper);
                addedWallpapers++;
                
                if (addedWallpapers >= count)
                    break;
            }
            
            return wallpapers;
        }

        /// <summary>
        /// Scrapes wallpapers for a specific category
        /// </summary>
        /// <param name="category">The category to scrape</param>
        /// <param name="maxPages">Maximum number of pages to scrape</param>
        public async Task<List<WallpaperModel>> ScrapeWallpapersByCategory(string category, int maxPages = 3)
        {
            try
            {
                var config = await _configService.GetBackieeConfigAsync();
                var wallpapers = new List<WallpaperModel>();
                
                _logger.LogInformation("Scraping wallpapers for category: {Category}", category);
                
                for (int page = 1; page <= maxPages; page++)
                {
                    string url = $"{config.BaseUrl}/category/{category}/page/{page}";
                    
                    _logger.LogDebug("Scraping page {Page} of {Category}", page, category);
                    
                    var pageWallpapers = await ScrapeWallpaperPage(url);
                    
                    if (!pageWallpapers.Any())
                    {
                        _logger.LogDebug("No more wallpapers found for category {Category} at page {Page}", category, page);
                        break;
                    }
                    
                    wallpapers.AddRange(pageWallpapers);
                    
                    // Add small delay to avoid overloading the server
                    await Task.Delay(config.RequestDelayMs);
                }
                
                _logger.LogInformation("Found {Count} wallpapers for category {Category}", wallpapers.Count, category);
                
                return wallpapers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping wallpapers for category: {Category}", category);
                return new List<WallpaperModel>();
            }
        }
        
        /// <summary>
        /// Scrapes a single page of wallpapers with timeout safeguards
        /// </summary>
        private async Task<List<WallpaperModel>> ScrapeWallpaperPage(string url, string category = null)
        {
            var wallpapers = new List<WallpaperModel>();
            
            // Add a timeout to prevent hanging
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(20)); // 20-second timeout for page scraping
            
            try
            {
                _logger.LogInformation("Starting to scrape page: {Url}", url);
                
                // Download HTML with timeout
                var downloadTask = _htmlDownloader.DownloadHtmlAsync(url);
                var completedTask = await Task.WhenAny(
                    downloadTask,
                    Task.Delay(10000, cts.Token) // 10 second timeout
                );
                
                if (completedTask != downloadTask)
                {
                    _logger.LogWarning("HTML download timed out for URL: {Url}", url);
                    return wallpapers;
                }
                
                var html = await downloadTask;
                
                if (string.IsNullOrEmpty(html))
                {
                    _logger.LogWarning("Downloaded HTML is empty for URL: {Url}", url);
                    return wallpapers;
                }
                
                _logger.LogDebug("Successfully downloaded HTML content of length: {Length}", html.Length);
                
                // Get all wallpaper items
                var findElementsTask = _htmlDownloader.FindElementsAsync(url, "//div[contains(@class, 'item')]");
                var elementsCompletedTask = await Task.WhenAny(
                    findElementsTask,
                    Task.Delay(5000, cts.Token) // 5 second timeout
                );
                
                if (elementsCompletedTask != findElementsTask)
                {
                    _logger.LogWarning("Finding elements timed out for URL: {Url}", url);
                    // Try direct extraction as a fallback
                    return await ExtractWallpapersDirectlyFromHtml(html, url, category);
                }
                
                var wallpaperElements = await findElementsTask;
                
                _logger.LogInformation("Found {Count} wallpaper elements on page", wallpaperElements.Count);
                
                foreach (var element in wallpaperElements)
                {
                    try
                    {
                        // Extract data
                        string title = _htmlDownloader.ExtractTextFromElement(element, "h3");
                        if (string.IsNullOrEmpty(title))
                        {
                            // Try alternative heading elements
                            title = _htmlDownloader.ExtractTextFromElement(element, "h2");
                            if (string.IsNullOrEmpty(title))
                            {
                                title = _htmlDownloader.ExtractTextFromElement(element, "h4");
                                if (string.IsNullOrEmpty(title))
                                {
                                    // Try looking for a title attribute
                                    title = _htmlDownloader.ExtractAttributeFromElement(element, "title");
                                    if (string.IsNullOrEmpty(title))
                                    {
                                        // Use alt text from image as fallback
                                        title = _htmlDownloader.ExtractAttributeFromElement(element, "alt");
                                        if (string.IsNullOrEmpty(title))
                                        {
                                            title = "Untitled Wallpaper";
                                        }
                                    }
                                }
                            }
                        }
                        
                        string detailUrl = _htmlDownloader.ExtractAttributeFromElement(element, "href");
                        string thumbnailUrl = _htmlDownloader.ExtractAttributeFromElement(element, "src");
                        
                        // Try to find nested <a> tag if href not found at this level
                        if (string.IsNullOrEmpty(detailUrl))
                        {
                            var aTagMatch = Regex.Match(element, "<a[^>]*href\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>");
                            if (aTagMatch.Success && aTagMatch.Groups.Count > 1)
                            {
                                detailUrl = aTagMatch.Groups[1].Value;
                            }
                        }
                        
                        // Try to find nested <img> tag if src not found at this level
                        if (string.IsNullOrEmpty(thumbnailUrl))
                        {
                            var imgTagMatch = Regex.Match(element, "<img[^>]*src\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>");
                            if (imgTagMatch.Success && imgTagMatch.Groups.Count > 1)
                            {
                                thumbnailUrl = imgTagMatch.Groups[1].Value;
                            }
                            
                            // Look for data-src attribute for lazy-loaded images (common in modern sites)
                            if (string.IsNullOrEmpty(thumbnailUrl) || thumbnailUrl.Contains("placeholder"))
                            {
                                var dataSrcMatch = Regex.Match(element, "<img[^>]*data-src\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>");
                                if (dataSrcMatch.Success && dataSrcMatch.Groups.Count > 1)
                                {
                                    thumbnailUrl = dataSrcMatch.Groups[1].Value;
                                    _logger.LogDebug("Found image URL in data-src attribute: {Url}", thumbnailUrl);
                                }
                            }
                        }
                        
                        if (string.IsNullOrEmpty(detailUrl) || string.IsNullOrEmpty(thumbnailUrl))
                        {
                            _logger.LogWarning("Missing required data for wallpaper: detailUrl={DetailUrl}, thumbnailUrl={ThumbnailUrl}", 
                                detailUrl, thumbnailUrl);
                            continue;
                        }
                        
                        // Create URL with base if it's relative
                        if (!detailUrl.StartsWith("http"))
                        {
                            var config = await _configService.GetBackieeConfigAsync();
                            detailUrl = $"{config.BaseUrl.TrimEnd('/')}/{detailUrl.TrimStart('/')}";
                        }
                        
                        // Extract wallpaper ID from detail URL
                        string wallpaperId = string.Empty;
                        var wallpaperIdMatch = Regex.Match(detailUrl, "/wallpaper/[^/]+/(\\d+)");
                        if (wallpaperIdMatch.Success && wallpaperIdMatch.Groups.Count > 1)
                        {
                            wallpaperId = wallpaperIdMatch.Groups[1].Value;
                            _logger.LogDebug("Extracted wallpaper ID: {WallpaperId}", wallpaperId);
                        }
                        
                        // Same for thumbnail URL
                        if (!thumbnailUrl.StartsWith("http"))
                        {
                            var config = await _configService.GetBackieeConfigAsync();
                            thumbnailUrl = $"{config.BaseUrl.TrimEnd('/')}/{thumbnailUrl.TrimStart('/')}";
                        }
                        
                        // Extract resolution from title
                        var match = Regex.Match(title, @"(\d+)\s*[xX]\s*(\d+)");
                        int width = 0, height = 0;
                        
                        if (match.Success && match.Groups.Count >= 3)
                        {
                            int.TryParse(match.Groups[1].Value, out width);
                            int.TryParse(match.Groups[2].Value, out height);
                        }
                        
                        // Default resolution if not found
                        if (width == 0 || height == 0)
                        {
                            width = 1920;
                            height = 1080;
                        }
                        
                        // Determine resolution category
                        string resolutionCategory = DetermineResolutionCategory(width, height);
                        
                        // Try to construct image URL directly from thumbnail URL
                        string imageUrl = string.Empty;
                        
                        // If we have a wallpaper ID, try to construct the full-size image URL
                        if (!string.IsNullOrEmpty(wallpaperId))
                        {
                            // Common pattern for backiee.com full wallpaper URLs
                            imageUrl = $"https://backiee.com/static/wallpapers/wide/{wallpaperId}.jpg";
                            _logger.LogDebug("Constructed potential image URL from wallpaper ID: {ImageUrl}", imageUrl);
                        }
                        
                        // Extract image URL from detail page if we don't have a direct construction
                        if (string.IsNullOrEmpty(imageUrl))
                        {
                            imageUrl = await ExtractImageUrlFromDetailPage(detailUrl);
                        }
                        
                        if (string.IsNullOrEmpty(imageUrl))
                        {
                            _logger.LogWarning("Could not extract image URL from detail page: {DetailUrl}", detailUrl);
                            
                            // Try to use thumbnail as fallback (potentially enlarging it)
                            if (thumbnailUrl.Contains("/static/wallpapers/") && !thumbnailUrl.Contains("placeholder"))
                            {
                                // Examples: 
                                // /static/wallpapers/560x315/418137.jpg -> /static/wallpapers/wide/418137.jpg
                                // /static/wallpapers/thumb/418137.jpg -> /static/wallpapers/wide/418137.jpg
                                
                                var idMatch = Regex.Match(thumbnailUrl, "/static/wallpapers/(?:[^/]+/)?([\\d]+)\\.jpg");
                                if (idMatch.Success && idMatch.Groups.Count > 1)
                                {
                                    string id = idMatch.Groups[1].Value;
                                    imageUrl = $"https://backiee.com/static/wallpapers/wide/{id}.jpg";
                                    _logger.LogDebug("Constructed image URL from thumbnail pattern: {ImageUrl}", imageUrl);
                                }
                                else
                                {
                                    imageUrl = thumbnailUrl.Replace("560x315", "wide").Replace("thumb", "wide");
                                    _logger.LogDebug("Modified thumbnail URL to get full image: {ImageUrl}", imageUrl);
                                }
                            }
                            else
                            {
                                imageUrl = thumbnailUrl;
                                _logger.LogDebug("Using thumbnail URL as fallback for image URL: {ImageUrl}", imageUrl);
                            }
                            
                            if (string.IsNullOrEmpty(imageUrl))
                            {
                                _logger.LogWarning("No viable image URL found, skipping wallpaper");
                                continue;
                            }
                        }
                        
                        var wallpaper = new WallpaperModel
                        {
                            Title = title,
                            Category = category ?? "Latest",
                            ResolutionCategory = resolutionCategory,
                            ThumbnailUrl = thumbnailUrl,
                            ImageUrl = imageUrl,
                            SourceUrl = detailUrl,
                            Source = "Backiee",
                            Width = width,
                            Height = height,
                            UploadDate = DateTime.Now // The site doesn't provide upload date, so we use current time
                        };
                        
                        _logger.LogDebug("Successfully scraped wallpaper: {Title}", title);
                        wallpapers.Add(wallpaper);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing wallpaper item");
                    }
                }
                
                _logger.LogInformation("Found {Count} wallpapers on page", wallpapers.Count);
                
                // Save diagnostic information about found wallpapers
                try
                {
                    if (wallpapers.Any())
                    {
                        string logDir = Path.Combine(Path.GetTempPath(), "WallYouNeed_Logs");
                        Directory.CreateDirectory(logDir);
                        string filename = $"found_wallpapers_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileNameWithoutExtension(url)}.txt";
                        string fullPath = Path.Combine(logDir, filename);
                        
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine($"Found {wallpapers.Count} wallpapers from {url} at {DateTime.Now}");
                        
                        foreach (var wallpaper in wallpapers)
                        {
                            sb.AppendLine($"Title: {wallpaper.Title}");
                            sb.AppendLine($"Category: {wallpaper.Category}");
                            sb.AppendLine($"ThumbnailUrl: {wallpaper.ThumbnailUrl}");
                            sb.AppendLine($"ImageUrl: {wallpaper.ImageUrl}");
                            sb.AppendLine($"SourceUrl: {wallpaper.SourceUrl}");
                            sb.AppendLine($"Resolution: {wallpaper.Width}x{wallpaper.Height} ({wallpaper.ResolutionCategory})");
                            sb.AppendLine();
                        }
                        
                        File.WriteAllText(fullPath, sb.ToString());
                        _logger.LogInformation("Saved wallpaper details to: {Path}", fullPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save wallpaper details");
                }
                
                return wallpapers;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Page scraping was cancelled due to timeout for URL: {Url}", url);
                return wallpapers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping wallpaper page: {Url}", url);
                return wallpapers;
            }
        }
        
        /// <summary>
        /// Extracts the full resolution image URL from the detail page
        /// </summary>
        private async Task<string> ExtractImageUrlFromDetailPage(string detailUrl)
        {
            try
            {
                _logger.LogDebug("Extracting image URL from detail page: {DetailUrl}", detailUrl);
                
                var config = await _configService.GetBackieeConfigAsync();
                
                // Check if we're being rate limited - add delay if needed
                await Task.Delay(config.RequestDelayMs);
                
                // If we see an image URL directly in the detail URL, use it (common for direct image links)
                if (detailUrl.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                    detailUrl.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || 
                    detailUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Detail URL appears to be a direct image URL: {ImageUrl}", detailUrl);
                    return detailUrl;
                }
                
                // Try to extract image ID from detail URL
                var wallpaperIdMatch = Regex.Match(detailUrl, "/wallpaper/[^/]+/(\\d+)");
                if (wallpaperIdMatch.Success && wallpaperIdMatch.Groups.Count > 1)
                {
                    string wallpaperId = wallpaperIdMatch.Groups[1].Value;
                    string constructedUrl = $"https://backiee.com/static/wallpapers/wide/{wallpaperId}.jpg";
                    _logger.LogDebug("Directly constructed image URL from wallpaper ID: {ImageUrl}", constructedUrl);
                    
                    // Verify this URL exists 
                    try
                    {
                        var request = new HttpRequestMessage(HttpMethod.Head, constructedUrl);
                        var response = await _httpClient.SendAsync(request);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Successfully verified constructed image URL: {ImageUrl}", constructedUrl);
                            return constructedUrl;
                        }
                        else
                        {
                            _logger.LogDebug("Constructed URL returned status code {StatusCode}: {ImageUrl}", 
                                response.StatusCode, constructedUrl);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error verifying constructed URL: {ImageUrl}", constructedUrl);
                    }
                }
                
                // Try to get the image URL from OpenGraph meta tags first
                string ogImageUrl = await _htmlDownloader.ExtractAttributeAsync(
                    detailUrl, 
                    "//meta[@property='og:image']", 
                    "content");
                
                if (!string.IsNullOrEmpty(ogImageUrl))
                {
                    _logger.LogDebug("Found image URL from og:image meta tag: {ImageUrl}", ogImageUrl);
                    return ogImageUrl;
                }
                
                // Also try twitter:image which is often used
                string twitterImageUrl = await _htmlDownloader.ExtractAttributeAsync(
                    detailUrl,
                    "//meta[@name='twitter:image']",
                    "content");
                    
                if (!string.IsNullOrEmpty(twitterImageUrl))
                {
                    _logger.LogDebug("Found image URL from twitter:image meta tag: {ImageUrl}", twitterImageUrl);
                    return twitterImageUrl;
                }
                
                // Try to get the image URL from the download button
                string downloadUrl = await _htmlDownloader.ExtractAttributeAsync(
                    detailUrl, 
                    "//a[contains(@class, 'download-button')]", 
                    "href");
                
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    _logger.LogDebug("Found image URL from download button: {ImageUrl}", downloadUrl);
                    
                    // Ensure URL is absolute
                    if (!downloadUrl.StartsWith("http"))
                    {
                        downloadUrl = $"{config.BaseUrl.TrimEnd('/')}/{downloadUrl.TrimStart('/')}";
                    }
                    
                    return downloadUrl;
                }
                
                // Try to find the main image on the page
                // Download the HTML to parse it directly
                string html = await _htmlDownloader.DownloadHtmlAsync(detailUrl);
                
                if (string.IsNullOrEmpty(html))
                {
                    _logger.LogWarning("Could not download HTML from detail page: {DetailUrl}", detailUrl);
                    return string.Empty;
                }
                
                // Save detail page HTML for inspection
                try 
                {
                    string logDir = Path.Combine(Path.GetTempPath(), "WallYouNeed_Logs");
                    Directory.CreateDirectory(logDir);
                    string filename = $"detail_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileNameWithoutExtension(detailUrl)}.html";
                    string fullPath = Path.Combine(logDir, filename);
                    File.WriteAllText(fullPath, html);
                    _logger.LogInformation("Saved detail HTML to log file: {Path}", fullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save detail HTML log file");
                }
                
                // Try several patterns to match the main wallpaper image
                var patterns = new[]
                {
                    // Look for download links
                    "<a[^>]*href\\s*=\\s*['\"]([^'\"]*(?:download|original|full|hd|wallpaper)[^'\"]*)['\"\\?][^>]*>",
                    
                    // Look for main/hero images
                    "<img[^>]*(?:id\\s*=\\s*['\"](?:main-image|hero-image|wallpaper-img|full-image)['\"])[^>]*src\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>",
                    
                    // Look for high-res images
                    "<img[^>]*src\\s*=\\s*['\"]([^'\"]*(?:original|full|large)[^'\"]*)['\"\\?][^>]*>",
                    
                    // Look for images with certain CSS classes
                    "<img[^>]*class\\s*=\\s*['\"][^'\"]*(?:wallpaper|full|large|main|hero)[^'\"]*['\"][^>]*src\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>",
                    
                    // Look for download links with other attributes
                    "<a[^>]*download[^>]*href\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>",
                    
                    // Look for buttons with download attributes
                    "<button[^>]*data-(?:url|download|src)\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>",
                    
                    // Check for JSON data containing image URL (common in modern sites)
                    "\"(?:url|wallpaper_url|src|original)\"\\s*:\\s*\"([^\"]*\\.(?:jpg|jpeg|png))\"",
                    
                    // Look for background image in style
                    "background(?:-image)?\\s*:\\s*url\\(['\"]?([^'\")]*)['\"]?\\)",
                    
                    // Find data-src attributes (common in lazy-loaded images)
                    "<img[^>]*data-src\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>"
                };
                
                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(html, pattern, RegexOptions.Singleline);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        string imageUrl = match.Groups[1].Value;
                        
                        // Skip small/thumbnail images
                        if (imageUrl.Contains("thumb") || imageUrl.Contains("small") || imageUrl.Contains("icon") || 
                            imageUrl.Contains("avatar") || imageUrl.Contains("mini"))
                        {
                            continue;
                        }
                        
                        // Ensure URL is absolute
                        if (!imageUrl.StartsWith("http"))
                        {
                            imageUrl = $"{config.BaseUrl.TrimEnd('/')}/{imageUrl.TrimStart('/')}";
                        }
                        
                        _logger.LogDebug("Found image URL using pattern {PatternIndex}: {ImageUrl}", 
                            Array.IndexOf(patterns, pattern), imageUrl);
                        
                        return imageUrl;
                    }
                }
                
                // If all else fails, try to find any large image on the page
                var allImageMatches = Regex.Matches(html, "<img[^>]*\\s(?:src|data-src)\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>", RegexOptions.Singleline);
                
                string largestImageUrl = string.Empty;
                foreach (Match match in allImageMatches)
                {
                    if (match.Groups.Count > 1)
                    {
                        string imageUrl = match.Groups[1].Value;
                        
                        // Skip if it's a thumbnail or icon
                        if (imageUrl.Contains("thumb") || imageUrl.Contains("icon") || 
                            imageUrl.Contains("avatar") || imageUrl.Contains("small"))
                        {
                            continue;
                        }
                        
                        // Choose this one if it's the first or if it contains keywords suggesting it's a wallpaper
                        if (string.IsNullOrEmpty(largestImageUrl) || 
                            imageUrl.Contains("large") || imageUrl.Contains("full") || 
                            imageUrl.Contains("original") || imageUrl.Contains("wallpaper") ||
                            imageUrl.Contains("background") || imageUrl.Contains("download") ||
                            (imageUrl.Contains(".jpg") || imageUrl.Contains(".jpeg") || imageUrl.Contains(".png")))
                        {
                            largestImageUrl = imageUrl;
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(largestImageUrl))
                {
                    // Ensure URL is absolute
                    if (!largestImageUrl.StartsWith("http"))
                    {
                        largestImageUrl = $"{config.BaseUrl.TrimEnd('/')}/{largestImageUrl.TrimStart('/')}";
                    }
                    
                    _logger.LogDebug("Found best candidate image URL from all images: {ImageUrl}", largestImageUrl);
                    return largestImageUrl;
                }
                
                // As a last resort, try to construct an image URL based on the detail URL
                string potentialImageUrl = string.Empty;
                
                // If detail URL doesn't end with a file extension, try adding one
                if (!detailUrl.EndsWith(".jpg") && !detailUrl.EndsWith(".jpeg") && !detailUrl.EndsWith(".png"))
                {
                    potentialImageUrl = detailUrl.TrimEnd('/') + ".jpg";
                    _logger.LogDebug("Attempting to construct image URL from detail URL: {ImageUrl}", potentialImageUrl);
                }
                
                // If we have a potential image URL, verify it exists
                if (!string.IsNullOrEmpty(potentialImageUrl))
                {
                    try
                    {
                        var request = new HttpRequestMessage(HttpMethod.Head, potentialImageUrl);
                        var response = await _httpClient.SendAsync(request);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogDebug("Successfully verified constructed image URL: {ImageUrl}", potentialImageUrl);
                            return potentialImageUrl;
                        }
                    }
                    catch
                    {
                        // Ignore errors - we'll just return empty string below
                    }
                }
                
                _logger.LogWarning("Could not find any suitable image URL in detail page: {DetailUrl}", detailUrl);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting image URL from detail page: {DetailUrl}", detailUrl);
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Determines the resolution category based on width and height
        /// </summary>
        private string DetermineResolutionCategory(int width, int height)
        {
            if (width == 0 || height == 0)
                return "Unknown";
                
            var ratio = (double)width / height;
            
            if (Math.Abs(ratio - 1.78) < 0.1) // 16:9 ratio
            {
                if (width >= 3840) return "4K";
                if (width >= 2560) return "2K";
                if (width >= 1920) return "Full HD";
                if (width >= 1280) return "HD";
                return "SD";
            }
            else if (Math.Abs(ratio - 1.33) < 0.1) // 4:3 ratio
            {
                return "4:3";
            }
            else if (ratio > 2) // Ultrawide
            {
                return "Ultrawide";
            }
            else if (width > height) // Landscape but irregular
            {
                return "Wide";
            }
            else if (height > width) // Portrait
            {
                return "Portrait";
            }
            
            return "Other";
        }
        
        /// <summary>
        /// Extract wallpapers directly from HTML content when element-based approach fails
        /// </summary>
        private async Task<List<WallpaperModel>> ExtractWallpapersDirectlyFromHtml(string html, string sourceUrl, string category = null)
        {
            var wallpapers = new List<WallpaperModel>();
            
            if (string.IsNullOrEmpty(html))
            {
                _logger.LogWarning("Empty HTML content received for direct extraction from {SourceUrl}", sourceUrl);
                return wallpapers;
            }
            
            try
            {
                _logger.LogInformation("Attempting direct HTML extraction from {SourceUrl}", sourceUrl);
                
                // Use HtmlAgilityPack for parsing
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                
                // Log the number of image elements found
                var allImages = htmlDoc.DocumentNode.SelectNodes("//img");
                var imageCount = allImages?.Count ?? 0;
                _logger.LogInformation("Found {Count} image elements in HTML", imageCount);
                
                // Find all possible wallpaper elements
                var wallpaperNodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'item') and contains(@class, 'wallpaper')]");
                
                if (wallpaperNodes == null || wallpaperNodes.Count == 0)
                {
                    // Try alternative selectors
                    wallpaperNodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'wallpapers__item')]");
                }
                
                if (wallpaperNodes == null || wallpaperNodes.Count == 0)
                {
                    // Try any element with data-id or wallpaper id attribute
                    wallpaperNodes = htmlDoc.DocumentNode.SelectNodes("//*[@data-id or @data-wallpaper-id]");
                }
                
                if (wallpaperNodes == null || wallpaperNodes.Count == 0)
                {
                    // Try finding elements with wallpaper in the class or id
                    wallpaperNodes = htmlDoc.DocumentNode.SelectNodes("//*[contains(@class, 'wallpaper') or contains(@id, 'wallpaper')]");
                }
                
                // If we still haven't found any wallpapers, look for links to wallpaper detail pages
                if (wallpaperNodes == null || wallpaperNodes.Count == 0)
                {
                    var wallpaperLinks = htmlDoc.DocumentNode.SelectNodes("//a[contains(@href, '/wallpaper/')]");
                    if (wallpaperLinks != null && wallpaperLinks.Count > 0)
                    {
                        _logger.LogInformation("Found {Count} wallpaper links", wallpaperLinks.Count);
                        foreach (var link in wallpaperLinks.Take(10))
                        {
                            var href = link.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(href))
                            {
                                var titleElement = link.SelectSingleNode(".//h2") ?? link.SelectSingleNode(".//h3") ?? link.SelectSingleNode(".//span");
                                var title = titleElement != null ? DecodeHtml(titleElement.InnerText.Trim()) : "Unknown";
                                
                                var imgElement = link.SelectSingleNode(".//img");
                                var thumbnailUrl = imgElement?.GetAttributeValue("src", "") ?? "";
                                
                                // Try to get data-src if src is empty or a placeholder
                                if (string.IsNullOrEmpty(thumbnailUrl) || thumbnailUrl.Contains("placeholder") || thumbnailUrl.Contains("lazy"))
                                {
                                    thumbnailUrl = imgElement?.GetAttributeValue("data-src", "") ?? "";
                                }
                                
                                // Get the wallpaper ID from the href
                                var idMatch = Regex.Match(href, @"/wallpaper/(\d+)");
                                var id = idMatch.Success ? idMatch.Groups[1].Value : "";
                                
                                if (!string.IsNullOrEmpty(id))
                                {
                                    // Construct a full URL
                                    var fullHref = href.StartsWith("http") ? href : (href.StartsWith("/") ? $"https://backiee.com{href}" : $"https://backiee.com/{href}");
                                    
                                    // Try to determine resolution from text
                                    var resolutionMatch = Regex.Match(link.InnerText, @"(\d+)\s*[xX]\s*(\d+)");
                                    int width = 0, height = 0;
                                    if (resolutionMatch.Success)
                                    {
                                        int.TryParse(resolutionMatch.Groups[1].Value, out width);
                                        int.TryParse(resolutionMatch.Groups[2].Value, out height);
                                    }
                                    
                                    // Construct direct image URL if possible
                                    var imageUrl = !string.IsNullOrEmpty(id) 
                                        ? $"https://backiee.com/static/wallpapers/wide/{id}.jpg" 
                                        : "";
                                    
                                    var wallpaper = new WallpaperModel
                                    {
                                        Id = id,
                                        Title = !string.IsNullOrEmpty(title) ? title : "Wallpaper " + id,
                                        Category = !string.IsNullOrEmpty(category) ? category : "Latest",
                                        Width = width,
                                        Height = height,
                                        ResolutionCategory = DetermineResolutionCategory(width, height),
                                        ThumbnailUrl = !string.IsNullOrEmpty(thumbnailUrl) ? thumbnailUrl : "",
                                        ImageUrl = !string.IsNullOrEmpty(imageUrl) ? imageUrl : "",
                                        SourceUrl = fullHref,
                                        Source = "Backiee",
                                        UploadDate = DateTime.Now
                                    };
                                    
                                    wallpapers.Add(wallpaper);
                                }
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("Found {Count} wallpaper nodes", wallpaperNodes.Count);
                    
                    foreach (var node in wallpaperNodes.Take(10))
                    {
                        // Extract wallpaper ID
                        var id = node.GetAttributeValue("data-id", "") ?? 
                                node.GetAttributeValue("data-wallpaper-id", "") ?? 
                                "";
                                
                        // If ID is still empty, try to find it in a link
                        if (string.IsNullOrEmpty(id))
                        {
                            var link = node.SelectSingleNode(".//a[contains(@href, '/wallpaper/')]");
                            if (link != null)
                            {
                                var href = link.GetAttributeValue("href", "");
                                var idMatch = Regex.Match(href, @"/wallpaper/(\d+)");
                                if (idMatch.Success)
                                {
                                    id = idMatch.Groups[1].Value;
                                }
                            }
                        }
                        
                        // Extract title
                        var titleNode = node.SelectSingleNode(".//h2") ?? 
                                        node.SelectSingleNode(".//h3") ?? 
                                        node.SelectSingleNode(".//span[@class='title']") ??
                                        node.SelectSingleNode(".//a[@title]");
                                        
                        var title = titleNode != null ? 
                                    DecodeHtml(titleNode.InnerText.Trim()) : 
                                    titleNode?.GetAttributeValue("title", "") ?? 
                                    "Wallpaper " + id;
                        
                        // Extract thumbnail URL
                        var imgNode = node.SelectSingleNode(".//img");
                        var thumbnailUrl = imgNode?.GetAttributeValue("src", "") ?? "";
                        
                        // Try data-src if src is empty or a placeholder
                        if (string.IsNullOrEmpty(thumbnailUrl) || thumbnailUrl.Contains("placeholder") || thumbnailUrl.Contains("lazy"))
                        {
                            thumbnailUrl = imgNode?.GetAttributeValue("data-src", "") ?? 
                                          imgNode?.GetAttributeValue("data-lazy-src", "") ?? 
                                          imgNode?.GetAttributeValue("data-original", "") ?? 
                                          "";
                        }
                        
                        // If we have an ID, try to construct a direct image URL
                        var imageUrl = !string.IsNullOrEmpty(id) 
                            ? $"https://backiee.com/static/wallpapers/wide/{id}.jpg" 
                            : "";
                            
                        // Try to find the source URL (detail page)
                        var detailLink = node.SelectSingleNode(".//a[contains(@href, '/wallpaper/')]");
                        var sourceUrl2 = detailLink?.GetAttributeValue("href", "") ?? "";
                        
                        // Make the URL absolute if it's relative
                        if (!string.IsNullOrEmpty(sourceUrl2) && !sourceUrl2.StartsWith("http"))
                        {
                            sourceUrl2 = sourceUrl2.StartsWith("/") 
                                ? $"https://backiee.com{sourceUrl2}" 
                                : $"https://backiee.com/{sourceUrl2}";
                        }
                        
                        // If we still don't have a source URL, use the current page
                        if (string.IsNullOrEmpty(sourceUrl2))
                        {
                            sourceUrl2 = sourceUrl;
                        }
                        
                        // Try to determine resolution
                        var resolutionText = node.InnerText;
                        var resolutionMatch = Regex.Match(resolutionText, @"(\d+)\s*[xX]\s*(\d+)");
                        int width = 0, height = 0;
                        if (resolutionMatch.Success)
                        {
                            int.TryParse(resolutionMatch.Groups[1].Value, out width);
                            int.TryParse(resolutionMatch.Groups[2].Value, out height);
                        }
                        
                        if (!string.IsNullOrEmpty(id))
                        {
                            var wallpaper = new WallpaperModel
                            {
                                Id = id,
                                Title = !string.IsNullOrEmpty(title) ? title : "Wallpaper " + id,
                                Category = !string.IsNullOrEmpty(category) ? category : "Latest",
                                Width = width,
                                Height = height,
                                ResolutionCategory = DetermineResolutionCategory(width, height),
                                ThumbnailUrl = !string.IsNullOrEmpty(thumbnailUrl) ? thumbnailUrl : "",
                                ImageUrl = !string.IsNullOrEmpty(imageUrl) ? imageUrl : "",
                                SourceUrl = sourceUrl2,
                                Source = "Backiee",
                                UploadDate = DateTime.Now
                            };
                            
                            wallpapers.Add(wallpaper);
                        }
                    }
                }
                
                _logger.LogInformation("Direct HTML extraction found {Count} wallpapers", wallpapers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during direct HTML extraction: {Message}", ex.Message);
            }
            
            return wallpapers;
        }

        private string DecodeHtml(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;
            
            // Use HtmlAgilityPack for HTML decoding
            return HtmlAgilityPack.HtmlEntity.DeEntitize(html);
        }

        /// <summary>
        /// Analyzes HTML directly for debugging purposes when scraping fails
        /// </summary>
        private async Task AnalyzeHtmlForDebug(string url)
        {
            try
            {
                _logger.LogInformation("Starting HTML analysis for debugging: {Url}", url);
                
                // Download the HTML
                var html = await _htmlDownloader.DownloadHtmlAsync(url);
                if (string.IsNullOrEmpty(html))
                {
                    _logger.LogWarning("Couldn't download HTML for analysis from {Url}", url);
                    return;
                }
                
                string logDir = Path.Combine(Path.GetTempPath(), "WallYouNeed_Logs");
                Directory.CreateDirectory(logDir);
                string filename = $"analysis_debug_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string fullPath = Path.Combine(logDir, filename);
                
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"HTML Analysis for {url} at {DateTime.Now}");
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine();
                
                // Check for key patterns that would indicate we're on the right page
                sb.AppendLine("1. Key page indicators:");
                sb.AppendLine("-".PadRight(40, '-'));
                CheckPattern(sb, html, "<title[^>]*>([^<]*backiee[^<]*)</title>", "Title contains 'backiee'");
                CheckPattern(sb, html, "wallpaper", "Contains 'wallpaper' text");
                CheckPattern(sb, html, "class\\s*=\\s*['\"]col-sm-3", "Contains Bootstrap column classes");
                CheckPattern(sb, html, "<a[^>]*href\\s*=\\s*['\"][^'\"]*wallpaper[^'\"]*['\"]", "Contains wallpaper links");
                CheckPattern(sb, html, "data-src\\s*=", "Contains data-src attributes (lazy loading)");
                sb.AppendLine();
                
                // Check for specific HTML structures
                sb.AppendLine("2. HTML Structure Checks:");
                sb.AppendLine("-".PadRight(40, '-'));
                CheckPattern(sb, html, "<div[^>]*class\\s*=\\s*['\"][^'\"]*tz-gallery[^'\"]*['\"]", "Gallery container");
                CheckPattern(sb, html, "<div[^>]*class\\s*=\\s*['\"][^'\"]*row[^'\"]*['\"]", "Bootstrap row");
                CheckPattern(sb, html, "<div[^>]*class\\s*=\\s*['\"][^'\"]*col-sm-3[^'\"]*['\"]", "Bootstrap column");
                CheckPattern(sb, html, "<div[^>]*class\\s*=\\s*['\"][^'\"]*placeholder[^'\"]*['\"]", "Image placeholder");
                CheckPattern(sb, html, "<img[^>]*class\\s*=\\s*['\"][^'\"]*rounded-image[^'\"]*['\"]", "Rounded image");
                sb.AppendLine();
                
                // Extract a sample wallpaper if possible
                sb.AppendLine("3. Sample Wallpaper Link Extraction:");
                sb.AppendLine("-".PadRight(40, '-'));
                var wallpaperLinkMatch = Regex.Match(html, "<a\\s+href\\s*=\\s*['\"](?:https?://)?(?:www\\.)?backiee\\.com/wallpaper/([^/\"']+)/(\\d+)['\"][^>]*>");
                if (wallpaperLinkMatch.Success)
                {
                    string slug = wallpaperLinkMatch.Groups[1].Value;
                    string id = wallpaperLinkMatch.Groups[2].Value;
                    sb.AppendLine($"Found wallpaper link: slug='{slug}', id='{id}'");
                    
                    // Extract context around this match
                    int start = Math.Max(0, wallpaperLinkMatch.Index - 100);
                    int length = Math.Min(html.Length - start, wallpaperLinkMatch.Length + 200);
                    string context = html.Substring(start, length);
                    sb.AppendLine("Context around match:");
                    sb.AppendLine(context);
                }
                else
                {
                    sb.AppendLine("No wallpaper links found matching expected pattern.");
                }
                sb.AppendLine();
                
                // Look for image tags and their structures
                sb.AppendLine("4. Image Tags Analysis:");
                sb.AppendLine("-".PadRight(40, '-'));
                var imgTags = Regex.Matches(html, "<img[^>]*>");
                sb.AppendLine($"Found {imgTags.Count} image tags in total.");
                if (imgTags.Count > 0)
                {
                    // Sample the first 3 image tags
                    int sampleSize = Math.Min(3, imgTags.Count);
                    for (int i = 0; i < sampleSize; i++)
                    {
                        sb.AppendLine($"Sample image tag {i+1}:");
                        sb.AppendLine(imgTags[i].Value);
                        sb.AppendLine();
                    }
                }
                
                // Write the analysis to file
                File.WriteAllText(fullPath, sb.ToString());
                _logger.LogInformation("HTML analysis complete. Results saved to: {Path}", fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing HTML");
            }
        }
        
        private void CheckPattern(System.Text.StringBuilder sb, string html, string pattern, string description)
        {
            var matches = Regex.Matches(html, pattern);
            sb.AppendLine($"{description}: {(matches.Count > 0 ? "YES" : "NO")} ({matches.Count} matches)");
        }

        /// <summary>
        /// More reliable extraction method that focuses on getting wallpaper IDs directly from the HTML
        /// </summary>
        private async Task<List<WallpaperModel>> ExtractWallpapersByDirectIds(List<string> wallpaperIds)
        {
            var wallpapers = new List<WallpaperModel>();
            var tasks = new List<Task<WallpaperModel>>();

            _logger.LogInformation("Starting extraction of {Count} wallpapers by direct IDs", wallpaperIds.Count);
            
            int maxConcurrent = 3; // Maximum concurrent requests to avoid overloading

            // Create a semaphore to limit concurrent requests
            using (var semaphore = new SemaphoreSlim(maxConcurrent))
            {
                foreach (var id in wallpaperIds)
                {
                    await semaphore.WaitAsync();
                    
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var url = $"https://backiee.com/wallpaper/{id}";
                            _logger.LogDebug("Downloading HTML for wallpaper ID {Id} from {Url}", id, url);
                            
                            string html = await _htmlDownloader.DownloadHtmlAsync(url);
                            
                            if (string.IsNullOrEmpty(html))
                            {
                                _logger.LogWarning("Failed to download HTML for wallpaper ID {Id}", id);
                                return null;
                            }
                            
                            // Check if we got actual HTML or binary data (anti-scraping measure)
                            if (IsBinaryData(html))
                            {
                                _logger.LogWarning("Received binary data instead of HTML for wallpaper ID {Id}. Site may be serving images to block scrapers.", id);
                                // Create wallpaper with direct URL construction as fallback
                                return CreateDirectWallpaper(id);
                            }
                            
                            var wallpaper = ExtractSingleWallpaperDetails(html, id);
                            
                            if (wallpaper != null)
                            {
                                _logger.LogInformation("Successfully extracted wallpaper with ID {Id}: {Title}", id, wallpaper.Title);
                                
                                // Verify the image URL actually works
                                if (await _htmlDownloader.VerifyImageUrl(wallpaper.ImageUrl))
                                {
                                    _logger.LogDebug("Verified image URL for ID {Id}: {ImageUrl}", id, wallpaper.ImageUrl);
                                    return wallpaper;
                                }
                                else
                                {
                                    _logger.LogWarning("Image URL verification failed for ID {Id}, using direct construction", id);
                                    return CreateDirectWallpaper(id);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Failed to extract wallpaper details for ID {Id}, using direct construction", id);
                                return CreateDirectWallpaper(id);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error extracting wallpaper with ID {Id}: {Message}", id, ex.Message);
                            return null;
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                    
                    // Small delay between starting each task to avoid triggering rate limits
                    await Task.Delay(100);
                }
                
                _logger.LogInformation("Waiting for all wallpaper extraction tasks to complete");
                
                try
                {
                    // Wait for all tasks to complete with a timeout
                    var completedTasks = await Task.WhenAll(tasks).ConfigureAwait(false);
                    
                    // Add all non-null wallpapers to the list
                    wallpapers.AddRange(completedTasks.Where(w => w != null));
                    
                    _logger.LogInformation("Successfully extracted {SuccessCount} out of {TotalCount} wallpapers", 
                        wallpapers.Count, wallpaperIds.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error waiting for wallpaper extraction tasks: {Message}", ex.Message);
                    
                    // Collect results from completed tasks
                    foreach (var task in tasks.Where(t => t.IsCompleted && !t.IsFaulted && !t.IsCanceled))
                    {
                        if (task.Result != null)
                        {
                            wallpapers.Add(task.Result);
                        }
                    }
                    
                    _logger.LogInformation("Recovered {Count} wallpapers from completed tasks", wallpapers.Count);
                }
            }
            
            // If we couldn't extract enough wallpapers, fall back to known working ones
            if (wallpapers.Count < 3)
            {
                _logger.LogWarning("Could not extract enough wallpapers. Got {Count}, needed at least 3. Using fallback mechanism", 
                    wallpapers.Count);
                
                // Get the number of additional wallpapers needed
                int additionalNeeded = Math.Max(5 - wallpapers.Count, 0);
                
                if (additionalNeeded > 0)
                {
                    var fallbackWallpapers = await GetKnownWorkingWallpapers(additionalNeeded);
                    wallpapers.AddRange(fallbackWallpapers);
                    
                    _logger.LogInformation("Added {Count} fallback wallpapers", fallbackWallpapers.Count);
                }
            }
            
            return wallpapers;
        }

        // Add a helper method to check if data is binary
        private bool IsBinaryData(string content)
        {
            if (string.IsNullOrEmpty(content) || content.Length < 10)
                return false;
            
            // Check for common binary file signatures
            // JPEG signature (0xFF, 0xD8)
            if (content[0] == (char)0xFF && content[1] == (char)0xD8)
                return true;
            
            // PNG signature (0x89, 'P', 'N', 'G')
            if (content[0] == (char)0x89 && content.StartsWith("\u0089PNG", StringComparison.Ordinal))
                return true;
            
            // GIF signature ('G', 'I', 'F')
            if (content.StartsWith("GIF", StringComparison.Ordinal))
                return true;
            
            // Check for high concentration of non-printable characters
            int nonPrintableCount = 0;
            for (int i = 0; i < Math.Min(200, content.Length); i++)
            {
                if (content[i] < 32 && content[i] != '\r' && content[i] != '\n' && content[i] != '\t')
                    nonPrintableCount++;
            }
            
            // If more than 15% of the first 200 characters are non-printable, it's likely binary
            if (nonPrintableCount > 30)
                return true;
            
            return false;
        }

        /// <summary>
        /// Gets wallpapers using a list of known working wallpaper IDs
        /// </summary>
        private async Task<List<WallpaperModel>> GetKnownWorkingWallpapers(int count)
        {
            _logger.LogInformation("Attempting to get {Count} known working wallpapers as fallback", count);
            
            // List of known working IDs
            var knownIds = new List<string>
            {
                "318542", "318541", "318540", "318534", "318532", 
                "318531", "318530", "318528", "318524", "318520",
                "318519", "318518", "318517", "318516", "318515",
                "318320", "318319", "318318", "318317", "318316",
                "318142", "318141", "318140", "318138", "318137"
            };
            
            // Shuffle the list to get different wallpapers each time
            var random = new Random();
            var shuffledIds = knownIds.OrderBy(x => random.Next()).Take(Math.Min(count, knownIds.Count)).ToList();
            
            var results = new List<WallpaperModel>();
            foreach (var id in shuffledIds)
            {
                try
                {
                    var url = $"https://backiee.com/wallpaper/{id}";
                    _logger.LogInformation("Attempting to get known wallpaper with ID {Id}", id);
                    
                    string html = await _htmlDownloader.DownloadHtmlAsync(url);
                    if (string.IsNullOrEmpty(html))
                    {
                        _logger.LogWarning("Failed to download HTML for known wallpaper ID {Id}", id);
                        continue;
                    }
                    
                    var wallpaper = ExtractSingleWallpaperDetails(html, id);
                    if (wallpaper != null && !string.IsNullOrEmpty(wallpaper.ImageUrl))
                    {
                        _logger.LogInformation("Successfully extracted known wallpaper with ID {Id}", id);
                        results.Add(wallpaper);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to extract wallpaper details for known ID {Id}", id);
                    }
                    
                    // Add a small delay to avoid hitting rate limits
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting known wallpaper with ID {Id}: {Message}", id, ex.Message);
                }
            }
            
            // If we still don't have enough wallpapers, add some stable Unsplash URLs as a last resort
            if (results.Count < count)
            {
                _logger.LogWarning("Could not get enough known wallpapers. Adding {Count} Unsplash fallback wallpapers", 
                    Math.Min(5, count - results.Count));
                
                var unsplashFallbacks = new List<(string Url, string Title)>
                {
                    ("https://images.unsplash.com/photo-1506744038136-46273834b3fb", "Beautiful Mountain Landscape"),
                    ("https://images.unsplash.com/photo-1494500764479-0c8f2919a3d8", "Starry Night Sky"),
                    ("https://images.unsplash.com/photo-1511300636408-a63a89df3482", "Peaceful Forest"),
                    ("https://images.unsplash.com/photo-1497436072909-60f360e1d4b1", "Green Mountains"),
                    ("https://images.unsplash.com/photo-1507525428034-b723cf961d3e", "Serene Beach")
                };
                
                var usedCount = 0;
                foreach (var (imageUrl, title) in unsplashFallbacks)
                {
                    if (results.Count >= count || usedCount >= 5) break;
                    
                    var wallpaper = new WallpaperModel
                    {
                        Title = title,
                        ImageUrl = imageUrl,
                        ThumbnailUrl = imageUrl,
                        Source = "Unsplash (Fallback)",
                        SourceUrl = "https://unsplash.com",
                        Width = 1920,
                        Height = 1080,
                        Category = "Nature",
                        Rating = 5
                    };
                    
                    results.Add(wallpaper);
                    usedCount++;
                    _logger.LogInformation("Added Unsplash fallback wallpaper: {Title}", title);
                }
            }
            
            return results;
        }

        /// <summary>
        /// Extracts wallpaper details by directly accessing individual wallpaper pages by ID
        /// </summary>
        private WallpaperModel ExtractSingleWallpaperDetails(string html, string id)
        {
            try
            {
                // Try various patterns to find the title
                var patterns = new[]
                {
                    $"<a[^>]*href\\s*=\\s*['\"][^'\"]*{id}[^'\"]*['\"][^>]*title\\s*=\\s*['\"]([^'\"]+)['\"]",
                    $"<div[^>]*class\\s*=\\s*['\"]max-linese['\"][^>]*>([^<]*{id}[^<]*)</div>",
                    $"<div[^>]*class\\s*=\\s*['\"]box['\"][^>]*>\\s*<div[^>]*>([^<]+)</div>\\s*</div>"
                };
                
                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(html, pattern);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        var title = DecodeHtml(match.Groups[1].Value.Trim());
                        return new WallpaperModel
                        {
                            Id = id,
                            Title = title,
                            Category = "Latest",
                            ThumbnailUrl = $"https://backiee.com/static/wallpapers/560x315/{id}.jpg",
                            ImageUrl = $"https://backiee.com/static/wallpapers/wide/{id}.jpg",
                            SourceUrl = $"https://backiee.com/wallpaper/{id}",
                            Source = "Backiee",
                            Width = 1920,
                            Height = 1080,
                            UploadDate = DateTime.Now
                        };
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting wallpaper with ID {Id}: {Message}", id, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Provides fallback wallpapers when Backiee scraping fails
        /// </summary>
        private async Task<List<WallpaperModel>> GetFallbackWallpapers(int count)
        {
            _logger.LogInformation("Getting {Count} fallback wallpapers through multi-stage fallback pipeline", count);
            
            var wallpapers = new List<WallpaperModel>();
            
            // STAGE 1: Try direct known ID approach first (fastest, but might get blocked)
            var knownIds = new List<string>
            {
                "318542", "318541", "318540", "318534", "318532", 
                "318531", "318530", "318528", "318524", "318520",
                "318519", "318518", "318517", "318516", "318515",
                "318320", "318319", "318318", "318317", "318316",
                "318142", "318141", "318140", "318138", "318137"
            };
            
            // Take a random subset of IDs to try
            var random = new Random();
            var shuffledIds = knownIds.OrderBy(x => random.Next()).Take(Math.Min(10, knownIds.Count)).ToList();
            
            // Try direct URL creation first (no HTML scraping required)
            _logger.LogInformation("STAGE 1: Testing direct image URLs from known IDs");
            foreach (var id in shuffledIds.Take(5))
            {
                // Create wallpaper with direct URL construction
                var wallpaper = CreateDirectWallpaper(id);
                
                if (wallpaper != null)
                {
                    // Verify the image URL without downloading HTML
                    if (await _htmlDownloader.VerifyImageUrl(wallpaper.ImageUrl))
                    {
                        _logger.LogInformation("Successfully verified direct image URL for ID {Id}", id);
                        wallpapers.Add(wallpaper);
                        
                        if (wallpapers.Count >= count)
                            break;
                    }
                }
            }
            
            if (wallpapers.Count >= count)
            {
                _logger.LogInformation("STAGE 1 succeeded: Found {Count} direct wallpapers without HTML scraping", wallpapers.Count);
                return wallpapers.Take(count).ToList();
            }
            
            // STAGE 2: Try HTML extraction for remaining IDs (more reliable but slower)
            _logger.LogInformation("STAGE 2: Trying HTML extraction for remaining {Count} wallpapers", count - wallpapers.Count);
            if (wallpapers.Count < count)
            {
                foreach (var id in shuffledIds.Skip(5).Take(10))
                {
                    try
                    {
                        var url = $"https://backiee.com/wallpaper/{id}";
                        string html = await _htmlDownloader.DownloadHtmlAsync(url);
                        
                        if (!string.IsNullOrEmpty(html))
                        {
                            var extractedWallpaper = ExtractSingleWallpaperDetails(html, id);
                            if (extractedWallpaper != null && !string.IsNullOrEmpty(extractedWallpaper.ImageUrl))
                            {
                                _logger.LogInformation("Extracted wallpaper with ID {Id} using HTML", id);
                                wallpapers.Add(extractedWallpaper);
                                
                                if (wallpapers.Count >= count)
                                    break;
                            }
                        }
                        
                        // Add a small delay to avoid hitting rate limits
                        await Task.Delay(300);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error during HTML extraction for ID {Id}", id);
                    }
                }
            }
            
            if (wallpapers.Count >= count)
            {
                _logger.LogInformation("STAGE 2 succeeded: Found {Count} wallpapers through HTML extraction", wallpapers.Count);
                return wallpapers.Take(count).ToList();
            }
            
            // STAGE 3: Use GetKnownWorkingWallpapers (our previous fallback approach)
            _logger.LogInformation("STAGE 3: Using GetKnownWorkingWallpapers method for {Count} wallpapers", count - wallpapers.Count);
            if (wallpapers.Count < count)
            {
                var knownWorkingWallpapers = await GetKnownWorkingWallpapers(count - wallpapers.Count);
                if (knownWorkingWallpapers.Any())
                {
                    wallpapers.AddRange(knownWorkingWallpapers);
                    _logger.LogInformation("Added {Count} known working wallpapers", knownWorkingWallpapers.Count);
                }
            }
            
            if (wallpapers.Count >= count)
            {
                _logger.LogInformation("STAGE 3 succeeded: Found total of {Count} wallpapers", wallpapers.Count);
                return wallpapers.Take(count).ToList();
            }
            
            // STAGE 4: Use stable Unsplash wallpapers as final fallback
            _logger.LogInformation("STAGE 4: Using stable Unsplash wallpapers for {Count} remaining", count - wallpapers.Count);
            if (wallpapers.Count < count)
            {
                var stableWallpapers = GetStableWallpapers(count - wallpapers.Count);
                wallpapers.AddRange(stableWallpapers);
                _logger.LogInformation("Added {Count} stable wallpapers from Unsplash", stableWallpapers.Count);
            }
            
            return wallpapers.Take(count).ToList();
        }

        /// <summary>
        /// Creates direct wallpaper URL without requiring HTML scraping
        /// </summary>
        private WallpaperModel CreateDirectWallpaper(string id, string category = "Latest")
        {
            try
            {
                _logger.LogInformation("Creating direct wallpaper for ID {Id} without HTML scraping", id);
                
                // Map of common categories to better titles
                var categoryTitles = new Dictionary<string, string[]>
                {
                    ["Nature"] = new[] { "Forest Landscape", "Mountain View", "Ocean Sunset", "Desert Dawn", "Winter Forest" },
                    ["Abstract"] = new[] { "Colorful Pattern", "Geometric Design", "Abstract Art", "Fluid Colors", "Digital Creation" },
                    ["Animals"] = new[] { "Wildlife", "Pet Companion", "Majestic Creature", "Animal Portrait", "Underwater Life" },
                    ["Space"] = new[] { "Galaxy View", "Nebula Colors", "Stars at Night", "Cosmic Wonder", "Space Exploration" },
                    ["Architecture"] = new[] { "Modern Building", "Historic Structure", "Urban Design", "Architectural Detail", "City Skyline" }
                };
                
                // Create a meaningful title based on category and ID
                string title;
                if (categoryTitles.ContainsKey(category))
                {
                    // Pick a title based on the ID's numeric value
                    int idHashIndex = Math.Abs(id.GetHashCode()) % categoryTitles[category].Length;
                    title = $"{categoryTitles[category][idHashIndex]} {id}";
                }
                else
                {
                    title = $"Wallpaper {id}";
                }
                
                return new WallpaperModel
                {
                    Id = id,
                    Title = title,
                    Category = category,
                    ThumbnailUrl = $"https://backiee.com/static/wallpapers/560x315/{id}.jpg",
                    ImageUrl = $"https://backiee.com/static/wallpapers/wide/{id}.jpg",
                    SourceUrl = $"https://backiee.com/wallpaper/{id}",
                    Source = "Backiee",
                    Width = 1920,
                    Height = 1080,
                    UploadDate = DateTime.Now,
                    Rating = 4
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating direct wallpaper with ID {Id}: {Message}", id, ex.Message);
                return null;
            }
        }
        
        /// <summary>
        /// Generates a list of stable wallpapers when all other methods fail
        /// </summary>
        private List<WallpaperModel> GetStableWallpapers(int count)
        {
            _logger.LogInformation("Generating {Count} stable wallpapers as last resort", count);
            
            var wallpapers = new List<WallpaperModel>();
            
            // Added more diverse Unsplash sources
            var stableSources = new List<(string Url, string Title, string Category)>
            {
                // Nature category
                ("https://images.unsplash.com/photo-1472214103451-9374bd1c798e", "Autumn Forest", "Nature"),
                ("https://images.unsplash.com/photo-1542224566-6e85f2e6772f", "Mountain Range", "Nature"),
                ("https://images.unsplash.com/photo-1505765050516-f72dcac9c60e", "Foggy Trees", "Nature"),
                ("https://images.unsplash.com/photo-1470071459604-3b5ec3a7fe05", "Sunset Valley", "Nature"),
                ("https://images.unsplash.com/photo-1433477155337-9aea4e790195", "Ocean Waves", "Nature"),
                
                // City category
                ("https://images.unsplash.com/photo-1477959858617-67f85cf4f1df", "City Skyline", "City"),
                ("https://images.unsplash.com/photo-1496442226666-8d4d0e62e6e9", "NYC Streets", "City"),
                ("https://images.unsplash.com/photo-1428908728789-d2de25dbd4e2", "Tokyo Night", "City"),
                ("https://images.unsplash.com/photo-1465447142348-e9952c393450", "Bridge View", "City"),
                ("https://images.unsplash.com/photo-1514924013411-cbf25faa35bb", "City Lights", "City"),
                
                // Abstract category
                ("https://images.unsplash.com/photo-1550859492-d5da9d8e45f3", "Abstract Colors", "Abstract"),
                ("https://images.unsplash.com/photo-1563089145-599997674d42", "Flowing Paint", "Abstract"),
                ("https://images.unsplash.com/photo-1505430111830-b998ef798efa", "Geometric Pattern", "Abstract"),
                ("https://images.unsplash.com/photo-1574169208507-84376144848b", "Neon Lights", "Abstract"),
                ("https://images.unsplash.com/photo-1557672172-298e090bd0f1", "Colorful Smoke", "Abstract"),
                
                // Space category
                ("https://images.unsplash.com/photo-1462331940025-496dfbfc7564", "Milky Way", "Space"),
                ("https://images.unsplash.com/photo-1541873676-a18131494184", "Moon Surface", "Space"),
                ("https://images.unsplash.com/photo-1464802686167-b939a6910659", "Nebula", "Space"),
                ("https://images.unsplash.com/photo-1444703686981-a3abbc4d4fe3", "Planet View", "Space"),
                ("https://images.unsplash.com/photo-1451187580459-43490279c0fa", "Galaxy", "Space")
            };
            
            // Get a shuffled subset of the sources
            var random = new Random();
            var shuffledSources = stableSources.OrderBy(x => random.Next()).Take(Math.Min(count, stableSources.Count)).ToList();
            
            foreach (var (url, title, category) in shuffledSources)
            {
                var wallpaper = new WallpaperModel
                {
                    Title = title,
                    ImageUrl = url,
                    ThumbnailUrl = url,
                    Source = "Unsplash (Stable)",
                    SourceUrl = "https://unsplash.com",
                    Width = 1920,
                    Height = 1080,
                    Category = category,
                    Rating = 5
                };
                
                wallpapers.Add(wallpaper);
            }
            
            return wallpapers;
        }

        /// <summary>
        /// Extracts wallpapers from backiee content HTML
        /// </summary>
        /// <param name="htmlContent">The HTML content to parse</param>
        /// <returns>A list of extracted wallpapers</returns>
        public async Task<List<WallpaperModel>> ExtractWallpapersFromContentHtml(string htmlContent)
        {
            _logger.LogInformation("Extracting wallpapers from backiee_content.html");
            LogToFile("Extracting wallpapers from backiee_content.html");
            
            var wallpapers = new List<WallpaperModel>();
            
            try
            {
                if (string.IsNullOrEmpty(htmlContent))
                {
                    _logger.LogWarning("HTML content is empty");
                    LogToFile("HTML content is empty");
                    return wallpapers;
                }
                
                // Parse the HTML document
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);
                
                // Debug the HTML structure
                _logger.LogInformation("HTML loaded, document node has {Count} child nodes", doc.DocumentNode.ChildNodes.Count);
                LogToFile($"HTML loaded, document node has {doc.DocumentNode.ChildNodes.Count} child nodes");
                
                // Look for placeholder divs with lazyload images - this is the pattern used by backiee.com
                var placeholderDivs = doc.DocumentNode.SelectNodes("//div[@class='placeholder']");
                
                if (placeholderDivs != null && placeholderDivs.Count > 0)
                {
                    _logger.LogInformation("Found {Count} placeholder divs", placeholderDivs.Count);
                    LogToFile($"Found {placeholderDivs.Count} placeholder divs");
                    
                    foreach (var div in placeholderDivs)
                    {
                        try
                        {
                            // Find the img tag inside the placeholder div with more permissive selection
                            var imgNode = div.SelectSingleNode(".//img[contains(@class, 'lazyload')]");
                            if (imgNode == null)
                            {
                                _logger.LogDebug("No lazyload img tag found in placeholder div, trying any img");
                                LogToFile("No lazyload img tag found in placeholder div, trying any img");
                                imgNode = div.SelectSingleNode(".//img");
                                if (imgNode == null)
                                {
                                    _logger.LogDebug("No img tag found in placeholder div, skipping");
                                    LogToFile("No img tag found in placeholder div, skipping");
                                    continue;
                                }
                            }
                            
                            // Get the actual image URL from data-src attribute (not src which is just a placeholder)
                            var imageUrl = imgNode.GetAttributeValue("data-src", "");
                            _logger.LogDebug("Found img with data-src: {Url}", imageUrl);
                            LogToFile($"Found img with data-src: {imageUrl}");
                            
                            if (string.IsNullOrEmpty(imageUrl))
                            {
                                _logger.LogDebug("No data-src attribute found in img tag, trying src");
                                LogToFile("No data-src attribute found in img tag, trying src");
                                imageUrl = imgNode.GetAttributeValue("src", "");
                                
                                // Skip placeholder images
                                if (imageUrl.Contains("placeholder"))
                                {
                                    _logger.LogDebug("Found placeholder image, skipping");
                                    LogToFile("Found placeholder image, skipping");
                                    continue;
                                }
                            }
                            
                            if (string.IsNullOrEmpty(imageUrl))
                            {
                                _logger.LogDebug("No image URL found, skipping");
                                LogToFile("No image URL found, skipping");
                                continue;
                            }
                            
                            // Make sure the URL is absolute
                            if (!imageUrl.StartsWith("http"))
                            {
                                imageUrl = imageUrl.StartsWith("/") 
                                    ? $"https://backiee.com{imageUrl}" 
                                    : $"https://backiee.com/{imageUrl}";
                            }
                            
                            // Extract the wallpaper ID from the URL
                            string id = "";
                            var idMatch = Regex.Match(imageUrl, @"/(\d+)\.jpg$");
                            if (idMatch.Success)
                            {
                                id = idMatch.Groups[1].Value;
                                _logger.LogDebug("Extracted ID {Id} from URL {Url}", id, imageUrl);
                            }
                            else
                            {
                                // Generate a random ID if we can't extract one
                                id = Guid.NewGuid().ToString();
                                _logger.LogDebug("Could not extract ID from URL {Url}, using generated ID", imageUrl);
                            }
                            
                            // Get the title from alt attribute
                            var title = imgNode.GetAttributeValue("alt", "");
                            if (string.IsNullOrEmpty(title))
                            {
                                title = $"Backiee Wallpaper {id}";
                            }
                            
                            // Convert thumbnail URL to high quality URL (directly, without resolution extraction)
                            var fullSizeUrl = imageUrl;
                            
                            // For thumbnail URLs like 560x315, convert to wide format
                            if (imageUrl.Contains("/560x315/") && id.Length > 0)
                            {
                                fullSizeUrl = $"https://backiee.com/static/wallpapers/wide/{id}.jpg";
                                _logger.LogDebug("Converted thumbnail URL to wide format: {FullSizeUrl}", fullSizeUrl);
                            }
                            
                            // Create wallpaper model with default HD resolution
                            var wallpaper = new WallpaperModel
                            {
                                Id = id,
                                Title = title,
                                ImageUrl = fullSizeUrl,
                                ThumbnailUrl = imageUrl,
                                SourceUrl = $"https://backiee.com/wallpaper/{id}",
                                Source = "Backiee",
                                Width = 1920,  // Default width
                                Height = 1080, // Default height
                                ResolutionCategory = "HD", // Default category
                                UploadDate = DateTime.Now
                            };
                            
                            wallpapers.Add(wallpaper);
                            _logger.LogInformation("Added wallpaper: {Title} with URL {Url}", title, fullSizeUrl);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error processing placeholder div");
                        }
                    }
                    
                    _logger.LogInformation("Successfully extracted {Count} wallpapers from placeholder divs", wallpapers.Count);
                    return wallpapers;
                }
                else
                {
                    _logger.LogWarning("No placeholder divs found in the HTML. Looking for img tags with data-src instead.");
                    
                    // Fallback approach - look for any img with data-src directly
                    var imgTags = doc.DocumentNode.SelectNodes("//img[@data-src]");
                    if (imgTags != null && imgTags.Count > 0)
                    {
                        _logger.LogInformation("Found {Count} img tags with data-src attribute", imgTags.Count);
                        
                        foreach (var img in imgTags)
                        {
                            try
                            {
                                var imageUrl = img.GetAttributeValue("data-src", "");
                                if (string.IsNullOrEmpty(imageUrl) || imageUrl.Contains("placeholder"))
                                {
                                    continue;
                                }
                                
                                // Make sure the URL is absolute
                                if (!imageUrl.StartsWith("http"))
                                {
                                    imageUrl = imageUrl.StartsWith("/") 
                                        ? $"https://backiee.com{imageUrl}" 
                                        : $"https://backiee.com/{imageUrl}";
                                }
                                
                                // Extract the wallpaper ID from the URL
                                string id = "";
                                var idMatch = Regex.Match(imageUrl, @"/(\d+)\.jpg$");
                                if (idMatch.Success)
                                {
                                    id = idMatch.Groups[1].Value;
                                }
                                else
                                {
                                    id = Guid.NewGuid().ToString();
                                }
                                
                                // Get the title from alt attribute
                                var title = img.GetAttributeValue("alt", "");
                                if (string.IsNullOrEmpty(title))
                                {
                                    title = $"Backiee Wallpaper {id}";
                                }
                                
                                // Convert thumbnail URL to high quality URL
                                var fullSizeUrl = imageUrl;
                                if (imageUrl.Contains("/560x315/") && id.Length > 0)
                                {
                                    fullSizeUrl = $"https://backiee.com/static/wallpapers/wide/{id}.jpg";
                                }
                                
                                var wallpaper = new WallpaperModel
                                {
                                    Id = id,
                                    Title = title,
                                    ImageUrl = fullSizeUrl,
                                    ThumbnailUrl = imageUrl,
                                    SourceUrl = $"https://backiee.com/wallpaper/{id}",
                                    Source = "Backiee",
                                    Width = 1920,
                                    Height = 1080,
                                    ResolutionCategory = "HD",
                                    UploadDate = DateTime.Now
                                };
                                
                                wallpapers.Add(wallpaper);
                                _logger.LogInformation("Added wallpaper from img tag: {Title}", title);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error processing img tag");
                            }
                        }
                        
                        if (wallpapers.Count > 0)
                        {
                            _logger.LogInformation("Successfully extracted {Count} wallpapers from img tags", wallpapers.Count);
                            return wallpapers;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No img tags with data-src found in the HTML.");
                    }
                }
                
                // Fallback to the original extraction method if no placeholder divs are found
                _logger.LogWarning("Falling back to original extraction method");
                
                // The rest of the fallback method remains the same...
                
                // Extract wallpaper IDs from the HTML content
                var wallpaperIds = new HashSet<string>();
                
                // Look for wallpaper links or containers
                var wallpaperLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, '/wallpaper/')]");
                if (wallpaperLinks != null)
                {
                    foreach (var link in wallpaperLinks)
                    {
                        var href = link.GetAttributeValue("href", "");
                        var match = Regex.Match(href, @"/wallpaper/([^/]+)/(\d+)");
                        if (match.Success && match.Groups.Count >= 3)
                        {
                            wallpaperIds.Add(match.Groups[2].Value);
                        }
                    }
                }
                
                // Look for wallpaper containers with data-id attribute
                var wallpaperContainers = doc.DocumentNode.SelectNodes("//*[@data-id]");
                if (wallpaperContainers != null)
                {
                    foreach (var container in wallpaperContainers)
                    {
                        var id = container.GetAttributeValue("data-id", "");
                        if (!string.IsNullOrEmpty(id) && Regex.IsMatch(id, @"^\d+$"))
                        {
                            wallpaperIds.Add(id);
                        }
                    }
                }
                
                if (wallpaperIds.Count == 0)
                {
                    _logger.LogWarning("No valid wallpaper IDs extracted from links");
                    return wallpapers;
                }
                
                _logger.LogInformation("Extracted {Count} unique wallpaper IDs from content HTML", wallpaperIds.Count);
                
                // Process these IDs to get actual wallpapers
                var extractedWallpapers = await ExtractWallpapersByDirectIds(wallpaperIds.ToList());
                wallpapers.AddRange(extractedWallpapers);
                
                _logger.LogInformation("Successfully processed {Count} wallpapers from content HTML", wallpapers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting wallpapers from content HTML: {Message}", ex.Message);
            }
            
            return wallpapers;
        }
        
        /// <summary>
        /// Extracts wallpapers from the local backiee_content.html file
        /// </summary>
        /// <returns>A list of extracted wallpapers</returns>
        public async Task<List<WallpaperModel>> ExtractWallpapersFromLocalFile()
        {
            try
            {
                LogToFile("Starting extraction from local backiee_content.html file");
                string localFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backiee_content.html");
                
                if (!File.Exists(localFilePath))
                {
                    _logger.LogWarning("Local backiee_content.html file not found at: {Path}", localFilePath);
                    LogToFile($"Local backiee_content.html file not found at: {localFilePath}");
                    
                    // Try an alternative location
                    var executablePath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                    localFilePath = Path.Combine(executablePath, "backiee_content.html");
                    LogToFile($"Trying alternative location: {localFilePath}");
                    
                    if (!File.Exists(localFilePath))
                    {
                        _logger.LogError("Could not find backiee_content.html in any expected location");
                        LogToFile("Could not find backiee_content.html in any expected location");
                        
                        // Try current directory as a last resort
                        string currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), "backiee_content.html");
                        LogToFile($"Trying current directory as last resort: {currentDirPath}");
                        
                        if (File.Exists(currentDirPath))
                        {
                            localFilePath = currentDirPath;
                            LogToFile($"Found backiee_content.html in current directory: {currentDirPath}");
                        }
                        else
                        {
                            LogToFile("File not found in any location. Returning empty list.");
                            return new List<WallpaperModel>();
                        }
                    }
                }
                
                _logger.LogInformation("Found local backiee_content.html at: {Path}", localFilePath);
                LogToFile($"Found local backiee_content.html at: {localFilePath}");
                
                string htmlContent = await File.ReadAllTextAsync(localFilePath);
                
                if (string.IsNullOrEmpty(htmlContent))
                {
                    _logger.LogWarning("Local backiee_content.html file is empty");
                    LogToFile("Local backiee_content.html file is empty");
                    return new List<WallpaperModel>();
                }
                
                _logger.LogInformation("Successfully read local backiee_content.html file, {Length} characters", htmlContent.Length);
                LogToFile($"Successfully read local backiee_content.html file, {htmlContent.Length} characters");
                
                // Log the first 200 characters for debugging
                LogToFile($"First 200 chars of HTML: {htmlContent.Substring(0, Math.Min(200, htmlContent.Length))}");
                
                // Process the HTML content to extract wallpapers
                var wallpapers = await ExtractWallpapersFromContentHtml(htmlContent);
                LogToFile($"Extraction complete. Found {wallpapers.Count} wallpapers.");
                return wallpapers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting wallpapers from local file: {Message}", ex.Message);
                LogToFile($"ERROR extracting wallpapers from local file: {ex.Message}\n{ex.StackTrace}");
                return new List<WallpaperModel>();
            }
        }

        /// <summary>
        /// Gets hardcoded wallpapers directly from specified URLs
        /// </summary>
        /// <returns>List of wallpaper models from hardcoded URLs</returns>
        public Task<List<WallpaperModel>> GetHardcodedWallpapers()
        {
            _logger.LogInformation("Using hardcoded wallpaper URLs");
            LogToFile("Using hardcoded wallpaper URLs");
            
            var wallpapers = new List<WallpaperModel>();
            
            // Hardcoded thumbnail URLs
            var thumbnailUrls = new List<string>
            {
                "https://backiee.com/static/wallpapers/560x315/418137.jpg",
                "https://backiee.com/static/wallpapers/560x315/418124.jpg",
                "https://backiee.com/static/wallpapers/560x315/418123.jpg",
                "https://backiee.com/static/wallpapers/560x315/418122.jpg"
            };
            
            // Titles for the wallpapers
            var titles = new List<string>
            {
                "Neon Nightfall An Anime Girl's Journey",
                "Samurai Serenity in Winter Wonderland",
                "Polar Companions in a Winter Realm",
                "Retrowave Dreamscape with Ethereal Pink Sky"
            };
            
            for (int i = 0; i < thumbnailUrls.Count; i++)
            {
                try
                {
                    string thumbnailUrl = thumbnailUrls[i];
                    LogToFile($"Processing hardcoded URL: {thumbnailUrl}");
                    
                    // Extract the ID from the URL (the number before .jpg)
                    var idMatch = Regex.Match(thumbnailUrl, @"/(\d+)\.jpg$");
                    if (!idMatch.Success)
                    {
                        _logger.LogWarning("Could not extract ID from URL: {Url}", thumbnailUrl);
                        LogToFile($"Could not extract ID from URL: {thumbnailUrl}");
                        continue;
                    }
                    
                    string id = idMatch.Groups[1].Value;
                    _logger.LogDebug("Extracted ID {Id} from URL {Url}", id, thumbnailUrl);
                    LogToFile($"Extracted ID {id} from URL {thumbnailUrl}");
                    
                    // Create the high-resolution URL
                    string fullSizeUrl = $"https://backiee.com/static/wallpapers/wide/{id}.jpg";
                    LogToFile($"Created high-res URL: {fullSizeUrl}");
                    
                    // Create the wallpaper model
                    var wallpaper = new WallpaperModel
                    {
                        Id = id,
                        Title = titles[i],
                        ImageUrl = fullSizeUrl,
                        ThumbnailUrl = thumbnailUrl,
                        SourceUrl = $"https://backiee.com/wallpaper/{id}",
                        Source = "Backiee",
                        Width = 1920,
                        Height = 1080,
                        ResolutionCategory = "HD",
                        UploadDate = DateTime.Now
                    };
                    
                    wallpapers.Add(wallpaper);
                    _logger.LogInformation("Added hardcoded wallpaper: {Title} with URL {Url}", titles[i], fullSizeUrl);
                    LogToFile($"Added hardcoded wallpaper: {titles[i]} with URL {fullSizeUrl}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing hardcoded wallpaper URL: {Url}", thumbnailUrls[i]);
                    LogToFile($"ERROR processing hardcoded wallpaper URL: {thumbnailUrls[i]}, Exception: {ex.Message}");
                }
            }
            
            LogToFile($"Finished processing hardcoded wallpapers. Count: {wallpapers.Count}");
            return Task.FromResult(wallpapers);
        }
    }
} 