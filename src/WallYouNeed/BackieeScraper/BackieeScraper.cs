using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

                // Save the wallpaper links to a markdown file
                SaveToMarkdown(wallpapers, "backiee_wallpapers.md");
                Console.WriteLine("Successfully saved wallpaper links to backiee_wallpapers.md");
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
                
                if (wallpaper != null && !uniqueUrls.Contains(wallpaper.Url))
                {
                    uniqueUrls.Add(wallpaper.Url);
                    wallpapers.Add(wallpaper);
                    Console.WriteLine($"Found wallpaper {count+1}: {wallpaper.Title}");
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
            
            // Find the title directly from the div box
            string boxPattern = @"<div class=""box mt-2"">\s*<div class=""max-linese"">(.*?)<\/div>";
            Match boxMatch = Regex.Match(div, boxPattern);
            
            if (boxMatch.Success)
            {
                string title = boxMatch.Groups[1].Value.Trim();
                if (title.EndsWith(" wallpaper"))
                {
                    title = title.Substring(0, title.Length - 10);
                }
                return new Wallpaper
                {
                    Url = url,
                    Title = title
                };
            }
            
            // Fallback: Extract from URL
            string[] urlParts = url.Split('/');
            if (urlParts.Length >= 2)
            {
                string slug = urlParts[urlParts.Length - 2];
                // Convert slug to title
                string title = string.Join(" ", slug.Split('-')
                    .Select(word => char.ToUpper(word[0]) + word.Substring(1)));
                
                return new Wallpaper
                {
                    Url = url,
                    Title = title
                };
            }
            
            return new Wallpaper
            {
                Url = url,
                Title = "Wallpaper"  // Default title
            };
        }

        static void SaveToMarkdown(List<Wallpaper> wallpapers, string filePath)
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
                    markdown.AppendLine($"{wallpapers[i].Url}");
                }
            }
            
            File.WriteAllText(filePath, markdown.ToString());
        }
    }

    public class Wallpaper
    {
        public string Url { get; set; }
        public string Title { get; set; }
    }
} 