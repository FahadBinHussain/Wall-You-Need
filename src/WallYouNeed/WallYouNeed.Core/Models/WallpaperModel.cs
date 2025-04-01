using System;

namespace WallYouNeed.Core.Models
{
    public class WallpaperModel
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string ResolutionCategory { get; set; } // 8K, 4K, etc.
        public string ThumbnailUrl { get; set; }
        public string ImageUrl { get; set; }
        public string SourceUrl { get; set; }
        public string Source { get; set; } // Website source (Backiee, Unsplash, etc.)
        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime UploadDate { get; set; }
        public bool IsDownloaded { get; set; }
        public string LocalPath { get; set; }
        public int Rating { get; set; } // User rating
        public long FileSizeBytes { get; set; } // Size of the wallpaper file in bytes
        public double FileSizeMB { get; set; } // Size of the wallpaper file in megabytes

        public WallpaperModel()
        {
            Id = Guid.NewGuid().ToString();
            UploadDate = DateTime.Now;
        }

        public string GetResolution()
        {
            return $"{Width}x{Height}";
        }
    }
} 