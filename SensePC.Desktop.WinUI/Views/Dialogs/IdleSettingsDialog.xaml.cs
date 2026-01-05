using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SensePC.Desktop.WinUI.Models;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI;

namespace SensePC.Desktop.WinUI.Views.Dialogs
{
    /// <summary>
    /// Dialog for configuring idle timeout settings - matches website functionality
    /// </summary>
    public sealed class IdleSettingsDialog : ContentDialog
    {
        private readonly PCInstance _pc;
        private readonly SensePCApiService _apiService;
        private readonly int? _currentTimeout;
        
        private ComboBox _timeoutCombo;
        private StackPanel _loadingPanel;
        private TextBlock _errorText;

        public bool SettingsSaved { get; private set; }

        // Timeout options matching website (15, 30, 45, 60 minutes + None)
        private readonly List<IdleTimeoutOption> _timeoutOptions = new()
        {
            new("none", "None (Disabled)", 0),
            new("15", "15 minutes", 15),
            new("30", "30 minutes", 30),
            new("45", "45 minutes", 45),
            new("60", "1 hour", 60),
        };

        public IdleSettingsDialog(XamlRoot xamlRoot, PCInstance pc, SensePCApiService apiService, int? currentTimeout = null)
        {
            this.XamlRoot = xamlRoot;
            _pc = pc;
            _apiService = apiService;
            _currentTimeout = currentTimeout;

            Title = "Configure Idle Settings";
            PrimaryButtonText = "Save Changes";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 420 };

            // Header description matching website
            mainStack.Children.Add(new TextBlock
            {
                Text = $"Set the idle timeout duration for {_pc.SystemName}.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8,
                FontSize = 14
            });
            mainStack.Children.Add(new TextBlock
            {
                Text = "The PC will be STOPPED after being idle for the specified duration.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                FontSize = 13
            });

            // Divider
            mainStack.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                Margin = new Thickness(0, 8, 0, 8)
            });

            // Timeout selection
            var fieldStack = new StackPanel { Spacing = 8 };
            fieldStack.Children.Add(new TextBlock
            {
                Text = "Idle Timeout (minutes)",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 16
            });

            _timeoutCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Select timeout duration"
            };

            int selectedIndex = 2; // Default to 30 minutes
            for (int i = 0; i < _timeoutOptions.Count; i++)
            {
                var option = _timeoutOptions[i];
                _timeoutCombo.Items.Add(new ComboBoxItem 
                { 
                    Content = option.Label, 
                    Tag = option.Minutes 
                });

                // Pre-select based on current timeout
                if (_currentTimeout.HasValue && option.Minutes == _currentTimeout.Value)
                {
                    selectedIndex = i;
                }
                else if (!_currentTimeout.HasValue && option.Minutes == 0)
                {
                    // If no current timeout, select "None"
                    selectedIndex = i;
                }
            }
            _timeoutCombo.SelectedIndex = selectedIndex;
            fieldStack.Children.Add(_timeoutCombo);

            mainStack.Children.Add(fieldStack);

            // Info box
            var infoBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 180, 0)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 8, 0, 0)
            };
            var infoStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            infoStack.Children.Add(new FontIcon 
            { 
                Glyph = "\uE7BA", 
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 150, 0))
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = "Setting to 'None' will disable automatic idle shutdown.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.9
            });
            infoBox.Child = infoStack;
            mainStack.Children.Add(infoBox);

            // Loading panel
            _loadingPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 8, 0, 0)
            };
            _loadingPanel.Children.Add(new ProgressRing { IsActive = true, Width = 16, Height = 16 });
            _loadingPanel.Children.Add(new TextBlock { Text = "Saving...", VerticalAlignment = VerticalAlignment.Center });
            mainStack.Children.Add(_loadingPanel);

            // Error text
            _errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 68, 68)),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            mainStack.Children.Add(_errorText);

            Content = mainStack;
        }

        private async void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var deferral = args.GetDeferral();

            try
            {
                _loadingPanel.Visibility = Visibility.Visible;
                _errorText.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = false;

                int timeoutMinutes = 0;
                if (_timeoutCombo.SelectedItem is ComboBoxItem selectedItem)
                {
                    timeoutMinutes = (int)(selectedItem.Tag ?? 0);
                }

                bool success;
                if (timeoutMinutes == 0)
                {
                    // Delete/disable idle timeout
                    success = await _apiService.DeleteIdleTimeoutAsync(_pc.InstanceId);
                }
                else
                {
                    // Set idle timeout
                    success = await _apiService.SetIdleTimeoutAsync(_pc.InstanceId, timeoutMinutes);
                }

                if (success)
                {
                    SettingsSaved = true;
                    // Dialog will close
                }
                else
                {
                    args.Cancel = true;
                    _errorText.Text = "Failed to save settings. Please try again.";
                    _errorText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                args.Cancel = true;
                _errorText.Text = $"Error: {ex.Message}";
                _errorText.Visibility = Visibility.Visible;
            }
            finally
            {
                _loadingPanel.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = true;
                deferral.Complete();
            }
        }
    }

    internal record IdleTimeoutOption(string Value, string Label, int Minutes);
}
