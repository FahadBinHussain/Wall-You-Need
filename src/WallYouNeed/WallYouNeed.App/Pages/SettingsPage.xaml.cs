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
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : Page
    {
        private readonly ISnackbarService _snackbarService;
        
        public SettingsPage()
        {
            InitializeComponent();
        }
        
        public SettingsPage(ISnackbarService snackbarService)
        {
            _snackbarService = snackbarService;
            
            InitializeComponent();
            
            // Set up event handlers
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Set initial values and state
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _snackbarService?.Show("Theme", "Theme selection changed", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
        }

        private void AutoLaunchToggle_Checked(object sender, RoutedEventArgs e)
        {
            _snackbarService?.Show("Auto Launch", "Auto launch enabled", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
        }

        private void AutoLaunchToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _snackbarService?.Show("Auto Launch", "Auto launch disabled", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
        }

        private void RemoveWidgetsButton_Click(object sender, RoutedEventArgs e)
        {
            _snackbarService?.Show("Widgets", "Widgets removed", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
        }

        private void NotificationExpandButton_Click(object sender, RoutedEventArgs e)
        {
            _snackbarService?.Show("Notifications", "Notification settings expanded", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
        }

        private void QuickLikeToggle_Checked(object sender, RoutedEventArgs e)
        {
            _snackbarService?.Show("Quick Like", "Quick like enabled", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
        }

        private void QuickLikeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _snackbarService?.Show("Quick Like", "Quick like disabled", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
        }

        private void SyncToggle_Checked(object sender, RoutedEventArgs e)
        {
            _snackbarService?.Show("Sync", "Synchronization enabled", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
        }

        private void SyncToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _snackbarService?.Show("Sync", "Synchronization disabled", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _snackbarService?.Show("Language", "Language changed", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
        }

        private void ShowHintsButton_Click(object sender, RoutedEventArgs e)
        {
            _snackbarService?.Show("Hints", "Hints will be shown again", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
        }
    }
} 