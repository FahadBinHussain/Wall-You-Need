using System;

namespace WallYouNeed.Core.Configuration
{
    public class BackieeScraperConfig
    {
        /// <summary>
        /// The base URL for Backiee.com
        /// </summary>
        public string BaseUrl { get; set; } = "https://backiee.com";

        /// <summary>
        /// The interval in milliseconds between scraping operations (default: 1 hour)
        /// </summary>
        public int ScrapingInterval { get; set; } = 3600000;

        /// <summary>
        /// Maximum number of pages to scrape per category
        /// </summary>
        public int MaxPagesPerCategory { get; set; } = 3;

        /// <summary>
        /// Maximum number of concurrent requests
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = 2;

        /// <summary>
        /// Delay between requests in milliseconds to avoid rate limiting
        /// </summary>
        public int RequestDelayMs { get; set; } = 1000;

        /// <summary>
        /// Whether to automatically start scraping on initialization
        /// </summary>
        public bool AutoStartScraping { get; set; } = true;

        /// <summary>
        /// Categories to scrape
        /// </summary>
        public string[] CategoriesToScrape { get; set; } = new string[]
        {
            "abstract",
            "animals",
            "anime",
            "cars",
            "city",
            "fantasy",
            "flowers",
            "food",
            "holidays",
            "landscape",
            "minimalistic",
            "motorcycles",
            "movies",
            "nature",
            "space",
            "sport"
        };
    }
} 