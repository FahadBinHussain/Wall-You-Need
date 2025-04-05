using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace BackieeApiClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Backiee Wallpaper API Client");
            Console.WriteLine("----------------------------");

            // API endpoint URL
            string apiUrl = "https://backiee.com/api/wallpaper/list.php?action=paging_list&list_type=latest&page=1&page_size=30&category=all&is_ai=all&sort_by=popularity";

            try
            {
                // Create HTTP client
                using (HttpClient client = new HttpClient())
                {
                    // Set User-Agent header to avoid potential blocking
                    client.DefaultRequestHeaders.Add("User-Agent", "BackieeApiClient/1.0");
                    
                    Console.WriteLine($"Requesting data from {apiUrl}");
                    
                    // Send GET request to the API
                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    
                    // Check if the request was successful
                    response.EnsureSuccessStatusCode();
                    
                    // Read the response content
                    string jsonContent = await response.Content.ReadAsStringAsync();
                    
                    Console.WriteLine("Successfully retrieved data. Parsing JSON...");

                    // Deserialize JSON into list of WallpaperItem objects
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true
                    };

                    List<WallpaperItem>? wallpapers = JsonSerializer.Deserialize<List<WallpaperItem>>(
                        jsonContent,
                        options
                    );

                    if (wallpapers == null || wallpapers.Count == 0)
                    {
                        Console.WriteLine("No wallpapers found in the response.");
                        return;
                    }

                    // Display information about the wallpapers
                    Console.WriteLine($"Found {wallpapers.Count} wallpapers");
                    
                    // Display details of the first few wallpapers
                    int displayCount = Math.Min(5, wallpapers.Count);
                    for (int i = 0; i < displayCount; i++)
                    {
                        var wallpaper = wallpapers[i];
                        Console.WriteLine($"\nWallpaper {i + 1}:");
                        Console.WriteLine($"ID: {wallpaper.ID ?? "N/A"}");
                        Console.WriteLine($"Title: {wallpaper.Title ?? "N/A"}");
                        Console.WriteLine($"Category: {wallpaper.ThemeCat ?? "N/A"}");
                        Console.WriteLine($"Resolution: {wallpaper.Resolution ?? "N/A"}");
                        Console.WriteLine($"Rating: {wallpaper.Rating ?? "0"}");
                        Console.WriteLine($"Downloads: {wallpaper.Downloads ?? "0"}");
                        Console.WriteLine($"UltraHD: {(wallpaper.UltraHD == "1" ? "Yes" : "No")}");
                        if (wallpaper.UltraHD == "1")
                        {
                            Console.WriteLine($"UltraHD Type: {wallpaper.UltraHDType ?? "N/A"}");
                        }
                        Console.WriteLine($"Uploaded: {wallpaper.Uploaded ?? "N/A"}");
                        Console.WriteLine($"Image URL: {wallpaper.FullPhotoUrl ?? "N/A"}");
                        Console.WriteLine($"Page URL: {wallpaper.WallpaperUrl ?? "N/A"}");
                    }

                    // Save the prettified JSON to a file
                    string prettyJsonPath = "wallpapers_pretty.json";
                    var prettyJson = JsonSerializer.Serialize(wallpapers, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    await File.WriteAllTextAsync(prettyJsonPath, prettyJson);
                    Console.WriteLine($"\nPrettified JSON data saved to {Path.GetFullPath(prettyJsonPath)}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error occurred while making the HTTP request: {ex.Message}");
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error occurred while parsing JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}