using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using Microsoft.Extensions.Logging;
using WallYouNeed.Core.Models;

namespace WallYouNeed.Core.Repositories
{
    public class CollectionRepository
    {
        private readonly ILogger<CollectionRepository> _logger;
        private readonly LiteDatabase _database;
        private readonly ILiteCollection<Collection> _collections;
        private readonly string _collectionName = "collections";

        public CollectionRepository(
            ILogger<CollectionRepository> logger,
            LiteDatabase database)
        {
            _logger = logger;
            _database = database;
            _collections = _database.GetCollection<Collection>(_collectionName);
            
            // Create indices for faster lookups
            _collections.EnsureIndex(x => x.Id);
            _collections.EnsureIndex(x => x.Name);
            _collections.EnsureIndex(x => x.CreatedAt);
        }

        public IEnumerable<Collection> GetAll()
        {
            try
            {
                return _collections.FindAll().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all collections");
                return new List<Collection>();
            }
        }

        public Collection GetById(string id)
        {
            try
            {
                return _collections.FindById(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting collection by ID: {Id}", id);
                return null;
            }
        }

        public Collection GetByName(string name)
        {
            try
            {
                return _collections.FindOne(x => x.Name == name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting collection by name: {Name}", name);
                return null;
            }
        }

        public void Insert(Collection collection)
        {
            try
            {
                if (string.IsNullOrEmpty(collection.Id))
                {
                    collection.Id = Guid.NewGuid().ToString();
                }

                // Ensure dates are set
                if (collection.CreatedAt == default)
                {
                    collection.CreatedAt = DateTime.Now;
                }
                
                if (collection.UpdatedAt == default)
                {
                    collection.UpdatedAt = DateTime.Now;
                }

                // Initialize collections if null
                if (collection.WallpaperIds == null)
                {
                    collection.WallpaperIds = new List<string>();
                }

                _collections.Insert(collection);
                _logger.LogInformation("Collection inserted: {Id}", collection.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting collection: {Id}", collection.Id);
                throw;
            }
        }

        public bool Update(Collection collection)
        {
            try
            {
                // Ensure UpdatedAt is set
                collection.UpdatedAt = DateTime.Now;
                
                var result = _collections.Update(collection);
                
                if (result)
                {
                    _logger.LogInformation("Collection updated: {Id}", collection.Id);
                }
                else
                {
                    _logger.LogWarning("Collection update failed (not found?): {Id}", collection.Id);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating collection: {Id}", collection.Id);
                throw;
            }
        }

        public bool Delete(string id)
        {
            try
            {
                var result = _collections.Delete(id);
                
                if (result)
                {
                    _logger.LogInformation("Collection deleted: {Id}", id);
                }
                else
                {
                    _logger.LogWarning("Collection delete failed (not found?): {Id}", id);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting collection: {Id}", id);
                throw;
            }
        }

        public IEnumerable<Collection> Search(string searchTerm)
        {
            try
            {
                return _collections.Find(x => 
                    x.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) || 
                    x.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching collections: {SearchTerm}", searchTerm);
                return new List<Collection>();
            }
        }

        public IEnumerable<Collection> GetRecentCollections(int count = 10)
        {
            try
            {
                return _collections.Find(Query.All("UpdatedAt", Query.Descending)).Take(count).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent collections");
                return new List<Collection>();
            }
        }
    }
} 