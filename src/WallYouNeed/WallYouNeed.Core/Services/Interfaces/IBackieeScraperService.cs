using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WallYouNeed.Core.Models;

namespace WallYouNeed.Core.Services.Interfaces
{
    public interface IBackieeScraperService
    {
        event EventHandler<List<WallpaperModel>> NewWallpapersAdded;
        
        /// <summary>
        /// Starts the timer to periodically scrape wallpapers
        /// </summary>
        Task StartPeriodicUpdates();
        
        /// <summary>
        /// Stops the periodic updates
        /// </summary>
        void StopPeriodicUpdates();
        
        /// <summary>
        /// Scrapes the latest wallpapers from the homepage
        /// </summary>
        Task<List<WallpaperModel>> ScrapeLatestWallpapers();
        
        /// <summary>
        /// Scrapes wallpapers for a specific category
        /// </summary>
        /// <param name="category">The category to scrape</param>
        /// <param name="maxPages">Maximum number of pages to scrape</param>
        Task<List<WallpaperModel>> ScrapeWallpapersByCategory(string category, int maxPages = 3);
    }
} 