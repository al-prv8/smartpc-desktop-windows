using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace SensePC.Desktop.WinUI.Views.Dialogs
{
    /// <summary>
    /// Dialog for building/creating a new SensePC - built programmatically
    /// Fetches configuration options from backend API
    /// </summary>
    public sealed class BuildNewPCDialog : ContentDialog
    {
        private readonly SensePCApiService _apiService;
        
        private TextBox _pcNameTextBox;
        private ComboBox _osComboBox;
        private ComboBox _cpuComboBox;
        private ComboBox _storageComboBox;
        private ComboBox _regionComboBox;
        private StackPanel _loadingPanel;
        private StackPanel _formPanel;
        private StackPanel _configLoadingPanel;
        private TextBlock _errorText;
        private TextBlock _estimatedCostText;

        // Dynamic config from backend
        private SmartPCConfigResponse? _config;
        private Dictionary<string, List<SmartPCConfigOption>> _cpuOptionsByOS = new();

        public bool PCCreated { get; private set; }

        // Fallback storage options if not provided by backend
        private readonly List<StorageOption> _defaultStorageOptions = new()
        {
            new("220", "220 GB", 0.01),
            new("300", "300 GB", 0.015),
            new("400", "400 GB", 0.02),
            new("500", "500 GB", 0.025),
            new("1000", "1000 GB (1 TB)", 0.05),
        };

        // Fallback region options if not provided by backend
        private readonly List<RegionOption> _defaultRegionOptions = new()
        {
            new("us-east-1", "US East Coast"),
        };

        public BuildNewPCDialog(XamlRoot xamlRoot)
        {
            this.XamlRoot = xamlRoot;
            _apiService = new SensePCApiService(new SecureStorage());

            Title = "Build New SensePC";
            PrimaryButtonText = "Build PC";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;
            IsPrimaryButtonEnabled = false;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
            Loaded += OnDialogLoaded;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 20, MinWidth = 500 };

            // Header
            var headerStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 8 };
            var icon = new FontIcon
            {
                Glyph = "\uE710",
                FontSize = 48,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 95, 111, 255))
            };
            headerStack.Children.Add(icon);
            headerStack.Children.Add(new TextBlock
            {
                Text = "Create a New Cloud PC",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            mainStack.Children.Add(headerStack);

            // Config loading panel
            _configLoadingPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20)
            };
            _configLoadingPanel.Children.Add(new ProgressRing { IsActive = true, Width = 24, Height = 24 });
            _configLoadingPanel.Children.Add(new TextBlock 
            { 
                Text = "Loading configurations...", 
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            });
            mainStack.Children.Add(_configLoadingPanel);

            // Form section (hidden until config loads)
            _formPanel = new StackPanel { Spacing = 16, Visibility = Visibility.Collapsed };

            // PC Name
            _pcNameTextBox = new TextBox
            {
                Header = "PC Name",
                PlaceholderText = "Enter a name for your PC (e.g., MyDevPC)",
                MaxLength = 50
            };
            _pcNameTextBox.TextChanged += ValidateForm;
            _formPanel.Children.Add(_pcNameTextBox);

            // Operating System
            _osComboBox = new ComboBox
            {
                Header = "Operating System",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Select operating system"
            };
            _osComboBox.SelectionChanged += OnOsChanged;
            _formPanel.Children.Add(_osComboBox);

            // CPU/RAM Configuration
            _cpuComboBox = new ComboBox
            {
                Header = "CPU & Memory",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Select configuration",
                IsEnabled = false
            };
            _cpuComboBox.SelectionChanged += ValidateFormHandler;
            _formPanel.Children.Add(_cpuComboBox);

            // Storage
            _storageComboBox = new ComboBox
            {
                Header = "Storage (SSD)",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Select storage size"
            };
            _storageComboBox.SelectionChanged += ValidateFormHandler;
            _formPanel.Children.Add(_storageComboBox);

            // Region
            _regionComboBox = new ComboBox
            {
                Header = "Region",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Select region"
            };
            _regionComboBox.SelectionChanged += ValidateFormHandler;
            _formPanel.Children.Add(_regionComboBox);

            mainStack.Children.Add(_formPanel);

            // Cost estimate section
            var costBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 0, 200, 100)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 8, 0, 0),
                Visibility = Visibility.Collapsed,
                Name = "CostBox"
            };
            var costStack = new StackPanel { Spacing = 4 };
            costStack.Children.Add(new TextBlock
            {
                Text = "Billing Plan: Hourly",
                Opacity = 0.8,
                FontSize = 12
            });
            _estimatedCostText = new TextBlock
            {
                Text = "Select configuration to see estimate",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 180, 80))
            };
            costStack.Children.Add(_estimatedCostText);
            costBox.Child = costStack;
            mainStack.Children.Add(costBox);

            // Info box
            var infoBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 95, 111, 255)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Visibility = Visibility.Collapsed,
                Name = "InfoBox"
            };
            var infoStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            infoStack.Children.Add(new FontIcon { Glyph = "\uE946", FontSize = 16 });
            infoStack.Children.Add(new TextBlock
            {
                Text = "Your PC will start building immediately. You can resize CPU/storage later.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            });
            infoBox.Child = infoStack;
            mainStack.Children.Add(infoBox);

            // Loading panel
            _loadingPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _loadingPanel.Children.Add(new ProgressRing { IsActive = true, Width = 20, Height = 20 });
            _loadingPanel.Children.Add(new TextBlock { Text = "Creating your PC...", VerticalAlignment = VerticalAlignment.Center });
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

        private async void OnDialogLoaded(object sender, RoutedEventArgs e)
        {
            await LoadConfigurationsAsync();
        }

        private async Task LoadConfigurationsAsync()
        {
            try
            {
                _config = await _apiService.GetSmartPCConfigAsync();

                if (_config != null)
                {
                    // Load OS options
                    if (_config.OsOptions != null && _config.OsOptions.Count > 0)
                    {
                        foreach (var os in _config.OsOptions)
                        {
                            _osComboBox.Items.Add(new ComboBoxItem { Content = os.Label, Tag = os.Value });
                        }
                    }
                    else if (_config.CpuOptions != null)
                    {
                        // Fallback: use keys from cpuOptions as OS options
                        foreach (var os in _config.CpuOptions.Keys)
                        {
                            _osComboBox.Items.Add(new ComboBoxItem { Content = os, Tag = os });
                        }
                    }

                    // Store CPU options for later use
                    if (_config.CpuOptions != null)
                    {
                        _cpuOptionsByOS = _config.CpuOptions;
                    }

                    // Load storage options
                    if (_config.StorageOptions != null && _config.StorageOptions.Count > 0)
                    {
                        foreach (var storage in _config.StorageOptions)
                        {
                            _storageComboBox.Items.Add(new ComboBoxItem { Content = storage.Label, Tag = storage.Value });
                        }
                    }
                    else
                    {
                        // Use default storage options
                        foreach (var storage in _defaultStorageOptions)
                        {
                            _storageComboBox.Items.Add(new ComboBoxItem { Content = storage.Label, Tag = storage.Value });
                        }
                    }
                    if (_storageComboBox.Items.Count > 0)
                        _storageComboBox.SelectedIndex = 0;

                    // Load region options
                    if (_config.LocationOptions != null && _config.LocationOptions.Count > 0)
                    {
                        foreach (var region in _config.LocationOptions)
                        {
                            _regionComboBox.Items.Add(new ComboBoxItem { Content = region.Label, Tag = region.Value });
                        }
                    }
                    else
                    {
                        // Use default region options
                        foreach (var region in _defaultRegionOptions)
                        {
                            _regionComboBox.Items.Add(new ComboBoxItem { Content = region.Label, Tag = region.Value });
                        }
                    }
                    if (_regionComboBox.Items.Count > 0)
                        _regionComboBox.SelectedIndex = 0;
                }
                else
                {
                    // Config fetch failed, use hardcoded defaults
                    LoadDefaultConfigurations();
                }

                // Show form, hide loading
                _configLoadingPanel.Visibility = Visibility.Collapsed;
                _formPanel.Visibility = Visibility.Visible;
                
                // Show cost and info boxes
                if (Content is StackPanel mainStack)
                {
                    foreach (var child in mainStack.Children)
                    {
                        if (child is Border border && (border.Name == "CostBox" || border.Name == "InfoBox"))
                        {
                            border.Visibility = Visibility.Visible;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadConfigurations error: {ex.Message}");
                LoadDefaultConfigurations();
                _configLoadingPanel.Visibility = Visibility.Collapsed;
                _formPanel.Visibility = Visibility.Visible;
            }
        }

        private void LoadDefaultConfigurations()
        {
            // Default OS options
            var defaultOS = new[] { "Windows 11", "Windows 10", "Linux" };
            foreach (var os in defaultOS)
            {
                _osComboBox.Items.Add(new ComboBoxItem { Content = os, Tag = os });
            }

            // Default CPU options
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
                    new() { Value = "Ubuntu_24.04_LTS_X64_Token_Test", Label = "Token Test (Dev Only)" },
                    new() { Value = "Ubuntu_24.04_LTS_X64_2core_4gbRam", Label = "Basic - 2 Core, 4 GB RAM" },
                    new() { Value = "Ubuntu_24.04_LTS_X64_4core_8gbRam", Label = "Standard - 4 Core, 8 GB RAM" },
                    new() { Value = "Ubuntu_24.04_LTS_X64_8core_16gbRam", Label = "Pro - 8 Core, 16 GB RAM" },
                    new() { Value = "Ubuntu_24.04_LTS_X64_16core_32gbRam", Label = "Ultra - 16 Core, 32 GB RAM" },
                },
            };

            // Default storage
            foreach (var storage in _defaultStorageOptions)
            {
                _storageComboBox.Items.Add(new ComboBoxItem { Content = storage.Label, Tag = storage.Value });
            }
            if (_storageComboBox.Items.Count > 0)
                _storageComboBox.SelectedIndex = 0;

            // Default region
            foreach (var region in _defaultRegionOptions)
            {
                _regionComboBox.Items.Add(new ComboBoxItem { Content = region.Label, Tag = region.Value });
            }
            if (_regionComboBox.Items.Count > 0)
                _regionComboBox.SelectedIndex = 0;
        }

        private void OnOsChanged(object sender, SelectionChangedEventArgs e)
        {
            _cpuComboBox.Items.Clear();
            
            if (_osComboBox.SelectedItem is ComboBoxItem osItem && osItem.Tag is string osValue)
            {
                _cpuComboBox.IsEnabled = true;
                
                if (_cpuOptionsByOS.TryGetValue(osValue, out var cpuOptions))
                {
                    foreach (var cpu in cpuOptions)
                    {
                        _cpuComboBox.Items.Add(new ComboBoxItem { Content = cpu.Label, Tag = cpu.Value });
                    }
                    
                    if (_cpuComboBox.Items.Count > 0)
                    {
                        _cpuComboBox.SelectedIndex = 0;
                    }
                }
            }
            else
            {
                _cpuComboBox.IsEnabled = false;
            }
            
            ValidateForm(null, null);
            UpdateCostEstimate();
        }

        private void ValidateFormHandler(object sender, SelectionChangedEventArgs e)
        {
            ValidateForm(null, null);
            UpdateCostEstimate();
        }

        private void ValidateForm(object? sender, TextChangedEventArgs? e)
        {
            bool isValid = true;

            // PC Name required and valid
            var pcName = _pcNameTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(pcName) || pcName.Length < 3)
            {
                isValid = false;
            }
            // Check for valid characters (alphanumeric, hyphen, underscore)
            if (!System.Text.RegularExpressions.Regex.IsMatch(pcName, @"^[a-zA-Z0-9_-]+$") && !string.IsNullOrEmpty(pcName))
            {
                isValid = false;
            }

            // All combos must have selections
            if (_osComboBox.SelectedItem == null) isValid = false;
            if (_cpuComboBox.SelectedItem == null) isValid = false;
            if (_storageComboBox.SelectedItem == null) isValid = false;
            if (_regionComboBox.SelectedItem == null) isValid = false;

            IsPrimaryButtonEnabled = isValid;
        }

        private void UpdateCostEstimate()
        {
            // Basic hourly cost estimation based on config
            double hourlyRate = 0.10; // Base rate
            
            if (_cpuComboBox.SelectedItem is ComboBoxItem cpuItem && cpuItem.Tag is string cpuValue)
            {
                if (cpuValue.Contains("2core") || cpuValue.Contains("Token_Test")) hourlyRate = 0.10;
                else if (cpuValue.Contains("4core")) hourlyRate = 0.20;
                else if (cpuValue.Contains("8core")) hourlyRate = 0.40;
                else if (cpuValue.Contains("16core")) hourlyRate = 0.80;
            }

            if (_storageComboBox.SelectedItem is ComboBoxItem storageItem && storageItem.Tag is string storageValue)
            {
                if (int.TryParse(storageValue, out var storageGB))
                {
                    hourlyRate += storageGB * 0.00005; // Approximate storage cost
                }
            }

            _estimatedCostText.Text = $"Estimated: ${hourlyRate:F3}/hour (~${hourlyRate * 24:F2}/day)";
        }

        private async void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var deferral = args.GetDeferral();

            try
            {
                _loadingPanel.Visibility = Visibility.Visible;
                _errorText.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = false;
                SetFormEnabled(false);

                var pcName = _pcNameTextBox.Text.Trim();
                var configId = (_cpuComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
                var storageSize = int.Parse((_storageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "220");
                var region = (_regionComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "us-east-1";
                var billingPlan = "hourly";

                var result = await _apiService.CreateVMAsync(configId, pcName, region, storageSize, billingPlan);

                // Check for success - status codes 200, 201, or null (which means success in our case)
                bool isSuccess = result.StatusCode == null || 
                                 result.StatusCode == 0 || 
                                 result.StatusCode == 200 || 
                                 result.StatusCode == 201 ||
                                 (result.StatusCode >= 200 && result.StatusCode < 300);

                if (isSuccess)
                {
                    PCCreated = true;
                    // Dialog will close automatically (args.Cancel is false by default)
                }
                else
                {
                    args.Cancel = true;
                    _errorText.Text = result.Message ?? "Failed to create PC. Please try again.";
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
                SetFormEnabled(true);
                deferral.Complete();
            }
        }

        private void SetFormEnabled(bool enabled)
        {
            _pcNameTextBox.IsEnabled = enabled;
            _osComboBox.IsEnabled = enabled;
            _cpuComboBox.IsEnabled = enabled && _osComboBox.SelectedItem != null;
            _storageComboBox.IsEnabled = enabled;
            _regionComboBox.IsEnabled = enabled;
        }
    }

    // Helper classes
    internal class StorageOption
    {
        public string Value { get; }
        public string Label { get; }
        public double HourlyRate { get; }
        public StorageOption(string value, string label, double hourlyRate) { Value = value; Label = label; HourlyRate = hourlyRate; }
    }

    internal class RegionOption
    {
        public string Value { get; }
        public string Label { get; }
        public RegionOption(string value, string label) { Value = value; Label = label; }
    }
}
