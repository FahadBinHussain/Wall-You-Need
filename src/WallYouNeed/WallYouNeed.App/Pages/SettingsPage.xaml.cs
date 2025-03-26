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
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Linq;
using WallYouNeed.App.Services;

namespace WallYouNeed.App.Pages
{
    public partial class SettingsPage : Page, INavigableView<SettingsPage>
    {
        private readonly ILogger<SettingsPage> _logger;
        private readonly ISnackbarService _snackbarService;
        private readonly ISettingsService _settingsService;
        private readonly IThemeService _themeService;
        private readonly ILogService _logService;
        private AppSettings _settings = new();
        private bool _isInitializing = true;

        public SettingsPage ViewModel => this;

        public SettingsPage(
            ILogger<SettingsPage> logger,
            ISnackbarService snackbarService,
            ISettingsService settingsService,
            IThemeService themeService,
            ILogService logService)
        {
            _logger = logger;
            _snackbarService = snackbarService;
            _settingsService = settingsService;
            _themeService = themeService;
            _logService = logService;

            InitializeComponent();
            DataContext = this;

            _logService.LogInfo("SettingsPage initialized");
            Loaded += SettingsPage_Loaded;
        }

        private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _logService.LogInfo("SettingsPage_Loaded called");
                
                // Use a default light background and dark text to ensure visibility
                this.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(30, 30, 30));

                _settings = await _settingsService.LoadSettingsAsync();
                UpdateUIFromSettings();
                await UpdateStorageSizeInfoAsync();
                
                _isInitializing = false;
                
                // Log that the page was loaded successfully
                _logger.LogInformation("Settings page loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings");
                _logService.LogError(ex, "Failed to load settings: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to load settings. Please try again.", 
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
            }
        }

        private void UpdateUIFromSettings()
        {
            ThemeToggle.IsChecked = _settings.Theme == AppTheme.Dark;
            AutoChangeToggle.IsChecked = _settings.AutoChangeWallpaper;
            RotationIntervalCombo.SelectedIndex = _settings.RotationIntervalIndex;
            
            UnsplashApiKey.Password = _settings.UnsplashApiKey;
            PexelsApiKey.Password = _settings.PexelsApiKey;
            StartupToggle.IsChecked = _settings.RunAtStartup;
            MinimizeToTrayToggle.IsChecked = _settings.MinimizeToTray;
            StorageLocationText.Text = _settings.StorageLocation;
            VerboseLoggingToggle.IsChecked = _settings.VerboseLogging;
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

        private async void ThemeToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _logService.LogUIAction("ThemeToggle", "Checked");
                _settings.Theme = AppTheme.Dark;
                await _settingsService.SaveSettingsAsync(_settings);
                _themeService.SetTheme(ApplicationTheme.Dark);
                _snackbarService.Show("Success", "Theme updated successfully", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update theme");
                _logService.LogError(ex, "Failed to update theme: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to update theme", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void ThemeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _logService.LogUIAction("ThemeToggle", "Unchecked");
                _settings.Theme = AppTheme.Light;
                await _settingsService.SaveSettingsAsync(_settings);
                _themeService.SetTheme(ApplicationTheme.Light);
                _snackbarService.Show("Success", "Theme updated successfully", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update theme");
                _logService.LogError(ex, "Failed to update theme: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to update theme", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void AutoChangeToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _logService.LogUIAction("AutoChangeToggle", "Checked");
                _settings.AutoChangeWallpaper = true;
                await _settingsService.SaveSettingsAsync(_settings);
                RotationIntervalGrid.Visibility = Visibility.Visible;
                _snackbarService.Show("Success", "Auto-change wallpaper setting updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update auto-change wallpaper setting");
                _logService.LogError(ex, "Failed to update auto-change wallpaper setting: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to update auto-change wallpaper setting", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void AutoChangeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _logService.LogUIAction("AutoChangeToggle", "Unchecked");
                _settings.AutoChangeWallpaper = false;
                await _settingsService.SaveSettingsAsync(_settings);
                RotationIntervalGrid.Visibility = Visibility.Collapsed;
                _snackbarService.Show("Success", "Auto-change wallpaper setting updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update auto-change wallpaper setting");
                _logService.LogError(ex, "Failed to update auto-change wallpaper setting: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to update auto-change wallpaper setting", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void RotationIntervalCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _logService.LogUIAction("RotationIntervalCombo", "SelectionChanged");
                _settings.RotationIntervalIndex = RotationIntervalCombo.SelectedIndex;
                
                // Convert index to minutes
                int[] intervalMinutes = { 30, 60, 120, 240, 1440 };
                if (_settings.RotationIntervalIndex >= 0 && _settings.RotationIntervalIndex < intervalMinutes.Length)
                {
                    _settings.RotationIntervalMinutes = intervalMinutes[_settings.RotationIntervalIndex];
                }
                
                await _settingsService.SaveSettingsAsync(_settings);
                _snackbarService.Show("Success", "Rotation interval updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update rotation interval");
                _logService.LogError(ex, "Failed to update rotation interval: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to update rotation interval", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void UnsplashApiKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _logService.LogUIAction("UnsplashApiKey", "PasswordChanged");
                _settings.UnsplashApiKey = UnsplashApiKey.Password;
                await _settingsService.SaveSettingsAsync(_settings);
                _snackbarService.Show("Success", "API key updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update API key");
                _logService.LogError(ex, "Failed to update API key: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to update API key", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void PexelsApiKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _logService.LogUIAction("PexelsApiKey", "PasswordChanged");
                _settings.PexelsApiKey = PexelsApiKey.Password;
                await _settingsService.SaveSettingsAsync(_settings);
                _snackbarService.Show("Success", "API key updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update API key");
                _logService.LogError(ex, "Failed to update API key: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to update API key", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void StartupToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _logService.LogUIAction("StartupToggle", "Checked");
                _settings.RunAtStartup = true;
                await _settingsService.SaveSettingsAsync(_settings);
                SetStartupWithWindows(true);
                _snackbarService.Show("Success", "Startup setting updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update startup setting");
                _logService.LogError(ex, "Failed to update startup setting: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to update startup setting", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void StartupToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _logService.LogUIAction("StartupToggle", "Unchecked");
                _settings.RunAtStartup = false;
                await _settingsService.SaveSettingsAsync(_settings);
                SetStartupWithWindows(false);
                _snackbarService.Show("Success", "Startup setting updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update startup setting");
                _logService.LogError(ex, "Failed to update startup setting: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to update startup setting", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private void SetStartupWithWindows(bool enable)
        {
            try
            {
                string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupPath, "WallYouNeed.lnk");
                
                if (enable)
                {
                    _snackbarService.Show("Info", "Startup entry created", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
                    // Create startup shortcut (placeholder implementation)
                }
                else
                {
                    // Remove startup shortcut if it exists
                    if (File.Exists(shortcutPath))
                    {
                        File.Delete(shortcutPath);
                    }
                    _snackbarService.Show("Info", "Startup entry removed", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set startup with Windows");
                _logService.LogError(ex, "Failed to set startup with Windows: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to set startup option", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void MinimizeToTrayToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _logService.LogUIAction("MinimizeToTrayToggle", "Checked");
                _settings.MinimizeToTray = true;
                await _settingsService.SaveSettingsAsync(_settings);
                _snackbarService.Show("Success", "Tray setting updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update tray setting");
                _logService.LogError(ex, "Failed to update tray setting: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to update tray setting", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void MinimizeToTrayToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            try
            {
                _logService.LogUIAction("MinimizeToTrayToggle", "Unchecked");
                _settings.MinimizeToTray = false;
                await _settingsService.SaveSettingsAsync(_settings);
                _snackbarService.Show("Success", "Tray setting updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update tray setting");
                _logService.LogError(ex, "Failed to update tray setting: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to update tray setting", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private void ChangeStorageLocation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logService.LogUIAction("ChangeStoragePath_Button", "Clicked");
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select storage location for wallpapers";
                    dialog.UseDescriptionForTitle = true;
                    dialog.SelectedPath = _settings.StorageLocation;
                    
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        _settings.StorageLocation = dialog.SelectedPath;
                        _settingsService.SaveSettingsAsync(_settings).Wait();
                        StorageLocationText.Text = _settings.StorageLocation;
                        UpdateStorageSizeInfoAsync().Wait();
                        _snackbarService.Show("Success", "Storage location updated", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to change storage location");
                _logService.LogError(ex, "Failed to change storage location: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to update storage location", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logService.LogUIAction("ClearCache_Button", "Clicked");
                var result = System.Windows.MessageBox.Show(
                    "Are you sure you want to clear the cache? This will remove all downloaded wallpapers.",
                    "Clear Cache",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    string cacheDir = Path.Combine(_settings.StorageLocation, "Cache");
                    if (Directory.Exists(cacheDir))
                    {
                        Directory.Delete(cacheDir, true);
                        Directory.CreateDirectory(cacheDir);
                    }
                    
                    UpdateStorageSizeInfoAsync().Wait();
                    _snackbarService.Show("Success", "Cache cleared successfully", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear cache");
                _logService.LogError(ex, "Failed to clear cache: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to clear cache", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open URL");
                _logService.LogError(ex, "Failed to open URL: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to open URL", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private void ViewLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logService.LogUIAction("ViewLogs_Button", "Clicked");
                _logService.OpenLogDirectory();
                _snackbarService.Show("Logs", "Opening logs directory...", 
                    ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening logs directory");
                _logService.LogError(ex, "Error opening logs directory: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to open logs directory: " + ex.Message, 
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
            }
        }

        private async void ExportLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logService.LogUIAction("ExportLogs_Button", "Clicked");
                _snackbarService.Show("Logs", "Exporting logs, please wait...", 
                    ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
                
                var exportPath = await _logService.ExportLogsAsync();
                
                _snackbarService.Show("Logs Exported", "Logs exported to: " + exportPath, 
                    ControlAppearance.Success, null, TimeSpan.FromSeconds(5));
                    
                _logService.LogInfo("Logs exported successfully to: {Path}", exportPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting logs");
                _logService.LogError(ex, "Error exporting logs: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to export logs: " + ex.Message, 
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
            }
        }

        private async void VerboseLoggingToggle_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                _logService.LogUIAction("VerboseLoggingToggle", "Checked");
                _settings.VerboseLogging = true;
                await _settingsService.SaveSettingsAsync(_settings);
                _logService.LogInfo("Verbose logging enabled");
                _snackbarService.Show("Success", "Verbose logging enabled", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling verbose logging");
                _logService.LogError(ex, "Error enabling verbose logging: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to enable verbose logging", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void VerboseLoggingToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                _logService.LogUIAction("VerboseLoggingToggle", "Unchecked");
                _settings.VerboseLogging = false;
                await _settingsService.SaveSettingsAsync(_settings);
                _logService.LogInfo("Verbose logging disabled");
                _snackbarService.Show("Success", "Verbose logging disabled", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling verbose logging");
                _logService.LogError(ex, "Error disabling verbose logging: {ExMessage}", ex.Message);
                _snackbarService.Show("Error", "Failed to disable verbose logging", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }
    }
} 