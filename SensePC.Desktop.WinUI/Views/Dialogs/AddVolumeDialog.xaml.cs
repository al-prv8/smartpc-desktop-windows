using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SensePC.Desktop.WinUI.Models;
using SensePC.Desktop.WinUI.Services;
using System;
using Windows.UI;

namespace SensePC.Desktop.WinUI.Views.Dialogs
{
    /// <summary>
    /// Dialog for adding/increasing storage volume - built programmatically
    /// </summary>
    public sealed class AddVolumeDialog : ContentDialog
    {
        private readonly PCInstance _pc;
        private readonly SensePCApiService _apiService;
        private readonly int _currentStorageGB;
        private int _selectedSizeGB = 0;
        
        private ComboBox _storageSizeCombo;
        private TextBlock _priceEstimateText;
        private StackPanel _loadingPanel;
        private TextBlock _errorText;

        // Cost per GB per month (sample pricing)
        private const double PricePerGBPerMonth = 0.10;

        public bool VolumeIncreased { get; private set; }

        public AddVolumeDialog(PCInstance pc, XamlRoot xamlRoot, int currentStorageGB)
        {
            this.XamlRoot = xamlRoot;
            _pc = pc;
            _currentStorageGB = currentStorageGB;
            _apiService = new SensePCApiService(new SecureStorage());

            Title = "Increase Storage";
            PrimaryButtonText = "Increase";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Close;
            IsPrimaryButtonEnabled = false;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 400 };

            // Current storage display
            var currentBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 95, 111, 255)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };
            var currentStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            currentStack.Children.Add(new FontIcon { Glyph = "\uEDA2", FontSize = 24 });
            var currentTextStack = new StackPanel();
            currentTextStack.Children.Add(new TextBlock { Text = "Current Storage", Opacity = 0.7, FontSize = 12 });
            currentTextStack.Children.Add(new TextBlock
            {
                Text = $"{_currentStorageGB} GB",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            currentStack.Children.Add(currentTextStack);
            currentBox.Child = currentStack;
            mainStack.Children.Add(currentBox);

            // New size combo
            _storageSizeCombo = new ComboBox
            {
                Header = "New Storage Size",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Select new size"
            };

            // Add storage options (only larger than current)
            var storageSizes = new[] { 100, 150, 200, 250, 300, 400, 500, 750, 1000, 2000 };
            foreach (var size in storageSizes)
            {
                if (size > _currentStorageGB)
                {
                    _storageSizeCombo.Items.Add(new ComboBoxItem
                    {
                        Content = $"{size} GB (+{size - _currentStorageGB} GB)",
                        Tag = size
                    });
                }
            }

            _storageSizeCombo.SelectionChanged += (s, e) =>
            {
                if (_storageSizeCombo.SelectedItem is ComboBoxItem item && item.Tag is int size)
                {
                    _selectedSizeGB = size;
                    var additionalGB = size - _currentStorageGB;
                    var monthlyCost = additionalGB * PricePerGBPerMonth;
                    _priceEstimateText.Text = $"Estimated additional cost: ${monthlyCost:F2}/month";
                    _priceEstimateText.Visibility = Visibility.Visible;
                    IsPrimaryButtonEnabled = true;
                }
            };

            mainStack.Children.Add(_storageSizeCombo);

            // Price estimate
            _priceEstimateText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 204, 102)),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Visibility = Visibility.Collapsed
            };
            mainStack.Children.Add(_priceEstimateText);

            // Warning
            var warningBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 136, 0)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12)
            };
            var warningStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            warningStack.Children.Add(new FontIcon { Glyph = "\uE7BA", FontSize = 16, Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 136, 0)) });
            warningStack.Children.Add(new TextBlock
            {
                Text = "Storage increases cannot be reversed. This change will take effect immediately.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            });
            warningBox.Child = warningStack;
            mainStack.Children.Add(warningBox);

            // Loading panel
            _loadingPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _loadingPanel.Children.Add(new ProgressRing { IsActive = true, Width = 16, Height = 16 });
            _loadingPanel.Children.Add(new TextBlock { Text = "Increasing storage...", VerticalAlignment = VerticalAlignment.Center });
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
            if (_selectedSizeGB <= _currentStorageGB)
            {
                args.Cancel = true;
                return;
            }

            var deferral = args.GetDeferral();

            try
            {
                _loadingPanel.Visibility = Visibility.Visible;
                _errorText.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = false;
                _storageSizeCombo.IsEnabled = false;

                var success = await _apiService.IncreaseVolumeAsync(_pc.SystemName, _selectedSizeGB);

                if (success)
                {
                    VolumeIncreased = true;
                }
                else
                {
                    args.Cancel = true;
                    _errorText.Text = "Failed to increase storage. Please try again.";
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
                _storageSizeCombo.IsEnabled = true;
                deferral.Complete();
            }
        }
    }
}
