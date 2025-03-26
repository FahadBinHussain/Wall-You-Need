using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using Microsoft.Extensions.Logging;
using WallYouNeed.Core.Models;

namespace WallYouNeed.Core.Repositories
{
    public class WallpaperRepository
    {
        private readonly ILogger<WallpaperRepository> _logger;
        private readonly LiteDatabase _database;
        private readonly ILiteCollection<Wallpaper> _wallpapers;
        private readonly string _collectionName = "wallpapers";

        public WallpaperRepository(
            ILogger<WallpaperRepository> logger,
            LiteDatabase database)
        {
            _logger = logger;
            _database = database;
            _wallpapers = _database.GetCollection<Wallpaper>(_collectionName);
            
            // Create indices for faster lookups
            _wallpapers.EnsureIndex(x => x.Id);
            _wallpapers.EnsureIndex(x => x.Name);
            _wallpapers.EnsureIndex(x => x.Source);
            _wallpapers.EnsureIndex(x => x.Tags);
            _wallpapers.EnsureIndex(x => x.CreatedAt);
            _wallpapers.EnsureIndex(x => x.IsFavorite);
        }

        public IEnumerable<Wallpaper> GetAllWallpapers()
        {
            try
            {
                return _wallpapers.FindAll().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all wallpapers");
                return new List<Wallpaper>();
            }
        }

        public Wallpaper GetWallpaperById(string id)
        {
            try
            {
                return _wallpapers.FindById(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wallpaper by ID: {Id}", id);
                return null;
            }
        }

        public IEnumerable<Wallpaper> GetWallpapersBySource(WallpaperSource source)
        {
            try
            {
                return _wallpapers.Find(x => x.Source == source).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wallpapers by source: {Source}", source);
                return new List<Wallpaper>();
            }
        }

        public IEnumerable<Wallpaper> GetWallpapersByTag(string tag)
        {
            try
            {
                return _wallpapers.Find(x => x.Tags.Contains(tag)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wallpapers by tag: {Tag}", tag);
                return new List<Wallpaper>();
            }
        }

        public void SaveWallpaper(Wallpaper wallpaper)
        {
            try
            {
                if (string.IsNullOrEmpty(wallpaper.Id))
                {
                    wallpaper.Id = Guid.NewGuid().ToString();
                }

                // Ensure dates are set
                if (wallpaper.CreatedAt == default)
                {
                    wallpaper.CreatedAt = DateTime.Now;
                }
                
                if (wallpaper.LastUsedAt == default)
                {
                    wallpaper.LastUsedAt = DateTime.Now;
                }

                // Initialize collections if null
                if (wallpaper.Tags == null)
                {
                    wallpaper.Tags = new List<string>();
                }
                
                if (wallpaper.Metadata == null)
                {
                    wallpaper.Metadata = new Dictionary<string, string>();
                }

                _wallpapers.Insert(wallpaper);
                _logger.LogInformation("Wallpaper inserted: {Id}", wallpaper.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting wallpaper: {Id}", wallpaper.Id);
                throw;
            }
        }

        public bool Update(Wallpaper wallpaper)
        {
            return UpdateWallpaper(wallpaper);
        }
        
        public bool UpdateWallpaper(Wallpaper wallpaper)
        {
            try
            {
                var result = _wallpapers.Update(wallpaper);
                
                if (result)
                {
                    _logger.LogInformation("Wallpaper updated: {Id}", wallpaper.Id);
                }
                else
                {
                    _logger.LogWarning("Wallpaper update failed (not found?): {Id}", wallpaper.Id);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating wallpaper: {Id}", wallpaper.Id);
                throw;
            }
        }

        public bool DeleteWallpaper(string id)
        {
            try
            {
                var result = _wallpapers.Delete(id);
                
                if (result)
                {
                    _logger.LogInformation("Wallpaper deleted: {Id}", id);
                }
                else
                {
                    _logger.LogWarning("Wallpaper delete failed (not found?): {Id}", id);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting wallpaper: {Id}", id);
                throw;
            }
        }

        public IEnumerable<Wallpaper> Search(string searchTerm)
        {
            try
            {
                // Search in name, description, and tags
                return _wallpapers.Find(x => 
                    x.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) || 
                    x.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    x.Tags.Any(t => t.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching wallpapers: {SearchTerm}", searchTerm);
                return new List<Wallpaper>();
            }
        }

        public IEnumerable<Wallpaper> GetFavoriteWallpapers()
        {
            try
            {
                return _wallpapers.Find(x => x.IsFavorite).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting favorite wallpapers");
                return new List<Wallpaper>();
            }
        }

        public IEnumerable<Wallpaper> GetRecentWallpapers(int count = 10)
        {
            try
            {
                return _wallpapers.Find(Query.All("LastUsedAt", Query.Descending)).Take(count).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent wallpapers");
                return new List<Wallpaper>();
            }
        }

        public bool ToggleFavorite(string id)
        {
            try
            {
                var wallpaper = GetWallpaperById(id);
                if (wallpaper == null)
                {
                    _logger.LogWarning("Wallpaper not found for toggling favorite: {Id}", id);
                    return false;
                }
                
                wallpaper.IsFavorite = !wallpaper.IsFavorite;
                return Update(wallpaper);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling favorite for wallpaper: {Id}", id);
                throw;
            }
        }

        public void UpdateLastUsed(string id)
        {
            try
            {
                var wallpaper = GetWallpaperById(id);
                if (wallpaper == null)
                {
                    _logger.LogWarning("Wallpaper not found for updating last used: {Id}", id);
                    return;
                }
                
                wallpaper.LastUsedAt = DateTime.Now;
                Update(wallpaper);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last used for wallpaper: {Id}", id);
                // Don't throw for this non-critical operation
            }
        }
    }
} 