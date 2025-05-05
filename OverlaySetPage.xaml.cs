using AlexAssistant.Models;
using AlexAssistant.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AlexAssistant
{
    public partial class OverlaySetPage : Page
    {
        private List<WallpaperItem> _overlayChoices = new List<WallpaperItem>();
        private const string NoneOptionName = "None";
        string folderWallpapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WallpaperAnimations");
        string userOverlaysPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserOverlays");
        private MainWindow _mainWindow;

        public OverlaySetPage(MainWindow mainWindow)
        {
            InitializeComponent();
            InitializeOverlayChoices();
            PopulateComboBox();
            _mainWindow = mainWindow;
        }

        private void InitializeOverlayChoices()
        {
            _overlayChoices.Clear();

            _overlayChoices.Add(new WallpaperItem
            {
                Name = NoneOptionName,
                Path = null,
                UniqueId = "overlay_none",
                FileType = WallpaperFileType.Unknown,
                IsResource = false
            });

            // Ensure user overlays folder exists
            if (!Directory.Exists(userOverlaysPath))
            {
                Directory.CreateDirectory(userOverlaysPath);
            }

            // Load standard overlays
            if (Directory.Exists(folderWallpapPath))
            {
                string[] files = Directory.GetFiles(folderWallpapPath);
                foreach (string file in files)
                {
                    _overlayChoices.Add(CreateWallpaperItem(file));
                }
            }
            else
            {
                Console.WriteLine("Can't find: " + folderWallpapPath);
            }

            // Load user overlays
            if (Directory.Exists(userOverlaysPath))
            {
                string[] userFiles = Directory.GetFiles(userOverlaysPath);
                foreach (string file in userFiles)
                {
                    var item = CreateWallpaperItem(file);
                    if (item != null)
                    {
                        item.Name = "Custom: " + item.Name; // Prefix to identify user-added overlays
                        _overlayChoices.Add(item);
                    }
                }
            }
        }

        private WallpaperItem CreateWallpaperItem(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Console.WriteLine($"Warning: Overlay file not found or path is null/empty: {filePath ?? "NULL"}");
                return null;
            }


            var item = new WallpaperItem
            {
                Path = filePath,
                Name = Path.GetFileNameWithoutExtension(filePath),
                UniqueId = Guid.NewGuid().ToString(),
                IsResource = false
            };

            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            switch (extension)
            {
                case ".mp4":
                    item.FileType = WallpaperFileType.Video;
                    break;
                case ".wmv":
                case ".avi":
                case ".mov":
                    item.FileType = WallpaperFileType.Video;
                    break;
                case ".png":
                    item.FileType = WallpaperFileType.Image;
                    break;
                case ".jpg":
                    item.FileType = WallpaperFileType.Image;
                    break;
                case ".jpeg":
                    item.FileType = WallpaperFileType.Image;
                    break;
                case ".bmp":
                case ".gif":
                    item.FileType = WallpaperFileType.Image;
                    break;
                default:
                    item.FileType = WallpaperFileType.Unknown;
                    break;
            }
            return item;
        }

        private void PopulateComboBox()
        {
            OverlayComboBox.ItemsSource = _overlayChoices.Where(item => item != null).ToList();
            OverlayComboBox.DisplayMemberPath = "Name";
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSavedOverlay();

            // Load the saved state of SaveConfidence toggle
            SaveConfidenceToggle.IsChecked = Properties.Settings.Default.SaveConfidence;
        }

        private void OverlayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OverlayComboBox.SelectedItem is WallpaperItem selectedItem)
            {
                if (selectedItem.Path == null && selectedItem.Name == NoneOptionName)
                {
                    Properties.Settings.Default.OverlayPath = null;
                    Properties.Settings.Default.Save();

                    UpdatePreview(null);

                    Console.WriteLine("Selected: None. Overlay setting cleared.");
                }
                else
                {
                    Properties.Settings.Default.OverlayPath = selectedItem.Path;
                    Properties.Settings.Default.Save();

                    UpdatePreview(selectedItem.Path);

                    Console.WriteLine($"Selected and saved: {selectedItem.Path}");
                }
            }
            else if (OverlayComboBox.SelectedIndex == -1)
            {
                Console.WriteLine("ComboBox selection cleared.");
            }
        }

        private void TrainButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new ModelTrainingPage(_mainWindow));
        }

        private void AddCustomOverlayButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select Image or Video File",
                Filter = "Media Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp4;*.wmv;*.avi;*.mov",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string sourceFilePath = openFileDialog.FileName;
                    string fileName = Path.GetFileName(sourceFilePath);
                    string destinationPath = Path.Combine(userOverlaysPath, fileName);

                    // Check if file with the same name already exists
                    if (File.Exists(destinationPath))
                    {
                        // Add timestamp to make filename unique
                        string newFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(fileName)}";
                        destinationPath = Path.Combine(userOverlaysPath, newFileName);
                    }

                    // Copy file to user overlays folder
                    File.Copy(sourceFilePath, destinationPath);

                    // Create and add the new item
                    var newItem = CreateWallpaperItem(destinationPath);
                    if (newItem != null)
                    {
                        newItem.Name = "Custom: " + newItem.Name;
                        _overlayChoices.Add(newItem);

                        // Refresh the combobox
                        OverlayComboBox.ItemsSource = null;
                        OverlayComboBox.ItemsSource = _overlayChoices.Where(item => item != null).ToList();

                        // Select the new item
                        OverlayComboBox.SelectedItem = newItem;

                        MessageBox.Show("Custom overlay added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error adding custom overlay: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveConfidenceToggle_CheckedChanged(object sender, RoutedEventArgs e)
        {
            //if (sender is ToggleButton toggleButton)
            //{
            //    Properties.Settings.Default.SaveConfidence = toggleButton.IsChecked ?? false;
            //    Properties.Settings.Default.Save();

            //    string state = toggleButton.IsChecked ?? false ? "enabled" : "disabled";
            //    Console.WriteLine($"Save Confidence {state}");
            //}
            if (sender is ToggleButton toggleButton)
            {
                bool isEnabled = toggleButton.IsChecked ?? false;
                Properties.Settings.Default.SaveConfidence = isEnabled;
                Properties.Settings.Default.Save();
                if (isEnabled)
                {
                    FaceRecogService.Start();
                }
                else
                {
                    FaceRecogService.Stop();
                }

                string state = isEnabled ? "enabled" : "disabled";
                Console.WriteLine($"Save Confidence {state}");
            }
        }

        private void LoadSavedOverlay()
        {
            string savedPath = Properties.Settings.Default.OverlayPath;
            WallpaperItem itemToSelect = null;

            var currentItems = OverlayComboBox.ItemsSource as List<WallpaperItem> ?? _overlayChoices;

            if (!string.IsNullOrEmpty(savedPath))
            {
                itemToSelect = currentItems.FirstOrDefault(item => item.Path != null && item.Path.Equals(savedPath, StringComparison.OrdinalIgnoreCase));

                if (itemToSelect == null)
                {
                    Console.WriteLine($"Saved overlay path '{savedPath}' not found in current options. Defaulting to 'None'.");
                    itemToSelect = currentItems.FirstOrDefault(item => item.Name == NoneOptionName);
                    Properties.Settings.Default.OverlayPath = null;
                    Properties.Settings.Default.Save();
                }
                else
                {
                    Console.WriteLine($"Loaded saved overlay: {savedPath}");
                }
            }
            else
            {
                Console.WriteLine("No overlay path saved or explicitly set to None. Selecting 'None'.");
                itemToSelect = currentItems.FirstOrDefault(item => item.Name == NoneOptionName);
            }

            if (itemToSelect != null)
            {
                OverlayComboBox.SelectedItem = itemToSelect;
            }
            else
            {
                OverlayComboBox.SelectedIndex = -1;
                UpdatePreview(null);
                Console.WriteLine("Warning: Could not find item to select, including 'None'.");
            }

            if (OverlayComboBox.SelectedItem is WallpaperItem currentlySelectedItem)
            {
                UpdatePreview(currentlySelectedItem.Path);
            }
            else
            {
                UpdatePreview(null);
            }
        }
     
        private void UpdatePreview(string filePath)
        {
            OverlayPreview.Stop();
            OverlayPreview.Source = null;

            if (!string.IsNullOrEmpty(filePath))
            {
                if (File.Exists(filePath))
                {
                    try
                    {
                        Uri fileUri = new Uri(filePath, UriKind.Absolute);
                        OverlayPreview.Source = fileUri;
                        Console.WriteLine($"Preview updated with: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading preview for {filePath}:\n{ex.Message}", "Preview Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        OverlayPreview.Source = null;
                    }
                }
                else
                {
                    Console.WriteLine($"Preview cannot be shown. File not found: {filePath}");
                }
            }
            else
            {
                Console.WriteLine("Preview cleared (No overlay selected).");
            }
        }
    }
}