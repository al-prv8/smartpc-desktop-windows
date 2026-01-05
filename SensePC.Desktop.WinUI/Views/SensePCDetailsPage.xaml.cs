using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SensePC.Desktop.WinUI.Models;
using SensePC.Desktop.WinUI.Services;

namespace SensePC.Desktop.WinUI.Views
{
    public sealed partial class SensePCDetailsPage : Page
    {
        private PCInstance? _pc;
        private InstanceDetails? _details;
        private DispatcherTimer? _metricsTimer;
        private SensePCApiService? _apiService;
        private bool _isRunning = false;

        public SensePCDetailsPage()
        {
            this.InitializeComponent();
            _apiService = new SensePCApiService(new SecureStorage());
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is PCInstance pc)
            {
                _pc = pc;
                InstanceIdText.Text = _pc.InstanceId ?? "Unknown";
                PcNameText.Text = _pc.SystemName ?? "PC Details";
                _isRunning = _pc.State?.ToLower() == "running";
                UpdateStatusUI();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _metricsTimer?.Stop();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDetailsAsync();
            StartMetricsRefresh();
        }

        private async Task LoadDetailsAsync()
        {
            if (_pc == null) return;
            
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                
                var detailsDict = await _apiService.GetInstanceDetailsAsync(new List<string> { _pc.SystemName });
                if (detailsDict.TryGetValue(_pc.SystemName, out var detail))
                {
                    _details = detail;
                    UpdateDetailsUI();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load details: {ex.Message}");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateDetailsUI()
        {
            if (_details == null) return;

            // Computer Metrics
            CpuText.Text = _details.CpuUsage ?? "0.00%";
            MemoryText.Text = _details.MemoryUsage ?? "0.00%";
            RegionText.Text = _details.Region ?? "—";
            UptimeText.Text = _details.Uptime ?? "N/A";
            CostText.Text = _details.MonthlyBillingTotal != null 
                ? $"${_details.MonthlyBillingTotal:F2} this month" 
                : "$0.00 this month";

            // Specifications
            var specs = _details.Specs;
            if (specs != null)
            {
                CpuCoresText.Text = specs.Cpu ?? "— Core";
                MemorySpecText.Text = specs.Ram ?? "—";
                StorageText.Text = specs.Storage ?? "—";
                GpuText.Text = specs.Gpu ?? "None";
                OsText.Text = specs.Os ?? "—";
            }

            // Assigned User
            if (!string.IsNullOrEmpty(_details.AssignedUser))
            {
                AssignedUserText.Text = _details.AssignedUser;
                AssignUserBtn.Content = CreateButtonContent("\uE70F", "Unassign");
            }
            else
            {
                AssignedUserText.Text = "No user assigned to this PC.";
                AssignUserBtn.Content = CreateButtonContent("\uE77B", "Assign user");
            }

            // Billing Plan
            BillingPlanText.Text = _details.BillingPlan ?? "Hourly";
            BillingPlanDescription.Text = _details.BillingPlanDescription ?? 
                "Unlimited usage — pay per hour while running. Great for quick tasks, testing, and flexible start/stop.";

            // Schedule
            if (_details.Schedule != null && (_details.Schedule.AutoStartTime != null || _details.Schedule.AutoStopTime != null))
            {
                var start = _details.Schedule.AutoStartTime ?? "—";
                var stop = _details.Schedule.AutoStopTime ?? "—";
                ScheduleText.Text = $"Start: {start}, Stop: {stop}";
            }
            else
            {
                ScheduleText.Text = "Not configured";
            }

            // Idle
            if (_details.IdleTimeout.HasValue && _details.IdleTimeout.Value > 0)
            {
                IdleText.Text = $"{_details.IdleTimeout.Value} minutes";
            }
            else
            {
                IdleText.Text = "Disabled";
            }
        }

        private StackPanel CreateButtonContent(string glyph, string text)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            stack.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14 });
            stack.Children.Add(new TextBlock { Text = text });
            return stack;
        }

        private void UpdateStatusUI()
        {
            if (_isRunning)
            {
                StatusBadge.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(30, 16, 185, 129));
                StatusText.Text = "Running";
                StatusText.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129));
                
                StartStopIcon.Glyph = "\uE71A";
                StartStopText.Text = "Stop";
                ConnectBtn.IsEnabled = true;
                RebootBtn.IsEnabled = true;
                ResizeBtn.IsEnabled = false;
            }
            else
            {
                StatusBadge.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(30, 150, 150, 150));
                StatusText.Text = "Stopped";
                StatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray);
                
                StartStopIcon.Glyph = "\uE768";
                StartStopText.Text = "Start";
                ConnectBtn.IsEnabled = false;
                RebootBtn.IsEnabled = false;
                ResizeBtn.IsEnabled = true;
            }
        }

        private void StartMetricsRefresh()
        {
            _metricsTimer = new DispatcherTimer();
            _metricsTimer.Interval = TimeSpan.FromSeconds(15);
            _metricsTimer.Tick += MetricsTimer_Tick;
            _metricsTimer.Start();
        }

        private async void MetricsTimer_Tick(object sender, object e)
        {
            await RefreshDetailsAsync();
        }

        private async Task RefreshDetailsAsync()
        {
            if (_pc == null) return;
            
            try
            {
                var detailsDict = await _apiService.GetInstanceDetailsAsync(new List<string> { _pc.SystemName });
                if (detailsDict.TryGetValue(_pc.SystemName, out var detail))
                {
                    _details = detail;
                    UpdateDetailsUI();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to refresh details: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(SensePCPage));
        }

        private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_pc == null) return;
            
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                var result = await _apiService.LaunchSessionAsync(_pc.InstanceId);
                if (result != null && !string.IsNullOrEmpty(result.DcvUrl))
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(result.DcvUrl));
                }
                else if (result != null && !string.IsNullOrEmpty(result.GatewayUrl))
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(result.GatewayUrl));
                }
                else
                {
                    await ShowDialogAsync("Connect", "Unable to get connection URL. Please try again.");
                }
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Error", $"Failed to connect: {ex.Message}");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void StartStopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_pc == null) return;
            
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                
                if (_isRunning)
                {
                    var result = await _apiService.StopVMAsync(_pc.InstanceId);
                    if (result != null)
                    {
                        _isRunning = false;
                        UpdateStatusUI();
                    }
                }
                else
                {
                    var result = await _apiService.StartVMAsync(_pc.InstanceId);
                    if (result != null)
                    {
                        _isRunning = true;
                        UpdateStatusUI();
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Error", $"Failed to {(_isRunning ? "stop" : "start")} PC: {ex.Message}");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void RebootBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_pc == null || !_isRunning) return;
            
            var dialog = new ContentDialog
            {
                Title = "Reboot PC",
                Content = "Are you sure you want to reboot this PC?",
                PrimaryButtonText = "Reboot",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };
            
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                    await _apiService.RestartVMAsync(_pc.InstanceId);
                }
                catch (Exception ex)
                {
                    await ShowDialogAsync("Error", $"Failed to reboot: {ex.Message}");
                }
                finally
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void MoreOptionsBtn_Click(object sender, RoutedEventArgs e)
        {
            // Show flyout with additional options
            var flyout = new MenuFlyout();
            flyout.Items.Add(new MenuFlyoutItem { Text = "Schedule", Icon = new FontIcon { Glyph = "\uE787" } });
            flyout.Items.Add(new MenuFlyoutItem { Text = "Idle Timeout", Icon = new FontIcon { Glyph = "\uE916" } });
            flyout.Items.Add(new MenuFlyoutItem { Text = "Resize", Icon = new FontIcon { Glyph = "\uE740" } });
            flyout.Items.Add(new MenuFlyoutSeparator());
            flyout.Items.Add(new MenuFlyoutItem { Text = "Delete", Icon = new FontIcon { Glyph = "\uE74D", Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red) } });
            
            flyout.ShowAt(sender as Button);
        }

        private async void AssignUser_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement assign user dialog
            await ShowDialogAsync("Assign User", "User assignment dialog will be implemented.");
        }

        private async void ManagePlan_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to billing or show plan selection
            await ShowDialogAsync("Manage Plan", "Plan management will be implemented.");
        }

        private async void ConfigureSchedule_Click(object sender, RoutedEventArgs e)
        {
            // Show schedule configuration dialog
            var dialog = new ContentDialog
            {
                Title = "Configure Schedule",
                XamlRoot = this.XamlRoot,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel"
            };
            
            var content = new StackPanel { Spacing = 16 };
            var toggleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            toggleStack.Children.Add(new TextBlock { Text = "Enable Schedule", VerticalAlignment = VerticalAlignment.Center });
            toggleStack.Children.Add(new ToggleSwitch());
            content.Children.Add(toggleStack);
            
            var startStack = new StackPanel { Spacing = 4 };
            startStack.Children.Add(new TextBlock { Text = "Auto Start Time" });
            startStack.Children.Add(new TimePicker { ClockIdentifier = "12HourClock" });
            content.Children.Add(startStack);
            
            var stopStack = new StackPanel { Spacing = 4 };
            stopStack.Children.Add(new TextBlock { Text = "Auto Stop Time" });
            stopStack.Children.Add(new TimePicker { ClockIdentifier = "12HourClock" });
            content.Children.Add(stopStack);
            
            dialog.Content = content;
            await dialog.ShowAsync();
        }

        private async void ConfigureIdle_Click(object sender, RoutedEventArgs e)
        {
            var combo = new ComboBox { Width = 200 };
            combo.Items.Add(new ComboBoxItem { Content = "Disabled", Tag = 0 });
            combo.Items.Add(new ComboBoxItem { Content = "15 minutes", Tag = 15 });
            combo.Items.Add(new ComboBoxItem { Content = "30 minutes", Tag = 30 });
            combo.Items.Add(new ComboBoxItem { Content = "1 hour", Tag = 60 });
            combo.Items.Add(new ComboBoxItem { Content = "2 hours", Tag = 120 });
            combo.SelectedIndex = 2; // Default 30 min
            
            var dialog = new ContentDialog
            {
                Title = "Idle Timeout",
                Content = combo,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };
            
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && _pc != null)
            {
                var selected = combo.SelectedItem as ComboBoxItem;
                var timeout = int.Parse(selected?.Tag?.ToString() ?? "30");
                
                try
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                    await _apiService.SaveIdleTimeoutAsync(_pc.InstanceId, timeout);
                    IdleText.Text = timeout > 0 ? $"{timeout} minutes" : "Disabled";
                }
                catch (Exception ex)
                {
                    await ShowDialogAsync("Error", $"Failed to save: {ex.Message}");
                }
                finally
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void ConfigureResize_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                await ShowDialogAsync("Cannot Resize", "Please stop the PC before resizing.");
                return;
            }
            
            // TODO: Implement resize dialog with config options
            await ShowDialogAsync("Resize PC", "Resize configuration will be implemented.");
        }

        private async void DeletePC_Click(object sender, RoutedEventArgs e)
        {
            if (_pc == null) return;
            
            var dialog = new ContentDialog
            {
                Title = "Delete PC",
                Content = $"Are you sure you want to permanently delete '{_pc.SystemName}'?\n\nThis action cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };
            
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                    var success = await _apiService.DeleteVMAsync(_pc.InstanceId, _details?.Region ?? "");
                    if (success)
                    {
                        Frame.Navigate(typeof(SensePCPage));
                    }
                }
                catch (Exception ex)
                {
                    await ShowDialogAsync("Error", $"Failed to delete: {ex.Message}");
                }
                finally
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async Task ShowDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
