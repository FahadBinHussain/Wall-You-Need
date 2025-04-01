using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using HtmlAgilityPack;

namespace WallYouNeed.Core.Utils
{
    /// <summary>
    /// Utility class for downloading and parsing HTML content
    /// </summary>
    public class HtmlDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HtmlDownloader> _logger;
        private readonly Random _random = new Random();
        
        private readonly string[] _userAgents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36 Edg/123.0.0.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.6 Safari/605.1.15",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:124.0) Gecko/20100101 Firefox/124.0",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_4_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1"
        };
        
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
                _logger.LogInformation("Downloading HTML from {Url}", url);
                
                // Set a random User-Agent to avoid being detected as a bot
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd(_userAgents[_random.Next(_userAgents.Length)]);
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
                request.Headers.Add("Accept-Language", "en-US,en;q=0.5");
                request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                request.Headers.Add("Upgrade-Insecure-Requests", "1");
                request.Headers.Add("Sec-Fetch-Dest", "document");
                request.Headers.Add("Sec-Fetch-Mode", "navigate");
                request.Headers.Add("Sec-Fetch-Site", "none");
                request.Headers.Add("Sec-Fetch-User", "?1");
                request.Headers.Add("Cache-Control", "max-age=0");

                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to download HTML from {Url}. Status: {StatusCode}", url, response.StatusCode);
                    return string.Empty;
                }

                var html = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Successfully downloaded {Length} bytes from {Url}", html.Length, url);
                return html;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading HTML from {Url}: {Message}", url, ex.Message);
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Extracts text content from an HTML element using a simple regex-based approach
        /// </summary>
        public async Task<string> ExtractTextAsync(string url, string elementSelector)
        {
            try
            {
                var html = await DownloadHtmlAsync(url);
                if (string.IsNullOrEmpty(html))
                {
                    return string.Empty;
                }

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                
                var node = htmlDoc.DocumentNode.SelectSingleNode(elementSelector);
                if (node != null)
                {
                    return node.InnerText.Trim();
                }
                
                _logger.LogWarning("Element selector '{Selector}' not found in HTML from {Url}", elementSelector, url);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text using selector '{Selector}' from {Url}: {Message}", 
                    elementSelector, url, ex.Message);
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Extracts an attribute value from an HTML element
        /// </summary>
        public async Task<string> ExtractAttributeAsync(string url, string elementSelector, string attributeName)
        {
            try
            {
                var html = await DownloadHtmlAsync(url);
                if (string.IsNullOrEmpty(html))
                {
                    return string.Empty;
                }

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                
                var node = htmlDoc.DocumentNode.SelectSingleNode(elementSelector);
                if (node != null)
                {
                    return node.GetAttributeValue(attributeName, string.Empty);
                }
                
                _logger.LogWarning("Element selector '{Selector}' not found for attribute '{Attribute}' in HTML from {Url}", 
                    elementSelector, attributeName, url);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting attribute '{Attribute}' using selector '{Selector}' from {Url}: {Message}", 
                    attributeName, elementSelector, url, ex.Message);
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Finds all elements matching a selector in the HTML
        /// </summary>
        public async Task<List<string>> FindElementsAsync(string url, string elementSelector)
        {
            try
            {
                var html = await DownloadHtmlAsync(url);
                if (string.IsNullOrEmpty(html))
                {
                    return new List<string>();
                }

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                
                var nodes = htmlDoc.DocumentNode.SelectNodes(elementSelector);
                if (nodes != null && nodes.Count > 0)
                {
                    _logger.LogInformation("Found {Count} elements matching selector '{Selector}' in {Url}", 
                        nodes.Count, elementSelector, url);
                    return nodes.Select(n => n.OuterHtml).ToList();
                }
                
                // Try different selectors if the main one fails
                if (elementSelector.Contains("@class"))
                {
                    var alternativeSelector = elementSelector.Replace("@class", "@class contains");
                    nodes = htmlDoc.DocumentNode.SelectNodes(alternativeSelector);
                    if (nodes != null && nodes.Count > 0)
                    {
                        _logger.LogInformation("Found {Count} elements using alternative selector '{Selector}' in {Url}", 
                            nodes.Count, alternativeSelector, url);
                        return nodes.Select(n => n.OuterHtml).ToList();
                    }
                }
                
                _logger.LogWarning("No elements found matching selector '{Selector}' in {Url}", elementSelector, url);
                return new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding elements using selector '{Selector}' from {Url}: {Message}", 
                    elementSelector, url, ex.Message);
                return new List<string>();
            }
        }
        
        /// <summary>
        /// Extracts text from an HTML element
        /// </summary>
        public string ExtractTextFromElement(string html, string elementType)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;
                
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                
                var node = doc.DocumentNode.SelectSingleNode("//" + elementType);
                return node?.InnerText.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from element {ElementType}: {Message}", elementType, ex.Message);
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Extracts an attribute value from an HTML element
        /// </summary>
        public string ExtractAttributeFromElement(string html, string attributeName)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;
                
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                
                var node = doc.DocumentNode.SelectSingleNode("//*[@" + attributeName + "]");
                return node?.GetAttributeValue(attributeName, string.Empty) ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting attribute {AttributeName} from element: {Message}", attributeName, ex.Message);
                return string.Empty;
            }
        }
    }
} 