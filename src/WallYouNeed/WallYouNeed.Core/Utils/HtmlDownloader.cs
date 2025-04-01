using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.IO;

namespace WallYouNeed.Core.Utils
{
    /// <summary>
    /// Utility class for downloading and parsing HTML content
    /// </summary>
    public class HtmlDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HtmlDownloader> _logger;
        
        public HtmlDownloader(HttpClient httpClient, ILogger<HtmlDownloader> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }
        
        /// <summary>
        /// Downloads HTML content from the specified URL
        /// </summary>
        public async Task<string> DownloadHtmlAsync(string url)
        {
            try
            {
                _logger.LogDebug("Downloading HTML from: {Url}", url);
                
                // Create a new HttpRequestMessage with appropriate headers
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                
                // Rotate through different user agents to avoid being blocked
                var userAgents = new[]
                {
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:122.0) Gecko/20100101 Firefox/122.0",
                    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                    "Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1"
                };
                
                var random = new Random();
                var userAgent = userAgents[random.Next(userAgents.Length)];
                
                // Add common browser headers to avoid being blocked by anti-scraping measures
                request.Headers.Add("User-Agent", userAgent);
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                request.Headers.Add("Cache-Control", "no-cache");
                request.Headers.Add("Pragma", "no-cache");
                request.Headers.Add("Referer", "https://www.google.com/");
                
                // Send the request
                _logger.LogDebug("Using User-Agent: {UserAgent}", userAgent);
                var response = await _httpClient.SendAsync(request);
                
                // Check if we're being redirected
                if (response.StatusCode == System.Net.HttpStatusCode.Redirect || 
                    response.StatusCode == System.Net.HttpStatusCode.MovedPermanently ||
                    response.StatusCode == System.Net.HttpStatusCode.TemporaryRedirect)
                {
                    var redirectUrl = response.Headers.Location?.ToString();
                    if (!string.IsNullOrEmpty(redirectUrl))
                    {
                        _logger.LogInformation("Redirecting to: {RedirectUrl}", redirectUrl);
                        return await DownloadHtmlAsync(redirectUrl);
                    }
                }
                
                // Ensure we got a successful response
                response.EnsureSuccessStatusCode();
                
                // Get the HTML content
                string html = await response.Content.ReadAsStringAsync();
                
                if (string.IsNullOrWhiteSpace(html))
                {
                    _logger.LogWarning("Downloaded HTML is empty for URL: {Url}", url);
                    return string.Empty;
                }
                
                _logger.LogDebug("Successfully downloaded HTML ({Length} bytes) from: {Url}", html.Length, url);
                
                // Save the HTML for debugging
                try 
                {
                    string logDir = Path.Combine(Path.GetTempPath(), "WallYouNeed_Logs");
                    Directory.CreateDirectory(logDir);
                    string filename = $"html_raw_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileNameWithoutExtension(url)}.html";
                    string fullPath = Path.Combine(logDir, filename);
                    File.WriteAllText(fullPath, html);
                    _logger.LogInformation("Saved raw HTML to: {Path}", fullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save raw HTML log file");
                }
                
                return html;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading HTML from {Url}", url);
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Extracts text content from an HTML element using a simple regex-based approach
        /// </summary>
        public async Task<string> ExtractTextAsync(string url, string elementSelector)
        {
            var html = await DownloadHtmlAsync(url);
            if (string.IsNullOrEmpty(html))
                return string.Empty;
                
            // Simple regex-based selector implementation
            // This is a very basic implementation and will not work for all cases
            if (elementSelector.StartsWith("//"))
            {
                // Convert XPath-like syntax to a simpler format
                elementSelector = elementSelector.TrimStart('/');
                var parts = elementSelector.Split('/');
                string pattern = "";
                
                // Extremely simplified XPath-to-regex conversion
                // This only handles basic cases like "//div[@class='item']"
                foreach (var part in parts)
                {
                    if (string.IsNullOrEmpty(part)) continue;
                    
                    if (part.Contains("["))
                    {
                        var tagName = part.Split('[')[0];
                        var attribute = part.Split('[')[1].TrimEnd(']');
                        
                        if (attribute.StartsWith("@class"))
                        {
                            var className = attribute.Split('=')[1].Trim('\'', '"');
                            pattern = $"<{tagName}[^>]*class\\s*=\\s*['\"][^'\"]*{className}[^'\"]*['\"][^>]*>";
                        }
                        else if (attribute.StartsWith("@id"))
                        {
                            var id = attribute.Split('=')[1].Trim('\'', '"');
                            pattern = $"<{tagName}[^>]*id\\s*=\\s*['\"][^'\"]*{id}[^'\"]*['\"][^>]*>";
                        }
                        else if (attribute.StartsWith("@property"))
                        {
                            var propValue = attribute.Split('=')[1].Trim('\'', '"');
                            pattern = $"<{tagName}[^>]*property\\s*=\\s*['\"][^'\"]*{propValue}[^'\"]*['\"][^>]*>";
                        }
                    }
                    else
                    {
                        pattern = $"<{part}[^>]*>";
                    }
                }
                
                if (!string.IsNullOrEmpty(pattern))
                {
                    var match = Regex.Match(html, pattern);
                    if (match.Success)
                    {
                        // Find the closing tag for this element
                        int startPos = match.Index + match.Length;
                        int endPos = html.IndexOf($"</{parts[parts.Length - 1]}>", startPos);
                        
                        if (endPos > startPos)
                        {
                            var content = html.Substring(startPos, endPos - startPos);
                            // Remove HTML tags from the content
                            return Regex.Replace(content, "<[^>]*>", "").Trim();
                        }
                    }
                }
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// Extracts an attribute value from an HTML element
        /// </summary>
        public async Task<string> ExtractAttributeAsync(string url, string elementSelector, string attributeName)
        {
            var html = await DownloadHtmlAsync(url);
            if (string.IsNullOrEmpty(html))
                return string.Empty;
                
            // Simple implementation for specific cases we need
            // This is tailored for the use cases in BackieeScraperService
            
            // Handle the download button case
            if (elementSelector.Contains("download-button") && attributeName == "href")
            {
                var match = Regex.Match(html, "<a[^>]*class\\s*=\\s*['\"][^'\"]*download-button[^'\"]*['\"][^>]*href\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>");
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }
            
            // Handle the meta image case
            if (elementSelector.Contains("meta") && elementSelector.Contains("og:image") && attributeName == "content")
            {
                var match = Regex.Match(html, "<meta[^>]*property\\s*=\\s*['\"]og:image['\"][^>]*content\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>");
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }
            
            // Handle twitter image extraction
            if (elementSelector.Contains("meta") && elementSelector.Contains("twitter:image") && attributeName == "content")
            {
                var match = Regex.Match(html, "<meta[^>]*name\\s*=\\s*['\"]twitter:image['\"][^>]*content\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>");
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }
            
            // Handle image src extraction
            if (elementSelector.Contains("img") && attributeName == "src")
            {
                var match = Regex.Match(html, "<img[^>]*src\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>");
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
                
                // Also look for data-src for lazy-loaded images
                match = Regex.Match(html, "<img[^>]*data-src\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>");
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }
            
            // Handle link href extraction
            if (elementSelector.Contains("a") && attributeName == "href")
            {
                var match = Regex.Match(html, "<a[^>]*href\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>");
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// Finds all elements matching a selector in the HTML
        /// </summary>
        public async Task<List<string>> FindElementsAsync(string url, string elementSelector)
        {
            var html = await DownloadHtmlAsync(url);
            var results = new List<string>();
            
            if (string.IsNullOrEmpty(html))
                return results;
            
            _logger.LogDebug("Finding elements with selector: {Selector}", elementSelector);
                
            // First, save the HTML to a log file for inspection
            try 
            {
                string logDir = Path.Combine(Path.GetTempPath(), "WallYouNeed_Logs");
                Directory.CreateDirectory(logDir);
                string filename = $"html_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileNameWithoutExtension(url)}.html";
                string fullPath = Path.Combine(logDir, filename);
                File.WriteAllText(fullPath, html);
                _logger.LogInformation("Saved HTML to log file: {Path}", fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save HTML log file");
            }
            
            // First try a direct approach for the specific pattern we observed on backiee.com
            if (url.Contains("backiee"))
            {
                _logger.LogDebug("Using specialized backiee.com patterns");
                
                // These patterns are specifically tailored for backiee.com
                var backieePatterns = new[]
                {
                    // Exact pattern we observed in the HTML sample
                    "<div\\s+class\\s*=\\s*['\"]col-sm-3\\s+col-md-3['\"][^>]*>.*?</div>\\s*</div>",
                    
                    // Alternative with slightly different spacing
                    "<div\\s+class\\s*=\\s*['\"]col-sm-3 col-md-3['\"][^>]*>.*?</div>",
                    
                    // Even more flexible pattern
                    "<div[^>]*class\\s*=\\s*['\"][^'\"]*col-sm-3[^'\"]*['\"][^>]*>.*?</div>",
                    
                    // Targeting the wallpaper item structure
                    "<a\\s+href\\s*=\\s*['\"]https?://backiee\\.com/wallpaper/[^'\"]+['\"][^>]*>.*?</a>\\s*<div\\s+class\\s*=\\s*['\"]image-likes['\"][^>]*>.*?</div>",
                };
                
                foreach (var pattern in backieePatterns)
                {
                    var matches = Regex.Matches(html, pattern, RegexOptions.Singleline);
                    
                    if (matches.Count > 0)
                    {
                        _logger.LogInformation("Found {Count} matches with backiee-specific pattern: {Pattern}", matches.Count, pattern);
                        foreach (Match match in matches)
                        {
                            results.Add(match.Value);
                        }
                        
                        // If we found matches, no need to try other patterns
                        if (results.Count > 0)
                            break;
                    }
                }
                
                // If we still didn't find anything, try targeting <a> tags with wallpaper links
                if (results.Count == 0)
                {
                    _logger.LogDebug("No matches found with backiee-specific patterns, trying wallpaper link extraction");
                    
                    var wallpaperLinkPattern = "<a\\s+href\\s*=\\s*['\"]https?://backiee\\.com/wallpaper/[^'\"]+['\"][^>]*>.*?</a>";
                    var linkMatches = Regex.Matches(html, wallpaperLinkPattern, RegexOptions.Singleline);
                    
                    if (linkMatches.Count > 0)
                    {
                        _logger.LogInformation("Found {Count} wallpaper links", linkMatches.Count);
                        foreach (Match match in linkMatches)
                        {
                            results.Add(match.Value);
                        }
                    }
                }
                
                // If that still didn't work, try a much broader approach
                if (results.Count == 0)
                {
                    _logger.LogDebug("No matches found with specific patterns, trying broader approach");
                    
                    // Look for any divs with classes that look like gallery items
                    var result = Regex.Matches(html, "<div[^>]*class\\s*=\\s*['\"][^'\"]*(?:col|gallery|item|wallpaper|card)[^'\"]*['\"][^>]*>.*?</div>", RegexOptions.Singleline);
                    
                    if (result.Count > 0)
                    {
                        _logger.LogInformation("Found {Count} potential gallery items with broader pattern", result.Count);
                        foreach (Match match in result)
                        {
                            // Only add if it contains an image
                            if (match.Value.Contains("<img") && match.Value.Length < 2000) // Not too large
                            {
                                results.Add(match.Value);
                            }
                        }
                    }
                }
            }
            
            // If we still don't have results, fall back to the standard approach
            if (results.Count == 0)
            {
                // Handle div with class="item" case specifically for Backiee
                if (elementSelector.Contains("div") && elementSelector.Contains("class") && 
                    (elementSelector.Contains("item") || elementSelector.Contains("item-container")))
                {
                    // Extract the class name from the selector (item or item-container)
                    string className = "item";
                    if (elementSelector.Contains("item-container"))
                    {
                        className = "item-container";
                    }
                    
                    _logger.LogDebug("Searching for elements with class: {ClassName}", className);
                    
                    // Try different patterns to match wallpaper containers
                    var patterns = new[]
                    {
                        // Standard pattern
                        $"<div[^>]*class\\s*=\\s*['\"][^'\"]*{className}[^'\"]*['\"][^>]*>.*?</div>",
                        
                        // Pattern for bootstrap grid layouts (new site structure)
                        "<div[^>]*class\\s*=\\s*['\"][^'\"]*col-sm-\\d+[^'\"]*['\"][^>]*>.*?</div>",
                        
                        // Pattern for bootstrap grid layouts (new site structure)
                        "<div[^>]*class\\s*=\\s*['\"][^'\"]*col-md-\\d+[^'\"]*['\"][^>]*>.*?</div>",
                        
                        // Alternative pattern for card-based layouts
                        "<div[^>]*class\\s*=\\s*['\"][^'\"]*card[^'\"]*['\"][^>]*>.*?</div>",
                        
                        // Grid item patterns (common in modern gallery layouts)
                        "<div[^>]*class\\s*=\\s*['\"][^'\"]*(?:grid-item|gallery-item|tile|cell)[^'\"]*['\"][^>]*>.*?</div>",
                        
                        // Broader pattern for any container with wallpaper-related class
                        "<div[^>]*class\\s*=\\s*['\"][^'\"]*(?:wallpaper|wall|preview|image|photo)[^'\"]*['\"][^>]*>.*?</div>",
                        
                        // Pattern for wrapper elements
                        "<article[^>]*class\\s*=\\s*['\"][^'\"]*(?:wallpaper|item|wall|preview)[^'\"]*['\"][^>]*>.*?</article>",
                        
                        // Common gallery layouts
                        "<div[^>]*class\\s*=\\s*['\"][^'\"]*(?:col|column|box|thumbnail)[^'\"]*['\"][^>]*>.*?</div>",
                        
                        // List item based layouts
                        "<li[^>]*class\\s*=\\s*['\"][^'\"]*(?:item|image|photo|wallpaper)[^'\"]*['\"][^>]*>.*?</li>"
                    };
                    
                    foreach (var pattern in patterns)
                    {
                        var matches = Regex.Matches(html, pattern, RegexOptions.Singleline);
                        if (matches.Count > 0)
                        {
                            _logger.LogInformation("Found {Count} matches with pattern: {Pattern}", matches.Count, pattern);
                            foreach (Match match in matches)
                            {
                                results.Add(match.Value);
                            }
                            
                            // If we found matches, no need to try other patterns
                            if (results.Count > 0)
                                break;
                        }
                    }
                    
                    // If we still didn't find anything, look for image tags inside links as a last resort
                    if (results.Count == 0)
                    {
                        _logger.LogDebug("No matches found with standard patterns, trying image/link extraction");
                        
                        // Look for <a> tags containing <img> tags with certain attributes
                        var linkPatterns = new[]
                        {
                            // Standard image inside link
                            "<a[^>]*href\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>.*?<img[^>]*src\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>.*?</a>",
                            
                            // Images with data-src (common in lazy-loaded galleries)
                            "<a[^>]*href\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>.*?<img[^>]*data-src\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>.*?</a>",
                            
                            // Background image elements
                            "<a[^>]*href\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>.*?<div[^>]*style\\s*=\\s*['\"][^'\"]*background(?:-image)?\\s*:\\s*url\\(['\"]?([^'\")]*)['\"]?\\)[^'\"]*['\"][^>]*>.*?</div>.*?</a>",
                            
                            // Direct image links (no surrounding element)
                            "<img[^>]*src\\s*=\\s*['\"]([^'\"]*(?:wallpaper|background|large|full|original)[^'\"]*)['\"\\?][^>]*>"
                        };
                        
                        foreach (var pattern in linkPatterns)
                        {
                            var linkMatches = Regex.Matches(html, pattern, RegexOptions.Singleline);
                            
                            if (linkMatches.Count > 0)
                            {
                                _logger.LogInformation("Found {Count} image links with pattern: {Pattern}", linkMatches.Count, pattern);
                                
                                foreach (Match match in linkMatches)
                                {
                                    // Extract the image source (group 2 in most patterns)
                                    string imgSrc = match.Groups.Count > 2 ? match.Groups[2].Value : string.Empty;
                                    
                                    // If pattern doesn't have a second group, try the first one
                                    if (string.IsNullOrEmpty(imgSrc) && match.Groups.Count > 1)
                                    {
                                        imgSrc = match.Groups[1].Value;
                                    }
                                    
                                    // Only include if the image looks like a wallpaper (check path)
                                    if (!string.IsNullOrEmpty(imgSrc) && (
                                        imgSrc.Contains("wallpaper") || imgSrc.Contains("background") || 
                                        imgSrc.Contains("preview") || imgSrc.Contains("thumb") ||
                                        imgSrc.Contains("large") || imgSrc.Contains("full") ||
                                        imgSrc.Contains("original") || imgSrc.Contains(".jpg") ||
                                        imgSrc.Contains(".png") || imgSrc.Contains(".jpeg")))
                                    {
                                        results.Add(match.Value);
                                    }
                                }
                                
                                // If we found matches, break the loop
                                if (results.Count > 0)
                                    break;
                            }
                        }
                    }
                }
            }
            
            _logger.LogInformation("Found {Count} matching elements in total", results.Count);
            return results;
        }
        
        /// <summary>
        /// Extracts text from an HTML element
        /// </summary>
        public string ExtractTextFromElement(string html, string elementType)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;
                
            var pattern = $"<{elementType}[^>]*>(.*?)</{elementType}>";
            var match = Regex.Match(html, pattern, RegexOptions.Singleline);
            
            if (match.Success && match.Groups.Count > 1)
            {
                // Remove any HTML tags from the extracted text
                return Regex.Replace(match.Groups[1].Value, "<[^>]*>", "").Trim();
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// Extracts an attribute value from an HTML element
        /// </summary>
        public string ExtractAttributeFromElement(string html, string attributeName)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;
                
            var pattern = $"{attributeName}\\s*=\\s*['\"]([^'\"]*)['\"]";
            var match = Regex.Match(html, pattern);
            
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }
            
            return string.Empty;
        }
    }
} 