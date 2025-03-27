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
        
        public DateTime CreatedDate { get; set; }
        
        public DateTime ModifiedDate { get; set; }
        
        public List<string> WallpaperIds { get; set; } = new List<string>();
        
        public Collection()
        {
            Id = Guid.NewGuid().ToString();
            CreatedDate = DateTime.Now;
            ModifiedDate = DateTime.Now;
        }
    }
} 