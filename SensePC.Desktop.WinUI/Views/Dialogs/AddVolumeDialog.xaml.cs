using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SensePC.Desktop.WinUI.Models;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace SensePC.Desktop.WinUI.Views.Dialogs
{
    /// <summary>
    /// Dialog for adding/increasing storage volume - matches website functionality
    /// Storage increase requires PC to be RUNNING
    /// </summary>
    public sealed class AddVolumeDialog : ContentDialog
    {
        private readonly PCInstance _pc;
        private readonly SensePCApiService _apiService;
        private readonly int _currentStorageGB;
        private int _selectedSizeGB = 0;
        
        // UI elements
        private StackPanel _loadingConfigPanel;
        private StackPanel _contentPanel;
        private ComboBox _storageSizeCombo;
        private TextBlock _stateWarningText;
        private StackPanel _savingPanel;
        private TextBlock _errorText;

        public bool VolumeIncreased { get; private set; }

        // Storage options matching website (220, 300, 400, 500, 1000 GB)
        private readonly List<VolumeStorageOption> _storageOptions = new()
        {
            new VolumeStorageOption(220, "220 GB"),
            new VolumeStorageOption(300, "300 GB"),
            new VolumeStorageOption(400, "400 GB"),
            new VolumeStorageOption(500, "500 GB"),
            new VolumeStorageOption(1000, "1000 GB (1 TB)"),
        };

        public AddVolumeDialog(PCInstance pc, XamlRoot xamlRoot, int currentStorageGB = 220)
        {
            this.XamlRoot = xamlRoot;
            _pc = pc;
            _currentStorageGB = currentStorageGB;
            _apiService = new SensePCApiService(new SecureStorage());

            Title = "Increase Storage";
            PrimaryButtonText = "Apply Storage Increase";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Close;
            IsPrimaryButtonEnabled = false;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
            Loaded += OnDialogLoaded;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 420 };

            // Header description
            mainStack.Children.Add(new TextBlock
            {
                Text = $"Increase storage volume for {_pc.SystemName}.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8,
                FontSize = 14
            });

            // Loading config panel
            _loadingConfigPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20),
                Visibility = Visibility.Collapsed
            };
            _loadingConfigPanel.Children.Add(new ProgressRing { IsActive = true, Width = 24, Height = 24 });
            _loadingConfigPanel.Children.Add(new TextBlock 
            { 
                Text = "Loading...", 
                VerticalAlignment = VerticalAlignment.Center 
            });
            mainStack.Children.Add(_loadingConfigPanel);

            // Content panel
            _contentPanel = new StackPanel { Spacing = 16 };

            // Current storage display
            var currentBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 95, 111, 255)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };
            var currentStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            currentStack.Children.Add(new FontIcon { Glyph = "\uEDA2", FontSize = 24 });
            var currentTextStack = new StackPanel();
            currentTextStack.Children.Add(new TextBlock { Text = "Current Storage", Opacity = 0.7, FontSize = 12 });
            currentTextStack.Children.Add(new TextBlock
            {
                Text = $"{_currentStorageGB} GB",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            currentStack.Children.Add(currentTextStack);
            currentBox.Child = currentStack;
            _contentPanel.Children.Add(currentBox);

            // State warning (storage increase requires running PC)
            _stateWarningText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 136, 0)),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Visibility = Visibility.Collapsed
            };
            _contentPanel.Children.Add(_stateWarningText);

            // Storage size selection
            _storageSizeCombo = new ComboBox
            {
                Header = "New Storage Size",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Select new storage size"
            };

            // Add storage options (only those larger than current)
            foreach (var option in _storageOptions.Where(o => o.SizeGB > _currentStorageGB))
            {
                var increaseAmount = option.SizeGB - _currentStorageGB;
                _storageSizeCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{option.Label} (+{increaseAmount} GB)",
                    Tag = option.SizeGB
                });
            }

            _storageSizeCombo.SelectionChanged += OnStorageSelectionChanged;
            _contentPanel.Children.Add(_storageSizeCombo);

            // Warning box
            var warningBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 136, 0)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 8, 0, 0)
            };
            var warningStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            warningStack.Children.Add(new FontIcon 
            { 
                Glyph = "\uE7BA", 
                FontSize = 14, 
                Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 150, 0))
            });
            warningStack.Children.Add(new TextBlock
            {
                Text = "Storage increases cannot be reversed. This change will take effect immediately.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            });
            warningBox.Child = warningStack;
            _contentPanel.Children.Add(warningBox);

            mainStack.Children.Add(_contentPanel);

            // Saving panel
            _savingPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _savingPanel.Children.Add(new ProgressRing { IsActive = true, Width = 16, Height = 16 });
            _savingPanel.Children.Add(new TextBlock { Text = "Increasing storage...", VerticalAlignment = VerticalAlignment.Center });
            mainStack.Children.Add(_savingPanel);

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

        private async void OnDialogLoaded(object sender, RoutedEventArgs e)
        {
            await ValidateStateAsync();
        }

        private Task ValidateStateAsync()
        {
            // Storage increase requires PC to be RUNNING (opposite of CPU resize)
            var pcState = _pc.State?.ToLowerInvariant() ?? "";
            if (pcState != "running")
            {
                _stateWarningText.Text = $"⚠️ Storage increase requires the PC to be running. Current state: {_pc.State}";
                _stateWarningText.Visibility = Visibility.Visible;
            }

            // Check if any storage options available
            if (_storageSizeCombo.Items.Count == 0)
            {
                _stateWarningText.Text = "You already have the maximum storage size available.";
                _stateWarningText.Visibility = Visibility.Visible;
            }

            return Task.CompletedTask;
        }

        private void OnStorageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_storageSizeCombo.SelectedItem is ComboBoxItem item && item.Tag is int sizeGB)
            {
                _selectedSizeGB = sizeGB;
                
                // Check if selection is valid
                bool isValidIncrease = sizeGB > _currentStorageGB;
                bool pcIsRunning = _pc.State?.Equals("running", StringComparison.OrdinalIgnoreCase) ?? false;
                
                // Enable button only if: larger size AND PC is running
                IsPrimaryButtonEnabled = isValidIncrease && pcIsRunning;
                
                if (!pcIsRunning)
                {
                    PrimaryButtonText = "PC Must Be Running";
                }
                else
                {
                    PrimaryButtonText = "Apply Storage Increase";
                }
            }
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
                _savingPanel.Visibility = Visibility.Visible;
                _errorText.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = false;
                _storageSizeCombo.IsEnabled = false;

                var success = await _apiService.IncreaseVolumeAsync(_pc.SystemName, _selectedSizeGB);

                if (success)
                {
                    VolumeIncreased = true;
                    // Dialog will close
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
                _savingPanel.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = true;
                _storageSizeCombo.IsEnabled = true;
                deferral.Complete();
            }
        }
    }

    internal class VolumeStorageOption
    {
        public int SizeGB { get; }
        public string Label { get; }
        public VolumeStorageOption(int sizeGB, string label) { SizeGB = sizeGB; Label = label; }
    }
}
