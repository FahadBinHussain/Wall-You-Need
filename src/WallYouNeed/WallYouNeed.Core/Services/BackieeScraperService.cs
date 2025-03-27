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
                
                // Scrape the first page to get the latest wallpapers
                var latestWallpapers = await ScrapeWallpaperPage(config.BaseUrl);
                wallpapers.AddRange(latestWallpapers);
                
                if (wallpapers.Any())
                {
                    _logger.LogInformation("Found {Count} wallpapers from the latest section", wallpapers.Count);
                    NewWallpapersAdded?.Invoke(this, wallpapers);
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
                var html = await _htmlDownloader.DownloadHtmlAsync(url);
                if (string.IsNullOrEmpty(html))
                {
                    return wallpapers;
                }
                
                // Get all wallpaper items
                var wallpaperElements = await _htmlDownloader.FindElementsAsync(url, "//div[contains(@class, 'item')]");
                
                if (wallpaperElements == null || !wallpaperElements.Any())
                {
                    return wallpapers;
                }
                
                foreach (var element in wallpaperElements)
                {
                    try
                    {
                        // Extract data
                        string title = _htmlDownloader.ExtractTextFromElement(element, "h3");
                        string detailUrl = _htmlDownloader.ExtractAttributeFromElement(element, "href");
                        string thumbnailUrl = _htmlDownloader.ExtractAttributeFromElement(element, "src");
                        
                        if (string.IsNullOrEmpty(detailUrl) || string.IsNullOrEmpty(thumbnailUrl))
                        {
                            continue;
                        }
                        
                        // Create URL with base if it's relative
                        if (!detailUrl.StartsWith("http"))
                        {
                            var config = await _configService.GetBackieeConfigAsync();
                            detailUrl = $"{config.BaseUrl.TrimEnd('/')}/{detailUrl.TrimStart('/')}";
                        }
                        
                        // Extract resolution from title
                        var match = Regex.Match(title, @"(\d+)\s*[xX]\s*(\d+)");
                        int width = 0, height = 0;
                        
                        if (match.Success && match.Groups.Count >= 3)
                        {
                            int.TryParse(match.Groups[1].Value, out width);
                            int.TryParse(match.Groups[2].Value, out height);
                        }
                        
                        // Determine resolution category
                        string resolutionCategory = DetermineResolutionCategory(width, height);
                        
                        // Extract image URL from detail page
                        string imageUrl = await ExtractImageUrlFromDetailPage(detailUrl);
                        
                        if (string.IsNullOrEmpty(imageUrl))
                        {
                            continue;
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
                        
                        wallpapers.Add(wallpaper);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing wallpaper item");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping page: {Url}", url);
            }
            
            return wallpapers;
        }
        
        /// <summary>
        /// Extracts the full-size image URL from the detail page
        /// </summary>
        private async Task<string> ExtractImageUrlFromDetailPage(string detailUrl)
        {
            try
            {
                // First try to find the download button
                var downloadUrl = await _htmlDownloader.ExtractAttributeAsync(detailUrl, "//a[contains(@class, 'download-button')]", "href");
                
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    if (!downloadUrl.StartsWith("http"))
                    {
                        var config = await _configService.GetBackieeConfigAsync();
                        downloadUrl = $"{config.BaseUrl.TrimEnd('/')}/{downloadUrl.TrimStart('/')}";
                    }
                    
                    return downloadUrl;
                }
                
                // Alternative: try to find the original image in the meta tags
                var imageUrl = await _htmlDownloader.ExtractAttributeAsync(detailUrl, "//meta[@property='og:image']", "content");
                
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    if (!imageUrl.StartsWith("http"))
                    {
                        var config = await _configService.GetBackieeConfigAsync();
                        imageUrl = $"{config.BaseUrl.TrimEnd('/')}/{imageUrl.TrimStart('/')}";
                    }
                    
                    return imageUrl;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting image URL from detail page: {Url}", detailUrl);
                return null;
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
    }
} 