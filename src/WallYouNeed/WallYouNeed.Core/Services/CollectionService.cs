using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WallYouNeed.Core.Models;
using WallYouNeed.Core.Repositories;
using WallYouNeed.Core.Services.Interfaces;

namespace WallYouNeed.Core.Services;

/// <summary>
/// Implementation of the collection service
/// </summary>
public class CollectionService : ICollectionService
{
    private readonly ILogger<CollectionService> _logger;
    private readonly CollectionRepository _repository;
    private readonly WallpaperRepository _wallpaperRepository;
    private readonly IWallpaperService _wallpaperService;

    public CollectionService(
        ILogger<CollectionService> logger,
        CollectionRepository repository,
        WallpaperRepository wallpaperRepository,
        IWallpaperService wallpaperService)
    {
        _logger = logger;
        _repository = repository;
        _wallpaperRepository = wallpaperRepository;
        _wallpaperService = wallpaperService;
    }

    /// <inheritdoc />
    public async Task<List<Collection>> GetAllCollectionsAsync()
    {
        try
        {
            _logger.LogInformation("Getting all collections");
            return await Task.FromResult(_repository.GetAll().ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all collections");
            return new List<Collection>();
        }
    }

    /// <inheritdoc />
    public async Task<Collection> GetCollectionByIdAsync(string id)
    {
        try
        {
            _logger.LogInformation("Getting collection by ID: {Id}", id);
            var collection = await Task.FromResult(_repository.GetById(id));
            
            if (collection == null)
            {
                throw new KeyNotFoundException($"Collection not found with ID: {id}");
            }
            
            return collection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting collection by ID: {Id}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Collection> CreateCollectionAsync(string name, string description = "")
    {
        try
        {
            _logger.LogInformation("Creating collection: {Name}", name);
            
            var collection = new Collection
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Description = description,
                CreatedAt = DateTime.Now,
                WallpaperIds = new List<string>()
            };
            
            _repository.Insert(collection);
            
            _logger.LogInformation("Collection created with ID: {Id}", collection.Id);
            
            return await Task.FromResult(collection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating collection: {Name}", name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateCollectionAsync(Collection collection)
    {
        try
        {
            _logger.LogInformation("Updating collection: {Id}", collection.Id);
            
            var existing = _repository.GetById(collection.Id);
            if (existing == null)
            {
                throw new KeyNotFoundException($"Collection not found with ID: {collection.Id}");
            }
            
            collection.UpdatedAt = DateTime.Now;
            _repository.Update(collection);
            
            _logger.LogInformation("Collection updated: {Id}", collection.Id);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating collection: {Id}", collection.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteCollectionAsync(string id)
    {
        try
        {
            _logger.LogInformation("Deleting collection: {Id}", id);
            
            var collection = _repository.GetById(id);
            if (collection == null)
            {
                throw new KeyNotFoundException($"Collection not found with ID: {id}");
            }
            
            _repository.Delete(id);
            
            _logger.LogInformation("Collection deleted: {Id}", id);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting collection: {Id}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AddWallpaperToCollectionAsync(string collectionId, string wallpaperId)
    {
        try
        {
            _logger.LogInformation("Adding wallpaper {WallpaperId} to collection {CollectionId}", wallpaperId, collectionId);
            
            var collection = _repository.GetById(collectionId);
            if (collection == null)
            {
                throw new KeyNotFoundException($"Collection not found with ID: {collectionId}");
            }
            
            var wallpaper = await _wallpaperService.GetWallpaperByIdAsync(wallpaperId);
            if (wallpaper == null)
            {
                throw new KeyNotFoundException($"Wallpaper not found with ID: {wallpaperId}");
            }
            
            if (collection.WallpaperIds.Contains(wallpaperId))
            {
                _logger.LogInformation("Wallpaper already in collection");
                return;
            }
            
            collection.WallpaperIds.Add(wallpaperId);
            collection.UpdatedAt = DateTime.Now;
            
            _repository.Update(collection);
            
            _logger.LogInformation("Wallpaper added to collection");
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding wallpaper {WallpaperId} to collection {CollectionId}", wallpaperId, collectionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RemoveWallpaperFromCollectionAsync(string collectionId, string wallpaperId)
    {
        try
        {
            _logger.LogInformation("Removing wallpaper {WallpaperId} from collection {CollectionId}", wallpaperId, collectionId);
            
            var collection = _repository.GetById(collectionId);
            if (collection == null)
            {
                throw new KeyNotFoundException($"Collection not found with ID: {collectionId}");
            }
            
            if (!collection.WallpaperIds.Contains(wallpaperId))
            {
                _logger.LogInformation("Wallpaper not in collection");
                return;
            }
            
            collection.WallpaperIds.Remove(wallpaperId);
            collection.UpdatedAt = DateTime.Now;
            
            _repository.Update(collection);
            
            _logger.LogInformation("Wallpaper removed from collection");
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing wallpaper {WallpaperId} from collection {CollectionId}", wallpaperId, collectionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Wallpaper>> GetWallpapersInCollectionAsync(string collectionId)
    {
        try
        {
            _logger.LogInformation("Getting wallpapers in collection: {Id}", collectionId);
            
            var collection = _repository.GetById(collectionId);
            if (collection == null)
            {
                throw new KeyNotFoundException($"Collection not found with ID: {collectionId}");
            }
            
            var wallpapers = new List<Wallpaper>();
            
            foreach (var wallpaperId in collection.WallpaperIds)
            {
                var wallpaper = await _wallpaperService.GetWallpaperByIdAsync(wallpaperId);
                if (wallpaper != null)
                {
                    wallpapers.Add(wallpaper);
                }
            }
            
            _logger.LogInformation("Found {Count} wallpapers in collection", wallpapers.Count);
            
            return wallpapers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting wallpapers in collection: {Id}", collectionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Wallpaper?> GetWallpaperFromCollectionAsync(string collectionId, string wallpaperId)
    {
        try
        {
            _logger.LogInformation("Getting wallpaper {WallpaperId} from collection {CollectionId}", wallpaperId, collectionId);
            
            var collection = _repository.GetById(collectionId);
            if (collection == null)
            {
                throw new KeyNotFoundException($"Collection not found with ID: {collectionId}");
            }
            
            if (!collection.WallpaperIds.Contains(wallpaperId))
            {
                _logger.LogWarning("Wallpaper {WallpaperId} not found in collection {CollectionId}", wallpaperId, collectionId);
                return null;
            }
            
            return await _wallpaperService.GetWallpaperByIdAsync(wallpaperId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting wallpaper {WallpaperId} from collection {CollectionId}", wallpaperId, collectionId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetCollectionCoverAsync(string collectionId, string wallpaperId)
    {
        try
        {
            _logger.LogInformation("Setting cover wallpaper {WallpaperId} for collection {CollectionId}", wallpaperId, collectionId);
            
            var collection = _repository.GetById(collectionId);
            if (collection == null)
            {
                throw new KeyNotFoundException($"Collection not found with ID: {collectionId}");
            }
            
            var wallpaper = await _wallpaperService.GetWallpaperByIdAsync(wallpaperId);
            if (wallpaper == null)
            {
                throw new KeyNotFoundException($"Wallpaper not found with ID: {wallpaperId}");
            }
            
            if (!collection.WallpaperIds.Contains(wallpaperId))
            {
                throw new InvalidOperationException("Cannot set cover to a wallpaper that is not in the collection");
            }
            
            collection.CoverImagePath = wallpaper.FilePath;
            collection.UpdatedAt = DateTime.Now;
            
            _repository.Update(collection);
            
            _logger.LogInformation("Cover set for collection");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cover for collection {CollectionId}", collectionId);
            return false;
        }
    }
} 