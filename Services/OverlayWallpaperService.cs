using AlexAssistant.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace AlexAssistant.Services
{
    public class OverlayWallpaperService
    {
        private List<WallpaperItem> _predefinedWallpapers;
        private readonly string _configFilePath;

        public OverlayWallpaperService()
        {
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Data", "wallpapers.json");
            LoadPredefinedWallpapers();
        }

        private void LoadPredefinedWallpapers()
        {
            _predefinedWallpapers = new List<WallpaperItem>();
            if (!File.Exists(_configFilePath))
            {
                Debug.WriteLine($"Warning: Config file not found: '{_configFilePath}'");
                return; 
            }
            try
            {
                string jsonContent = File.ReadAllText(_configFilePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
                };
                var loadedItems = JsonSerializer.Deserialize<List<WallpaperItem>>(jsonContent, options);

                if (loadedItems != null)
                {
                    _predefinedWallpapers = loadedItems.Where(item => item.IsResource).ToList();
                    Debug.WriteLine($"Loaded {_predefinedWallpapers.Count} predefined wallpapers from JSON.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading wallpapers from '{_configFilePath}': {ex.Message}");
                MessageBox.Show($"Error loading wallpaper list: {ex.Message}", "Config Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public List<WallpaperItem> GetPredefinedWallpapers()
        {
            return _predefinedWallpapers.ToList(); 
        }

        public WallpaperItem FindPredefinedById(string uniqueId)
        {
            if (string.IsNullOrWhiteSpace(uniqueId)) return null;
            return _predefinedWallpapers.FirstOrDefault(wp => wp.UniqueId.Equals(uniqueId, StringComparison.OrdinalIgnoreCase));
        }

        public WallpaperItem CreateCustomWallpaperItem(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Debug.WriteLine($"Cannot create custom wallpaper item: File does not exist or path is empty ('{filePath}')");
                return null; 
            }

            WallpaperFileType fileType = GetFileTypeFromExtension(filePath);
            if (fileType == WallpaperFileType.Unknown)
            {
                Debug.WriteLine($"Cannot create custom wallpaper item: Unknown file type for '{filePath}'");
                return null; 
            }

            return new WallpaperItem
            {
                Name = Path.GetFileNameWithoutExtension(filePath), 
                UniqueId = filePath, 
                FileType = fileType,
                Path = filePath, 
                IsResource = false 
            };
        }

        private WallpaperFileType GetFileTypeFromExtension(string filePath)
        {
            string extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            switch (extension)
            {
                case ".mp4":
                case ".wmv":
                case ".avi": 
                    return WallpaperFileType.Video;
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".gif": 
                    return WallpaperFileType.Image;
                default:
                    return WallpaperFileType.Unknown;
            }
        }

    }
}
