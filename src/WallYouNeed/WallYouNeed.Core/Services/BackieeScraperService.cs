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
                    _timer = new Timer(async _ => await ScrapeLatestWallpapers(), null, 0, config.ScrapingInterval);
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
        /// Scrapes the latest wallpapers from the homepage
        /// </summary>
        public async Task<List<WallpaperModel>> ScrapeLatestWallpapers()
        {
            lock (_lock)
            {
                if (_isScrapingInProgress)
                {
                    _logger.LogWarning("Scraping already in progress, skipping this request");
                    return new List<WallpaperModel>();
                }
                
                _isScrapingInProgress = true;
            }

            try
            {
                var config = await _configService.GetBackieeConfigAsync();
                var wallpapers = new List<WallpaperModel>();
                
                _logger.LogInformation("Scraping latest wallpapers from Backiee.com");
                
                // Save diagnostic information
                try
                {
                    string logDir = Path.Combine(Path.GetTempPath(), "WallYouNeed_Logs");
                    Directory.CreateDirectory(logDir);
                    string filename = $"scraper_diagnostic_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    string fullPath = Path.Combine(logDir, filename);
                    
                    File.WriteAllText(fullPath, $"Starting scrape at {DateTime.Now}\n");
                    File.AppendAllText(fullPath, $"Base URL: {config.BaseUrl}\n");
                    File.AppendAllText(fullPath, $"Alternative URLs: {string.Join(", ", config.AlternativeUrls)}\n");
                    
                    _logger.LogInformation("Created diagnostic log file: {Path}", fullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create diagnostic log file");
                }
                
                // Try the primary URL first
                _logger.LogInformation("Trying primary URL: {Url}", config.BaseUrl);
                var latestWallpapers = await ScrapeWallpaperPage(config.BaseUrl);
                
                // If primary URL doesn't yield results, try alternative URLs
                if (!latestWallpapers.Any() && config.AlternativeUrls != null && config.AlternativeUrls.Any())
                {
                    _logger.LogInformation("Primary URL didn't yield wallpapers, trying alternative URLs");
                    
                    foreach (var alternativeUrl in config.AlternativeUrls)
                    {
                        _logger.LogInformation("Trying alternative URL: {Url}", alternativeUrl);
                        latestWallpapers = await ScrapeWallpaperPage(alternativeUrl);
                        
                        if (latestWallpapers.Any())
                        {
                            _logger.LogInformation("Found wallpapers from alternative URL: {Url}", alternativeUrl);
                            break;
                        }
                    }
                    
                    // If still no results, try adding /wallpapers or /latest to the base URL
                    if (!latestWallpapers.Any())
                    {
                        _logger.LogInformation("Trying with specific paths added to base URL");
                        string[] additionalPaths = { "/wallpapers", "/latest", "/wallpaper", "/popular" };
                        
                        foreach (var path in additionalPaths)
                        {
                            var urlWithPath = $"{config.BaseUrl.TrimEnd('/')}{path}";
                            _logger.LogInformation("Trying URL with path: {Url}", urlWithPath);
                            
                            latestWallpapers = await ScrapeWallpaperPage(urlWithPath);
                            
                            if (latestWallpapers.Any())
                            {
                                _logger.LogInformation("Found wallpapers from URL with path: {Url}", urlWithPath);
                                break;
                            }
                        }
                    }
                }
                
                // If we still have no wallpapers, let's try one more approach - analyze HTML directly
                if (!latestWallpapers.Any())
                {
                    _logger.LogWarning("All regular scraping approaches failed. Performing direct HTML analysis for diagnostics...");
                    await AnalyzeHtmlForDebug(config.BaseUrl);
                }
                
                // Add found wallpapers to the list
                wallpapers.AddRange(latestWallpapers);
                
                if (wallpapers.Any())
                {
                    _logger.LogInformation("Found {Count} wallpapers from the latest section", wallpapers.Count);
                    NewWallpapersAdded?.Invoke(this, wallpapers);
                }
                else
                {
                    _logger.LogWarning("No wallpapers found from any URL. Check if website structure has changed or site is inaccessible.");
                }
                
                // Save diagnostic information about found wallpapers
                try
                {
                    if (wallpapers.Any())
                    {
                        string logDir = Path.Combine(Path.GetTempPath(), "WallYouNeed_Logs");
                        Directory.CreateDirectory(logDir);
                        string filename = $"found_wallpapers_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                        string fullPath = Path.Combine(logDir, filename);
                        
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine($"Found {wallpapers.Count} wallpapers from {config.BaseUrl} at {DateTime.Now}");
                        
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping latest wallpapers");
                return new List<WallpaperModel>();
            }
            finally
            {
                lock (_lock)
                {
                    _isScrapingInProgress = false;
                }
            }
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
                    
                    var pageWallpapers = await ScrapeWallpaperPage(url, category);
                    
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
        /// Scrapes a single page of wallpapers
        /// </summary>
        private async Task<List<WallpaperModel>> ScrapeWallpaperPage(string url, string category = null)
        {
            var wallpapers = new List<WallpaperModel>();
            
            try
            {
                _logger.LogInformation("Starting to scrape page: {Url}", url);
                
                var html = await _htmlDownloader.DownloadHtmlAsync(url);
                if (string.IsNullOrEmpty(html))
                {
                    _logger.LogWarning("Downloaded HTML is empty for URL: {Url}", url);
                    return wallpapers;
                }
                
                _logger.LogDebug("Successfully downloaded HTML content of length: {Length}", html.Length);
                
                // Get all wallpaper items
                var wallpaperElements = await _htmlDownloader.FindElementsAsync(url, "//div[contains(@class, 'item')]");
                
                if (wallpaperElements == null || !wallpaperElements.Any())
                {
                    _logger.LogWarning("No wallpaper elements found on page: {Url}", url);
                    
                    // Try an alternative pattern for newer site design
                    var alternativePattern = "item-container";
                    _logger.LogInformation("Trying alternative pattern: {Pattern}", alternativePattern);
                    
                    // Use a more flexible approach to find wallpaper items
                    wallpaperElements = await _htmlDownloader.FindElementsAsync(url, $"//div[contains(@class, '{alternativePattern}')]");
                    
                    if (wallpaperElements == null || !wallpaperElements.Any())
                    {
                        _logger.LogWarning("Alternative pattern also found no elements. Attempting direct regex search...");
                        
                        // Try a direct regex approach - match common patterns for wallpaper items
                        var directRegex = new Regex("<div[^>]*class\\s*=\\s*['\"][^'\"]*(?:item|card|wallpaper|preview)[^'\"]*['\"][^>]*>.*?</div>", RegexOptions.Singleline);
                        var matches = directRegex.Matches(html);
                        
                        if (matches.Count > 0)
                        {
                            _logger.LogInformation("Found {Count} elements using direct regex", matches.Count);
                            wallpaperElements = new List<string>();
                            foreach (Match match in matches)
                            {
                                wallpaperElements.Add(match.Value);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("No wallpaper elements found using any method. Trying direct HTML extraction as a last resort.");
                            
                            // Direct HTML extraction as a last resort
                            var directWallpapers = await ExtractWallpapersDirectlyFromHtml(html, url, category);
                            if (directWallpapers.Any())
                            {
                                _logger.LogInformation("Successfully extracted {Count} wallpapers directly from HTML.", directWallpapers.Count);
                                return directWallpapers;
                            }
                            
                            _logger.LogWarning("All extraction methods failed. Returning empty result.");
                            return wallpapers;
                        }
                    }
                }
                
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
            long pixels = (long)width * height;
            
            if (width >= 7680 || height >= 4320 || pixels >= 33177600) // 7680x4320 or higher
            {
                return "8K";
            }
            else if (width >= 3840 || height >= 2160 || pixels >= 8294400) // 3840x2160 or higher
            {
                return "4K";
            }
            else if (width >= 2560 || height >= 1440 || pixels >= 3686400) // 2560x1440 or higher
            {
                return "2K";
            }
            else if (width >= 1920 || height >= 1080 || pixels >= 2073600) // 1920x1080 or higher
            {
                return "FullHD";
            }
            else if (width >= 1280 || height >= 720 || pixels >= 921600) // 1280x720 or higher
            {
                return "HD";
            }
            else
            {
                return "Other";
            }
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
    }
} 