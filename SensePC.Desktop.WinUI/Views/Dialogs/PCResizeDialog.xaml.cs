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
    /// Dialog for resizing PC configuration - matches website functionality
    /// Loads current config from backend, uses dynamic CPU options
    /// CPU resize requires PC to be STOPPED
    /// </summary>
    public sealed class PCResizeDialog : ContentDialog
    {
        private readonly PCInstance _pc;
        private readonly SensePCApiService _apiService;
        private string? _selectedConfigId;
        private string? _currentConfigId;
        
        // UI elements
        private StackPanel _loadingConfigPanel;
        private StackPanel _contentPanel;
        private TextBlock _currentConfigText;
        private ComboBox _cpuComboBox;
        private TextBlock _stateWarningText;
        private StackPanel _savingPanel;
        private TextBlock _errorText;

        public bool ResizeConfirmed { get; private set; }

        // CPU options will be loaded from backend or fallback to defaults
        private Dictionary<string, List<SmartPCConfigOption>> _cpuOptionsByOS = new();

        public PCResizeDialog(PCInstance pc, XamlRoot xamlRoot)
        {
            this.XamlRoot = xamlRoot;
            _pc = pc;
            _apiService = new SensePCApiService(new SecureStorage());

            Title = "Resize PC";
            PrimaryButtonText = "Apply CPU Resize";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Close;
            IsPrimaryButtonEnabled = false;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
            Loaded += OnDialogLoaded;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 450 };

            // Header description
            mainStack.Children.Add(new TextBlock
            {
                Text = $"Resize the CPU and Memory for {_pc.SystemName}.",
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
                Margin = new Thickness(0, 20, 0, 20)
            };
            _loadingConfigPanel.Children.Add(new ProgressRing { IsActive = true, Width = 24, Height = 24 });
            _loadingConfigPanel.Children.Add(new TextBlock 
            { 
                Text = "Loading current configuration...", 
                VerticalAlignment = VerticalAlignment.Center 
            });
            mainStack.Children.Add(_loadingConfigPanel);

            // Content panel (hidden during loading)
            _contentPanel = new StackPanel { Spacing = 16, Visibility = Visibility.Collapsed };

            // Current config display
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
                Opacity = 0.7,
                FontSize = 12
            });
            _currentConfigText = new TextBlock
            {
                Text = _pc.ConfigId ?? _pc.InstanceType ?? "Unknown",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            currentStack.Children.Add(_currentConfigText);
            currentBox.Child = currentStack;
            _contentPanel.Children.Add(currentBox);

            // State warning (CPU resize requires stopped PC)
            _stateWarningText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 136, 0)),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Visibility = Visibility.Collapsed
            };
            _contentPanel.Children.Add(_stateWarningText);

            // CPU selection
            _cpuComboBox = new ComboBox
            {
                Header = "New CPU & Memory Configuration",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Select new configuration"
            };
            _cpuComboBox.SelectionChanged += OnCpuSelectionChanged;
            _contentPanel.Children.Add(_cpuComboBox);

            // Warning box about resize
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
                Text = "CPU resize takes approximately 60 seconds to complete.",
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
            _savingPanel.Children.Add(new TextBlock { Text = "Resizing PC...", VerticalAlignment = VerticalAlignment.Center });
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
            await LoadCurrentConfigAsync();
        }

        private async Task LoadCurrentConfigAsync()
        {
            try
            {
                // Check PC state - CPU resize requires PC to be STOPPED
                var pcState = _pc.State?.ToLowerInvariant() ?? "";
                if (pcState != "stopped")
                {
                    _stateWarningText.Text = $"⚠️ CPU resize requires the PC to be stopped. Current state: {_pc.State}";
                    _stateWarningText.Visibility = Visibility.Visible;
                    IsPrimaryButtonEnabled = false;
                }

                // Load CPU options from backend
                var config = await _apiService.GetSmartPCConfigAsync();
                
                if (config?.CpuOptions != null)
                {
                    _cpuOptionsByOS = config.CpuOptions;
                }
                else
                {
                    // Use defaults if API fails
                    LoadDefaultCpuOptions();
                }

                // Determine the OS of this PC to load appropriate CPU options
                var os = DetermineOS(_pc.ConfigId ?? "");
                PopulateCpuOptions(os);

                // Try to load current config from resize API
                var currentConfig = await _apiService.GetResizeConfigAsync(_pc.SystemName);
                if (currentConfig != null)
                {
                    _currentConfigId = currentConfig.ConfigId;
                    _currentConfigText.Text = FormatConfigLabel(currentConfig.ConfigId ?? _pc.ConfigId ?? "Unknown");
                }
                else
                {
                    _currentConfigId = _pc.ConfigId;
                    _currentConfigText.Text = FormatConfigLabel(_pc.ConfigId ?? _pc.InstanceType ?? "Unknown");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCurrentConfig error: {ex.Message}");
                LoadDefaultCpuOptions();
                var os = DetermineOS(_pc.ConfigId ?? "");
                PopulateCpuOptions(os);
                _currentConfigId = _pc.ConfigId;
            }
            finally
            {
                _loadingConfigPanel.Visibility = Visibility.Collapsed;
                _contentPanel.Visibility = Visibility.Visible;
            }
        }

        private void LoadDefaultCpuOptions()
        {
            _cpuOptionsByOS = new Dictionary<string, List<SmartPCConfigOption>>
            {
                ["Windows 11"] = new()
                {
                    new() { Value = "Basic_win11_2core_4gbRam", Label = "Basic - 2 Core, 4 GB RAM" },
                    new() { Value = "Standerd_win11_4core_8gbRam", Label = "Standard - 4 Core, 8 GB RAM" },
                    new() { Value = "Pro_win11_8core_16gbRam", Label = "Pro - 8 Core, 16 GB RAM" },
                    new() { Value = "Ultra_win11_16core_32gbRam", Label = "Ultra - 16 Core, 32 GB RAM" },
                },
                ["Windows 10"] = new()
                {
                    new() { Value = "Basic_win10_2core_4gbRam", Label = "Basic - 2 Core, 4 GB RAM" },
                    new() { Value = "Standerd_win10_4core_8gbRam", Label = "Standard - 4 Core, 8 GB RAM" },
                    new() { Value = "Pro_win10_8core_16gbRam", Label = "Pro - 8 Core, 16 GB RAM" },
                    new() { Value = "Ultra_win10_16core_32gbRam", Label = "Ultra - 16 Core, 32 GB RAM" },
                },
                ["Linux"] = new()
                {
                    new() { Value = "Ubuntu_24.04_LTS_X64_2core_4gbRam", Label = "Basic - 2 Core, 4 GB RAM" },
                    new() { Value = "Ubuntu_24.04_LTS_X64_4core_8gbRam", Label = "Standard - 4 Core, 8 GB RAM" },
                    new() { Value = "Ubuntu_24.04_LTS_X64_8core_16gbRam", Label = "Pro - 8 Core, 16 GB RAM" },
                    new() { Value = "Ubuntu_24.04_LTS_X64_16core_32gbRam", Label = "Ultra - 16 Core, 32 GB RAM" },
                },
            };
        }

        private string DetermineOS(string configId)
        {
            if (configId.Contains("win11", StringComparison.OrdinalIgnoreCase)) return "Windows 11";
            if (configId.Contains("win10", StringComparison.OrdinalIgnoreCase)) return "Windows 10";
            if (configId.Contains("Ubuntu", StringComparison.OrdinalIgnoreCase) || 
                configId.Contains("Linux", StringComparison.OrdinalIgnoreCase)) return "Linux";
            return "Windows 11"; // Default
        }

        private void PopulateCpuOptions(string os)
        {
            _cpuComboBox.Items.Clear();
            
            if (_cpuOptionsByOS.TryGetValue(os, out var options))
            {
                foreach (var opt in options)
                {
                    _cpuComboBox.Items.Add(new ComboBoxItem { Content = opt.Label, Tag = opt.Value });
                }
            }
            else
            {
                // Try first available OS
                var firstOs = _cpuOptionsByOS.Keys.FirstOrDefault();
                if (firstOs != null && _cpuOptionsByOS.TryGetValue(firstOs, out var fallbackOptions))
                {
                    foreach (var opt in fallbackOptions)
                    {
                        _cpuComboBox.Items.Add(new ComboBoxItem { Content = opt.Label, Tag = opt.Value });
                    }
                }
            }
        }

        private string FormatConfigLabel(string configId)
        {
            // Convert configId to readable format
            if (configId.Contains("2core")) return "Basic - 2 Core, 4 GB RAM";
            if (configId.Contains("4core")) return "Standard - 4 Core, 8 GB RAM";
            if (configId.Contains("8core")) return "Pro - 8 Core, 16 GB RAM";
            if (configId.Contains("16core")) return "Ultra - 16 Core, 32 GB RAM";
            return configId;
        }

        private void OnCpuSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cpuComboBox.SelectedItem is ComboBoxItem item)
            {
                _selectedConfigId = item.Tag?.ToString();
                
                // Check if selection is same as current
                bool isSameConfig = _selectedConfigId == _currentConfigId;
                bool pcIsStopped = _pc.State?.Equals("stopped", StringComparison.OrdinalIgnoreCase) ?? false;
                
                // Enable button only if: different config AND PC is stopped
                IsPrimaryButtonEnabled = !isSameConfig && pcIsStopped;
                
                if (isSameConfig)
                {
                    PrimaryButtonText = "No Changes to Apply";
                }
                else if (!pcIsStopped)
                {
                    PrimaryButtonText = "PC Must Be Stopped";
                }
                else
                {
                    PrimaryButtonText = "Apply CPU Resize";
                }
            }
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
                _savingPanel.Visibility = Visibility.Visible;
                _errorText.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = false;
                _cpuComboBox.IsEnabled = false;

                var success = await _apiService.ResizePCAsync(_pc.SystemName, _selectedConfigId);

                if (success)
                {
                    ResizeConfirmed = true;
                    // Dialog will close
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
                _savingPanel.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = true;
                _cpuComboBox.IsEnabled = true;
                deferral.Complete();
            }
        }
    }
}
