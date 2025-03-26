using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Navigation;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Appearance;
using WallYouNeed.Core.Models;
using WallYouNeed.Core.Services.Interfaces;

namespace WallYouNeed.App.Pages
{
    public partial class SettingsPage : Page, INavigableView<SettingsPage>
    {
        private readonly ILogger<SettingsPage> _logger;
        private readonly ISnackbarService _snackbarService;
        private readonly ISettingsService _settingsService;
        private readonly IThemeService _themeService;
        private AppSettings _settings = new();
        private bool _isInitializing = true;

        public SettingsPage ViewModel => this;

        public SettingsPage(
            ILogger<SettingsPage> logger,
            ISnackbarService snackbarService,
            ISettingsService settingsService,
            IThemeService themeService)
        {
            _logger = logger;
            _snackbarService = snackbarService;
            _settingsService = settingsService;
            _themeService = themeService;

            InitializeComponent();
            
            Loaded += SettingsPage_Loaded;
        }

        private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _settings = await _settingsService.LoadSettingsAsync();
                UpdateUIFromSettings();
                await UpdateStorageSizeInfoAsync();
                
                _isInitializing = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings");
                _snackbarService.Show("Error", "Failed to load settings", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private void UpdateUIFromSettings()
        {
            DarkModeToggle.IsChecked = _settings.Theme == AppTheme.Dark;
            AutoChangeWallpaperToggle.IsChecked = _settings.AutoChangeWallpaper;
            RotationIntervalComboBox.SelectedIndex = _settings.RotationIntervalIndex;
            UnsplashSourceCheckBox.IsChecked = _settings.UseUnsplash;
            PexelsSourceCheckBox.IsChecked = _settings.UsePexels;
            LocalSourceCheckBox.IsChecked = _settings.UseLocalWallpapers;
            UnsplashApiKeyBox.Password = _settings.UnsplashApiKey;
            PexelsApiKeyBox.Password = _settings.PexelsApiKey;
            RunAtStartupToggle.IsChecked = _settings.RunAtStartup;
            MinimizeToTrayToggle.IsChecked = _settings.MinimizeToTray;
            StorageLocationText.Text = _settings.StorageLocation;
        }

        private async Task UpdateStorageSizeInfoAsync()
        {
            try
            {
                var directory = new DirectoryInfo(_settings.StorageLocation);
                var size = await Task.Run(() => directory.GetFiles("*.*", SearchOption.AllDirectories)
                    .Sum(fi => fi.Length));
                StorageSizeText.Text = $"Storage Size: {FormatFileSize(size)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate storage size");
                StorageSizeText.Text = "Storage Size: Unknown";
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        private async void DarkModeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _settings.Theme = DarkModeToggle.IsChecked ?? false ? AppTheme.Dark : AppTheme.Light;
                await _settingsService.SaveSettingsAsync(_settings);
                _themeService.SetTheme(_settings.Theme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
                _snackbarService.Show("Success", "Theme updated successfully", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update theme");
                _snackbarService.Show("Error", "Failed to update theme", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void AutoChangeWallpaperToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _settings.AutoChangeWallpaper = AutoChangeWallpaperToggle.IsChecked ?? false;
                await _settingsService.SaveSettingsAsync(_settings);
                _snackbarService.Show("Success", "Auto-change wallpaper setting updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update auto-change wallpaper setting");
                _snackbarService.Show("Error", "Failed to update auto-change wallpaper setting", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void RotationIntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _settings.RotationIntervalIndex = RotationIntervalComboBox.SelectedIndex;
                
                // Convert index to minutes
                int[] intervalMinutes = { 15, 30, 60, 180, 360, 720, 1440 };
                _settings.RotationIntervalMinutes = intervalMinutes[_settings.RotationIntervalIndex];
                
                await _settingsService.SaveSettingsAsync(_settings);
                _snackbarService.Show("Success", "Rotation interval updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update rotation interval");
                _snackbarService.Show("Error", "Failed to update rotation interval", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void SourceCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _settings.UseLocalWallpapers = LocalSourceCheckBox.IsChecked ?? true;
                _settings.UseUnsplash = UnsplashSourceCheckBox.IsChecked ?? false;
                _settings.UsePexels = PexelsSourceCheckBox.IsChecked ?? false;

                // Ensure at least one source is enabled
                if (!_settings.UseLocalWallpapers && !_settings.UseUnsplash && !_settings.UsePexels)
                {
                    _settings.UseLocalWallpapers = true;
                    LocalSourceCheckBox.IsChecked = true;
                    _snackbarService.Show("Warning", "At least one wallpaper source must be selected", ControlAppearance.Caution, null, TimeSpan.FromSeconds(2));
                }

                await _settingsService.SaveSettingsAsync(_settings);
                _snackbarService.Show("Success", "Wallpaper source settings updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update wallpaper source settings");
                _snackbarService.Show("Error", "Failed to update wallpaper source settings", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                if (sender is Wpf.Ui.Controls.PasswordBox passwordBox)
                {
                    switch (passwordBox.Name)
                    {
                        case "UnsplashApiKeyBox":
                            _settings.UnsplashApiKey = passwordBox.Password;
                            break;
                        case "PexelsApiKeyBox":
                            _settings.PexelsApiKey = passwordBox.Password;
                            break;
                    }
                    await _settingsService.SaveSettingsAsync(_settings);
                    _snackbarService.Show("Success", "API key updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update API key");
                _snackbarService.Show("Error", "Failed to update API key", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void RunAtStartupToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _settings.RunAtStartup = RunAtStartupToggle.IsChecked ?? false;
                await _settingsService.SaveSettingsAsync(_settings);
                _snackbarService.Show("Success", "Startup setting updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update startup setting");
                _snackbarService.Show("Error", "Failed to update startup setting", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void MinimizeToTrayToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _settings.MinimizeToTray = MinimizeToTrayToggle.IsChecked ?? true;
                await _settingsService.SaveSettingsAsync(_settings);
                _snackbarService.Show("Success", "Tray setting updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update tray setting");
                _snackbarService.Show("Error", "Failed to update tray setting", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private void OpenStorageLocationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", _settings.StorageLocation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open storage location");
                _snackbarService.Show("Error", "Failed to open storage location", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void ChangeStorageLocationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Use Windows Forms folder browser dialog
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dialog.Description = "Select new storage location";
                    dialog.UseDescriptionForTitle = true;
                    dialog.SelectedPath = _settings.StorageLocation;

                    var result = dialog.ShowDialog();
                    if (result == System.Windows.Forms.DialogResult.OK)
                    {
                        _settings.StorageLocation = dialog.SelectedPath;
                        await _settingsService.SaveSettingsAsync(_settings);
                        StorageLocationText.Text = _settings.StorageLocation;
                        await UpdateStorageSizeInfoAsync();
                        _snackbarService.Show("Success", "Storage location updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update storage location");
                _snackbarService.Show("Error", "Failed to update storage location", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "Are you sure you want to clear all data? This action cannot be undone.",
                    "Clear Data",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    // Reset settings to defaults
                    _settings = new AppSettings();
                    await _settingsService.SaveSettingsAsync(_settings);
                    
                    // Update UI
                    UpdateUIFromSettings();
                    await UpdateStorageSizeInfoAsync();
                    
                    _snackbarService.Show("Success", "All data cleared successfully", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear data");
                _snackbarService.Show("Error", "Failed to clear data", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open URL");
                _snackbarService.Show("Error", "Failed to open URL", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }
    }
} 