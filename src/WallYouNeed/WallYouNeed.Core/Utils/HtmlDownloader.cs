using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
                string html = await _httpClient.GetStringAsync(url);
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
            
            // Handle image src extraction
            if (elementSelector.Contains("img") && attributeName == "src")
            {
                var match = Regex.Match(html, "<img[^>]*src\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>");
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
                
            // Handle div with class="item" case specifically
            if (elementSelector.Contains("div") && elementSelector.Contains("class") && elementSelector.Contains("item"))
            {
                var matches = Regex.Matches(html, "<div[^>]*class\\s*=\\s*['\"][^'\"]*item[^'\"]*['\"][^>]*>.*?</div>", RegexOptions.Singleline);
                foreach (Match match in matches)
                {
                    results.Add(match.Value);
                }
            }
            
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