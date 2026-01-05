using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SensePC.Desktop.WinUI.Models;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Collections.Generic;
using Windows.UI;

namespace SensePC.Desktop.WinUI.Views.Dialogs
{
    /// <summary>
    /// Dialog for resizing PC configuration - built programmatically
    /// </summary>
    public sealed class PCResizeDialog : ContentDialog
    {
        private readonly PCInstance _pc;
        private readonly SensePCApiService _apiService;
        private string? _selectedConfigId;
        
        private ListView _configListView;
        private StackPanel _loadingPanel;
        private TextBlock _errorText;

        public bool ResizeConfirmed { get; private set; }

        // Sample configurations - in production, these would come from an API
        private readonly List<PCConfiguration> _configurations = new()
        {
            new PCConfiguration { Id = "basic", Name = "Basic", Description = "4 vCPU, 16 GB RAM", Price = "$0.15/hr" },
            new PCConfiguration { Id = "standard", Name = "Standard", Description = "8 vCPU, 32 GB RAM", Price = "$0.30/hr" },
            new PCConfiguration { Id = "performance", Name = "Performance", Description = "16 vCPU, 64 GB RAM", Price = "$0.60/hr" },
            new PCConfiguration { Id = "professional", Name = "Professional", Description = "32 vCPU, 128 GB RAM", Price = "$1.20/hr" },
            new PCConfiguration { Id = "gpu-basic", Name = "GPU Basic", Description = "8 vCPU, 32 GB RAM, NVIDIA T4", Price = "$0.80/hr" },
            new PCConfiguration { Id = "gpu-pro", Name = "GPU Professional", Description = "16 vCPU, 64 GB RAM, NVIDIA A10G", Price = "$1.50/hr" },
        };

        public PCResizeDialog(PCInstance pc, XamlRoot xamlRoot)
        {
            this.XamlRoot = xamlRoot;
            _pc = pc;
            _apiService = new SensePCApiService(new SecureStorage());

            Title = "Resize PC";
            PrimaryButtonText = "Resize";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Close;
            IsPrimaryButtonEnabled = false;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 450 };

            // Current config
            var currentBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 95, 111, 255)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };
            var currentStack = new StackPanel { Spacing = 4 };
            currentStack.Children.Add(new TextBlock
            {
                Text = "Current Configuration",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Opacity = 0.7
            });
            currentStack.Children.Add(new TextBlock
            {
                Text = _pc.InstanceType ?? _pc.ConfigId ?? "Unknown",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            currentBox.Child = currentStack;
            mainStack.Children.Add(currentBox);

            // Select new config label
            mainStack.Children.Add(new TextBlock
            {
                Text = "Select New Configuration",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            // Config list
            _configListView = new ListView
            {
                SelectionMode = ListViewSelectionMode.Single,
                Height = 200
            };

            foreach (var config in _configurations)
            {
                var item = new ListViewItem
                {
                    Tag = config.Id,
                    Content = CreateConfigItem(config)
                };
                _configListView.Items.Add(item);
            }

            _configListView.SelectionChanged += (s, e) =>
            {
                if (_configListView.SelectedItem is ListViewItem item)
                {
                    _selectedConfigId = item.Tag?.ToString();
                    IsPrimaryButtonEnabled = !string.IsNullOrEmpty(_selectedConfigId);
                }
            };

            mainStack.Children.Add(_configListView);

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
                Text = "Your PC will be stopped during the resize process.",
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
            _loadingPanel.Children.Add(new TextBlock { Text = "Resizing PC...", VerticalAlignment = VerticalAlignment.Center });
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

        private Grid CreateConfigItem(PCConfiguration config)
        {
            var grid = new Grid { Padding = new Thickness(8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel();
            infoStack.Children.Add(new TextBlock
            {
                Text = config.Name,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = config.Description,
                Opacity = 0.7,
                FontSize = 12
            });
            Grid.SetColumn(infoStack, 0);
            grid.Children.Add(infoStack);

            var priceText = new TextBlock
            {
                Text = config.Price,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 204, 102)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(priceText, 1);
            grid.Children.Add(priceText);

            return grid;
        }

        private async void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrEmpty(_selectedConfigId))
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
                _configListView.IsEnabled = false;

                var success = await _apiService.ResizePCAsync(_pc.SystemName, _selectedConfigId);

                if (success)
                {
                    ResizeConfirmed = true;
                }
                else
                {
                    args.Cancel = true;
                    _errorText.Text = "Failed to resize PC. Please try again.";
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
                _configListView.IsEnabled = true;
                deferral.Complete();
            }
        }
    }

    /// <summary>
    /// Represents a PC configuration option for resizing
    /// </summary>
    public class PCConfiguration
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Price { get; set; } = "";
    }
}
