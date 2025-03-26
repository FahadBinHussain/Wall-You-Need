using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WallYouNeed.Core.Models;

namespace WallYouNeed.Core.Services.Interfaces;

/// <summary>
/// Interface for working with wallpapers
/// </summary>
public interface IWallpaperService
{
    /// <summary>
    /// Get all wallpapers
    /// </summary>
    Task<IEnumerable<Wallpaper>> GetAllWallpapersAsync();
    
    /// <summary>
    /// Get wallpapers by source type
    /// </summary>
    Task<IEnumerable<Wallpaper>> GetWallpapersBySourceAsync(WallpaperSource source);
    
    /// <summary>
    /// Get wallpapers by a specific tag
    /// </summary>
    Task<IEnumerable<Wallpaper>> GetWallpapersByTagAsync(string tag);
    
    /// <summary>
    /// Get a single wallpaper by ID
    /// </summary>
    Task<Wallpaper?> GetWallpaperByIdAsync(string id);
    
    /// <summary>
    /// Save a new wallpaper
    /// </summary>
    Task<bool> SaveWallpaperAsync(Wallpaper wallpaper);
    
    /// <summary>
    /// Delete a wallpaper
    /// </summary>
    Task<bool> DeleteWallpaperAsync(string id);
    
    /// <summary>
    /// Update wallpaper metadata
    /// </summary>
    Task<bool> UpdateWallpaperAsync(Wallpaper wallpaper);
    
    /// <summary>
    /// Get recently used wallpapers
    /// </summary>
    Task<IEnumerable<Wallpaper>> GetRecentWallpapersAsync(int count = 10);
    
    /// <summary>
    /// Get favorite wallpapers
    /// </summary>
    Task<IEnumerable<Wallpaper>> GetFavoriteWallpapersAsync();
    
    /// <summary>
    /// Toggle favorite status for a wallpaper
    /// </summary>
    Task<bool> ToggleFavoriteAsync(string id);
    
    /// <summary>
    /// Download a wallpaper from Unsplash
    /// </summary>
    Task<Wallpaper> DownloadFromUnsplashAsync(string query);
    
    /// <summary>
    /// Download a wallpaper from Pexels
    /// </summary>
    Task<Wallpaper> DownloadFromPexelsAsync(string query);
    
    /// <summary>
    /// Download a wallpaper from Wallpaper Engine
    /// </summary>
    Task<Wallpaper> DownloadFromWallpaperEngineAsync(string workshopUrl);
    
    /// <summary>
    /// Generate a wallpaper using AI
    /// </summary>
    Task<Wallpaper> GenerateAiWallpaperAsync(string prompt);
    
    /// <summary>
    /// Apply a wallpaper to the desktop
    /// </summary>
    Task<bool> ApplyWallpaperAsync(string id, string? monitorId = null);
    
    /// <summary>
    /// Apply a wallpaper to the lock screen
    /// </summary>
    Task<bool> ApplyToLockScreenAsync(string id);
    
    /// <summary>
    /// Import a local wallpaper file
    /// </summary>
    Task<Wallpaper> ImportLocalWallpaperAsync(string filePath);
} 