using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;

namespace WallYouNeed.BackieeScraper
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Backiee Wallpaper Scraper");
            Console.WriteLine("-------------------------");

            try
            {
                await ScrapeBackieeWallpapers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task ScrapeBackieeWallpapers()
        {
            // Create HTTP client
            using (HttpClient client = new HttpClient())
            {
                // Add headers to simulate a browser request
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                
                // Fetch the main page
                Console.WriteLine("Fetching content from backiee.com...");
                HttpResponseMessage response = await client.GetAsync("https://backiee.com");
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to fetch the webpage. Status code: {response.StatusCode}");
                }

                // Read the content
                string html = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Content fetched successfully. Length: {html.Length} characters");

                // Save HTML for debugging purposes (optional)
                File.WriteAllText("backiee_latest.html", html);
                Console.WriteLine("Saved raw HTML to backiee_latest.html for debugging");

                // Extract wallpaper links from the first page
                var wallpapers = ExtractWallpaperLinks(html);
                
                // If we still don't have 20 wallpapers (which shouldn't happen with the improved pattern),
                // try page 2 as a fallback
                if (wallpapers.Count < 20)
                {
                    Console.WriteLine($"Only found {wallpapers.Count} wallpapers on first page, trying page 2...");
                    
                    // Fetch page 2
                    response = await client.GetAsync("https://backiee.com/?page=2");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string html2 = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Page 2 fetched successfully. Length: {html2.Length} characters");
                        
                        // Extract additional wallpapers, but only up to 20 total
                        int remainingCount = 20 - wallpapers.Count;
                        var additionalWallpapers = ExtractWallpaperLinks(html2, remainingCount);
                        
                        wallpapers.AddRange(additionalWallpapers);
                        Console.WriteLine($"Found {additionalWallpapers.Count} additional wallpapers from page 2");
                    }
                }
                
                Console.WriteLine($"Extracted a total of {wallpapers.Count} wallpaper links");

                // Save wallpapers as JSON
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                string jsonContent = JsonSerializer.Serialize(wallpapers, jsonOptions);
                File.WriteAllText("backiee_wallpapers.json", jsonContent);
                Console.WriteLine("Successfully saved wallpapers to backiee_wallpapers.json");
            }
        }

        static List<Wallpaper> ExtractWallpaperLinks(string html, int maxCount = 20)
        {
            var wallpapers = new List<Wallpaper>();
            var uniqueUrls = new HashSet<string>();

            // More comprehensive regex pattern that catches all wallpaper types
            // This pattern looks for <a> tags pointing to backiee.com/wallpaper/
            // and captures both the URL and the alt text from the image inside
            string pattern = @"<div class=""col-sm-3 col-md-3"">\s*<a href=""(https://backiee\.com/wallpaper/[^""]+)"">[^<]*(?:<img[^>]*>)*?.*?<div class=""placeholder"">\s*<img[^>]*alt=""([^""]+)""";
            
            // Additional pattern to catch category-specific wallpapers
            string categoryPattern = @"<a href=""(https://backiee\.com/wallpaper/[^""]+)"">.*?<img[^>]*alt=""([^""]+)""";
            
            // Find all divs with wallpapers
            var allDivs = FindWallpaperDivs(html);
            
            // Process each div to extract the wallpaper details
            int count = 0;
            foreach (var div in allDivs)
            {
                if (count >= maxCount) break;
                
                // Try to extract the wallpaper URL and title
                var wallpaper = ExtractWallpaperFromDiv(div);
                
                if (wallpaper != null && !uniqueUrls.Contains(wallpaper.SourceUrl))
                {
                    uniqueUrls.Add(wallpaper.SourceUrl);
                    wallpapers.Add(wallpaper);
                    Console.WriteLine($"Found wallpaper {count+1}: {wallpaper.SourceUrl}");
                    count++;
                }
            }

            return wallpapers;
        }
        
        static List<string> FindWallpaperDivs(string html)
        {
            var result = new List<string>();
            
            // Find all col-sm-3 col-md-3 divs (which contain wallpapers)
            string divPattern = @"<div class=""col-sm-3 col-md-3"">(.*?)<\/div>\s+<\/div>";
            MatchCollection matches = Regex.Matches(html, divPattern, RegexOptions.Singleline);
            
            foreach (Match match in matches)
            {
                result.Add(match.Groups[0].Value);
            }
            
            return result;
        }
        
        static Wallpaper ExtractWallpaperFromDiv(string div)
        {
            // Extract the URL
            string urlPattern = @"<a href=""(https://backiee\.com/wallpaper/[^""]+)""";
            Match urlMatch = Regex.Match(div, urlPattern);
            
            if (!urlMatch.Success) return null;
            
            string url = urlMatch.Groups[1].Value;
            
            // Extract the placeholder image URL from data-src attribute
            string imageSrcPattern = @"<div class=""placeholder""[^>]*?>\s*<img[^>]*?data-src=""([^""]+)""";
            Match imageSrcMatch = Regex.Match(div, imageSrcPattern);
            string imageUrl = imageSrcMatch.Success ? imageSrcMatch.Groups[1].Value : "";

            // Extract quality
            string quality = "";
            if (div.Contains("8k_logo.png"))
                quality = "8K";
            else if (div.Contains("5k_logo.png"))
                quality = "5K";
            else if (div.Contains("4k_logo.png"))
                quality = "4K";

            // Extract AI status
            bool aiStatus = div.Contains("aigenerated-icon.png");

            // Extract likes and downloads from image-likes div
            int likes = 0;
            int downloads = 0;
            string likesPattern = @"<div class=""image-likes""[^>]*?>.*?(\d+).*?(\d+).*?</div>";
            Match likesMatch = Regex.Match(div, likesPattern, RegexOptions.Singleline);
            if (likesMatch.Success)
            {
                likes = int.Parse(likesMatch.Groups[1].Value);
                downloads = int.Parse(likesMatch.Groups[2].Value);
            }

            // Extract ID from URL
            string id = url.Split('/').Last();
            
            return new Wallpaper
            {
                Id = id,
                Title = $"Backiee Wallpaper {id}",
                Name = $"Backiee Wallpaper {id}",
                Description = $"Wallpaper from Backiee.com",
                SourceUrl = url,
                ThumbnailUrl = imageUrl,
                Author = "Backiee.com",
                Source = aiStatus ? WallpaperSource.AI : WallpaperSource.Custom,
                CreatedAt = DateTime.Now,
                LastUsedAt = DateTime.Now,
                Metadata = new Dictionary<string, string>
                {
                    { "quality", quality },
                    { "likes", likes.ToString() },
                    { "downloads", downloads.ToString() }
                }
            };
        }

        static void SaveStaticImageUrls(List<Wallpaper> wallpapers, string filePath)
        {
            StringBuilder markdown = new StringBuilder();
            
            if (wallpapers.Count == 0)
            {
                // Just output an empty file if no wallpapers found
            }
            else
            {
                // Make sure we have exactly 20 wallpapers (or the max available)
                int count = Math.Min(20, wallpapers.Count);
                
                for (int i = 0; i < count; i++)
                {
                    if (!string.IsNullOrEmpty(wallpapers[i].ThumbnailUrl))
                    {
                        markdown.AppendLine($"{wallpapers[i].ThumbnailUrl}");
                    }
                }
            }
            
            File.WriteAllText(filePath, markdown.ToString());
        }

        static void SaveWallpaperPageUrls(List<Wallpaper> wallpapers, string filePath)
        {
            StringBuilder markdown = new StringBuilder();
            
            if (wallpapers.Count == 0)
            {
                // Just output an empty file if no wallpapers found
            }
            else
            {
                // Make sure we have exactly 20 wallpapers (or the max available)
                int count = Math.Min(20, wallpapers.Count);
                
                for (int i = 0; i < count; i++)
                {
                    markdown.AppendLine($"{wallpapers[i].SourceUrl}");
                }
            }
            
            File.WriteAllText(filePath, markdown.ToString());
        }
    }

    public class Wallpaper
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsLive { get; set; }
        public WallpaperSource Source { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastUsedAt { get; set; } = DateTime.Now;
        public bool IsFavorite { get; set; }
        public List<string> CollectionIds { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public enum WallpaperSource
    {
        Unsplash,
        Pexels,
        WallpaperEngine,
        Local,
        Custom,
        AI
    }
} 