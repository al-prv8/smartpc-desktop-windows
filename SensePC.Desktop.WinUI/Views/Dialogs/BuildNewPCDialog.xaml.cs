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
        private ComboBox _billingPlanComboBox;
        private StackPanel _loadingPanel;
        private StackPanel _formPanel;
        private StackPanel _configLoadingPanel;
        private TextBlock _errorText;
        private TextBlock _estimatedCostText;
        private ToggleSwitch _gpuOnlyToggle;

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

            // PC Name (full width)
            _pcNameTextBox = new TextBox
            {
                Header = "PC Name",
                PlaceholderText = "Enter a name for your PC (e.g., MyDevPC)",
                MaxLength = 50
            };
            _pcNameTextBox.TextChanged += ValidateForm;
            _formPanel.Children.Add(_pcNameTextBox);

            // Two-column grid for form fields
            var formGrid = new Grid { ColumnSpacing = 16, RowSpacing = 12 };
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 0: OS, CPU (both with headers)
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 1: Storage, Region
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 2: Billing Plan (full width)

            // Row 0, Col 0: Operating System Section
            var osSection = new StackPanel { Spacing = 4 };
            osSection.Children.Add(new TextBlock 
            { 
                Text = "Operating System", 
                FontSize = 14
            });
            _osComboBox = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Select OS"
            };
            _osComboBox.SelectionChanged += OnOsChanged;
            osSection.Children.Add(_osComboBox);
            Grid.SetRow(osSection, 0);
            Grid.SetColumn(osSection, 0);
            formGrid.Children.Add(osSection);

            // Row 0, Col 1: CPU Section (Header with GPU toggle + ComboBox)
            var cpuSection = new StackPanel { Spacing = 4 };
            
            // CPU Header with GPU Toggle
            var cpuHeaderPanel = new Grid();
            cpuHeaderPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cpuHeaderPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var cpuLabel = new TextBlock 
            { 
                Text = "CPU & Memory", 
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(cpuLabel, 0);
            cpuHeaderPanel.Children.Add(cpuLabel);
            
            _gpuOnlyToggle = new ToggleSwitch
            {
                OffContent = "All",
                OnContent = "GPU",
                IsOn = false,
                MinWidth = 0
            };
            _gpuOnlyToggle.Toggled += OnGpuToggleChanged;
            Grid.SetColumn(_gpuOnlyToggle, 1);
            cpuHeaderPanel.Children.Add(_gpuOnlyToggle);
            cpuSection.Children.Add(cpuHeaderPanel);

            // CPU ComboBox
            _cpuComboBox = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Select configuration",
                IsEnabled = false
            };
            _cpuComboBox.SelectionChanged += ValidateFormHandler;
            cpuSection.Children.Add(_cpuComboBox);
            
            Grid.SetRow(cpuSection, 0);
            Grid.SetColumn(cpuSection, 1);
            formGrid.Children.Add(cpuSection);

            // Row 1, Col 0: Storage
            _storageComboBox = new ComboBox
            {
                Header = "Storage (SSD)",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Select size"
            };
            _storageComboBox.SelectionChanged += ValidateFormHandler;
            Grid.SetRow(_storageComboBox, 1);
            Grid.SetColumn(_storageComboBox, 0);
            formGrid.Children.Add(_storageComboBox);

            // Row 1, Col 1: Region
            _regionComboBox = new ComboBox
            {
                Header = "Region",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Select region"
            };
            _regionComboBox.SelectionChanged += ValidateFormHandler;
            Grid.SetRow(_regionComboBox, 1);
            Grid.SetColumn(_regionComboBox, 1);
            formGrid.Children.Add(_regionComboBox);

            // Row 2: Billing Plan (full width)
            _billingPlanComboBox = new ComboBox
            {
                Header = "Billing Plan",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Select billing plan"
            };
            _billingPlanComboBox.Items.Add(new ComboBoxItem { Content = "Hourly - Pay as you go", Tag = "hourly" });
            _billingPlanComboBox.Items.Add(new ComboBoxItem { Content = "Monthly - Save 10%", Tag = "monthly" });
            _billingPlanComboBox.Items.Add(new ComboBoxItem { Content = "Yearly - Save 20%", Tag = "yearly" });
            _billingPlanComboBox.SelectedIndex = 0;
            _billingPlanComboBox.SelectionChanged += ValidateFormHandler;
            Grid.SetRow(_billingPlanComboBox, 2);
            Grid.SetColumn(_billingPlanComboBox, 0);
            Grid.SetColumnSpan(_billingPlanComboBox, 2);
            formGrid.Children.Add(_billingPlanComboBox);

            _formPanel.Children.Add(formGrid);

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

        private void OnGpuToggleChanged(object sender, RoutedEventArgs e)
        {
            // Re-populate CPU options based on GPU toggle state
            OnOsChanged(sender, null);
        }

        private void OnOsChanged(object sender, SelectionChangedEventArgs e)
        {
            _cpuComboBox.Items.Clear();
            
            if (_osComboBox.SelectedItem is ComboBoxItem osItem && osItem.Tag is string osValue)
            {
                _cpuComboBox.IsEnabled = true;
                
                if (_cpuOptionsByOS.TryGetValue(osValue, out var cpuOptions))
                {
                    var filteredOptions = cpuOptions;
                    
                    // Filter for GPU configurations if toggle is on
                    if (_gpuOnlyToggle?.IsOn == true)
                    {
                        filteredOptions = cpuOptions.Where(cpu => 
                            cpu.Label?.Contains("GPU", StringComparison.OrdinalIgnoreCase) == true ||
                            cpu.Value?.Contains("gpu", StringComparison.OrdinalIgnoreCase) == true ||
                            cpu.Value?.Contains("nvidia", StringComparison.OrdinalIgnoreCase) == true ||
                            cpu.Value?.Contains("graphics", StringComparison.OrdinalIgnoreCase) == true ||
                            cpu.Label?.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) == true ||
                            cpu.Label?.Contains("Graphics", StringComparison.OrdinalIgnoreCase) == true
                        ).ToList();
                    }
                    
                    foreach (var cpu in filteredOptions)
                    {
                        _cpuComboBox.Items.Add(new ComboBoxItem { Content = cpu.Label, Tag = cpu.Value });
                    }
                    
                    if (_cpuComboBox.Items.Count > 0)
                    {
                        _cpuComboBox.SelectedIndex = 0;
                    }
                    else if (_gpuOnlyToggle?.IsOn == true)
                    {
                        // No GPU options available for this OS
                        _cpuComboBox.PlaceholderText = "No GPU options for this OS";
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

        private async void UpdateCostEstimate()
        {
            // Get current selections
            var configId = (_cpuComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var storageValue = (_storageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var region = (_regionComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "us-east-1";
            var billingPlan = (_billingPlanComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "hourly";

            // If we don't have required selections, show placeholder
            if (string.IsNullOrEmpty(configId) || string.IsNullOrEmpty(storageValue))
            {
                _estimatedCostText.Text = "Select config to see estimate";
                return;
            }

            // Parse storage size
            if (!int.TryParse(storageValue, out var storageSize))
            {
                storageSize = 220; // Default
            }

            // Show loading state
            _estimatedCostText.Text = "Calculating...";

            try
            {
                // Call the estimation API
                var estimate = await _apiService.GetCostEstimateAsync(configId, storageSize, region);

                if (estimate?.Total != null)
                {
                    // Display based on billing plan
                    var displayText = billingPlan switch
                    {
                        "monthly" => estimate.Total.PricePerMonth.HasValue 
                            ? $"Estimated: ${estimate.Total.PricePerMonth:F2}/month"
                            : $"Estimated: ${(estimate.Total.PricePerHour ?? 0) * 24 * 30:F2}/month",
                        "yearly" => estimate.Total.PricePerMonth.HasValue
                            ? $"Estimated: ${estimate.Total.PricePerMonth * 12:F2}/year"
                            : $"Estimated: ${(estimate.Total.PricePerHour ?? 0) * 24 * 365:F2}/year",
                        _ => estimate.Total.PricePerHour.HasValue
                            ? $"Estimated: ${estimate.Total.PricePerHour:F3}/hour (~${(estimate.Total.PricePerDay ?? estimate.Total.PricePerHour * 24):F2}/day)"
                            : "Estimate unavailable"
                    };
                    _estimatedCostText.Text = displayText;
                }
                else
                {
                    // Fallback to local calculation if API fails
                    UpdateCostEstimateLocal();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cost estimate error: {ex.Message}");
                // Fallback to local calculation
                UpdateCostEstimateLocal();
            }
        }

        private void UpdateCostEstimateLocal()
        {
            // Fallback local calculation if API fails
            double hourlyRate = 0.10; // Base rate
            
            if (_cpuComboBox.SelectedItem is ComboBoxItem cpuItem && cpuItem.Tag is string cpuValue)
            {
                var lowerValue = cpuValue.ToLowerInvariant();
                var lowerLabel = (cpuItem.Content?.ToString() ?? "").ToLowerInvariant();
                
                int cores = 2;
                if (lowerLabel.Contains("ultra") || lowerLabel.Contains("16")) cores = 16;
                else if (lowerLabel.Contains("pro") || lowerLabel.Contains("8")) cores = 8;
                else if (lowerLabel.Contains("standard") || lowerLabel.Contains("4")) cores = 4;
                
                hourlyRate = cores switch { 16 => 0.80, 8 => 0.40, 4 => 0.20, _ => 0.10 };
                
                if (lowerValue.Contains("gpu") || lowerLabel.Contains("gpu")) hourlyRate *= 2.0;
            }

            if (_storageComboBox.SelectedItem is ComboBoxItem storageItem && storageItem.Tag is string storageValue)
            {
                if (int.TryParse(storageValue, out var storageGB))
                {
                    hourlyRate += storageGB * 0.00005;
                }
            }

            var billingPlan = (_billingPlanComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "hourly";
            var displayText = billingPlan switch
            {
                "monthly" => $"Estimated: ~${hourlyRate * 24 * 30:F2}/month",
                "yearly" => $"Estimated: ~${hourlyRate * 24 * 365:F2}/year",
                _ => $"Estimated: ~${hourlyRate:F3}/hour"
            };
            _estimatedCostText.Text = displayText;
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
                var billingPlan = (_billingPlanComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "hourly";

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
