using System;
using System.Collections.Generic;

namespace WallYouNeed.Core.Models
{
    public class Collection
    {
        public string Id { get; set; }
        
        public string Name { get; set; }
        
        public string Description { get; set; }
        
        public string CoverImagePath { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime UpdatedAt { get; set; }
        
        public List<string> WallpaperIds { get; set; } = new List<string>();
    }
} 