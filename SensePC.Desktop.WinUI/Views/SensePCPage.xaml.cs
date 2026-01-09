using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SensePC.Desktop.WinUI.Models;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace SensePC.Desktop.WinUI.Views
{
    public sealed partial class SensePCPage : Page
    {
        private readonly SensePCApiService _apiService;
        private List<PCInstance> _allPCs = new();
        private Dictionary<string, InstanceDetails> _pcDetails = new();
        private bool _isGridView = true;
        private DispatcherTimer? _pollingTimer;

        public SensePCPage()
        {
            this.InitializeComponent();
            _apiService = new SensePCApiService(new SecureStorage());
            Loaded += SensePCPage_Loaded;
            Unloaded += SensePCPage_Unloaded;
        }

        private async void SensePCPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadPCsAsync();
            StartPolling();
        }

        private void SensePCPage_Unloaded(object sender, RoutedEventArgs e)
        {
            StopPolling();
        }

        private void StartPolling()
        {
            _pollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _pollingTimer.Tick += async (s, e) => await LoadPCsAsync(showLoading: false);
            _pollingTimer.Start();
        }

        private void StopPolling()
        {
            _pollingTimer?.Stop();
            _pollingTimer = null;
        }

        private async Task LoadPCsAsync(bool showLoading = true)
        {
            try
            {
                if (showLoading)
                {
                    LoadingState.Visibility = Visibility.Visible;
                    EmptyState.Visibility = Visibility.Collapsed;
                    PCCardsPanel.Visibility = Visibility.Collapsed;
                }

                _allPCs = await _apiService.FetchPCsAsync();

                // Fetch real-time details
                if (_allPCs.Count > 0)
                {
                    var systemNames = _allPCs.Select(pc => pc.SystemName).ToList();
                    _pcDetails = await _apiService.GetInstanceDetailsAsync(systemNames);
                }

                UpdateUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadPCs error: {ex.Message}");
            }
            finally
            {
                LoadingState.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateUI()
        {
            var searchText = SearchBox.Text?.ToLower() ?? "";
            var filteredPCs = _allPCs.Where(pc =>
                string.IsNullOrEmpty(searchText) ||
                (pc.SystemName?.ToLower().Contains(searchText) ?? false) ||
                (pc.Region?.ToLower().Contains(searchText) ?? false) ||
                (pc.Description?.ToLower().Contains(searchText) ?? false)
            ).ToList();

            if (filteredPCs.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                PCCardsPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyState.Visibility = Visibility.Collapsed;
                PCCardsPanel.Visibility = Visibility.Visible;
                RenderPCCards(filteredPCs);
            }
        }

        private void RenderPCCards(List<PCInstance> pcs)
        {
            PCCardsPanel.Children.Clear();

            if (_isGridView)
            {
                // Grid layout - 3 columns
                var gridContainer = new Grid();
                gridContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                gridContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                gridContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                int row = 0;
                int col = 0;

                foreach (var pc in pcs)
                {
                    if (col == 0)
                    {
                        gridContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    }

                    var card = CreatePCCard(pc);
                    Grid.SetRow(card, row);
                    Grid.SetColumn(card, col);
                    gridContainer.Children.Add(card);

                    col++;
                    if (col >= 3)
                    {
                        col = 0;
                        row++;
                    }
                }

                PCCardsPanel.Children.Add(gridContainer);
            }
            else
            {
                // List layout - single column
                foreach (var pc in pcs)
                {
                    PCCardsPanel.Children.Add(CreatePCCardListView(pc));
                }
            }
        }

        private Border CreatePCCard(PCInstance pc)
        {
            var details = _pcDetails.GetValueOrDefault(pc.SystemName);

            // Main card container with subtle glass effect
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128)),
                Padding = new Thickness(16),
                Margin = new Thickness(6),
                MinHeight = 220
            };
            
            // Navigate to validation details on click
            card.PointerReleased += (s, e) => 
            {
                // Ensure clicks on buttons don't trigger navigation (bubbling handles this naturally mostly, but good to note)
                 Frame.Navigate(typeof(SensePCDetailsPage), pc);
            };

            var content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Spacer
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Info
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Actions

            // Header: Status Dot + Name + Badge + More Actions
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Status Dot
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Badge
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // More Actions

            // Status Dot
            var statusColor = GetStatusColor(pc.State);
            var statusDot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(statusColor),
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(statusDot, 0);
            header.Children.Add(statusDot);

            // Name - with text trimming for long names
            var nameText = new TextBlock
            {
                Text = pc.SystemName,
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(nameText, 1);
            header.Children.Add(nameText);

            // Badge - with margin for separation
            var statusBadge = CreateStatusBadge(pc.State);
            statusBadge.Margin = new Thickness(0, 0, 8, 0);
            statusBadge.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(statusBadge, 2);
            header.Children.Add(statusBadge);

            // More Actions
            var moreBtn = CreateMoreActionsButton(pc);
            Grid.SetColumn(moreBtn, 3);
            header.Children.Add(moreBtn);

            Grid.SetRow(header, 0);
            content.Children.Add(header);

            // Info Grid: Uptime | Region | Schedule
            var infoGrid = new Grid { Margin = new Thickness(0, 24, 0, 24) };
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // Left Column (Uptime, Region)
            var leftInfo = new StackPanel { Spacing = 12 };
            leftInfo.Children.Add(CreateCompactInfoItem("\uE823", details?.UptimeInfo?.FormatUptime() ?? "—"));
            leftInfo.Children.Add(CreateCompactInfoItem("\uE774", details?.Region ?? pc.Region ?? "—"));
            Grid.SetColumn(leftInfo, 0);
            infoGrid.Children.Add(leftInfo);

            // Right Column (Schedule, Idle)
            var rightInfo = new StackPanel { Spacing = 12 };
            rightInfo.Children.Add(CreateCompactInfoItem("\uE787", details?.Schedule?.FormatSchedule() ?? "No schedule"));
            var idleText = details?.IdleTimeout.HasValue == true ? $"{details.IdleTimeout}min" : "—";
            rightInfo.Children.Add(CreateCompactInfoItem("\uE708", idleText));
            Grid.SetColumn(rightInfo, 1);
            infoGrid.Children.Add(rightInfo);

            Grid.SetRow(infoGrid, 2);
            content.Children.Add(infoGrid);

            // Action Buttons Row
            var actionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            if (pc.IsRunning)
            {
                var connectBtn = CreateActionButton("Connect", "\uE8AB", "#5F6FFF", true);
                connectBtn.Tag = pc;
                connectBtn.Click += ConnectPC_Click;
                actionsPanel.Children.Add(connectBtn);

                var stopBtn = CreateActionButton("Stop", "\uE71A", "#FF4444", false);
                stopBtn.Tag = pc;
                stopBtn.Click += StopPC_Click;
                actionsPanel.Children.Add(stopBtn);
                
                var rebootBtn = CreateActionButton("", "\uE72C", "#FF8800", false);
                rebootBtn.Tag = pc;
                rebootBtn.Click += RebootPC_Click;
                actionsPanel.Children.Add(rebootBtn);
            }
            else if (pc.IsStopped)
            {
                var startBtn = CreateActionButton("Start", "\uE768", "#00CC66", true);
                startBtn.Tag = pc;
                startBtn.Click += StartPC_Click;
                actionsPanel.Children.Add(startBtn);
            }
            else
            {
                var busyPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                busyPanel.Children.Add(new ProgressRing { IsActive = true, Width = 16, Height = 16 });
                busyPanel.Children.Add(new TextBlock 
                { 
                    Text = pc.State?.ToUpper(), 
                    Foreground = new SolidColorBrush(ColorFromHex("#888899")),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                });
                actionsPanel.Children.Add(busyPanel);
            }

            Grid.SetRow(actionsPanel, 3);
            content.Children.Add(actionsPanel);

            card.Child = content;
            return card;
        }

        private Border CreatePCCardListView(PCInstance pc)
        {
            var details = _pcDetails.GetValueOrDefault(pc.SystemName);

            // Subtle card styling that works in light/dark mode
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128)),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Info
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Actions

            // Left side: Status Dot + Name + Badge + Uptime + Region
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, VerticalAlignment = VerticalAlignment.Center };

            // Status dot + Name
            var nameStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Spacing = 12 };
            var statusDot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(GetStatusColor(pc.State)),
                VerticalAlignment = VerticalAlignment.Center
            };
            nameStack.Children.Add(statusDot);
            
            nameStack.Children.Add(new TextBlock
            {
                Text = pc.SystemName,
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            leftStack.Children.Add(nameStack);

            // Status badge
            leftStack.Children.Add(CreateStatusBadge(pc.State));

            // Vertical Divider
            leftStack.Children.Add(new Rectangle { Width = 1, Height = 16, Fill = new SolidColorBrush(ColorFromHex("#333344")), VerticalAlignment = VerticalAlignment.Center });

            // Uptime
            var uptime = details?.UptimeInfo?.FormatUptime() ?? "—";
            leftStack.Children.Add(CreateCompactInfoItem("\uE823", uptime));

            // Region
            leftStack.Children.Add(CreateCompactInfoItem("\uE774", details?.Region ?? pc.Region ?? "—"));

            Grid.SetColumn(leftStack, 0);
            content.Children.Add(leftStack);

            // Right side: Actions
            var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };

            if (pc.IsRunning)
            {
                var connectBtn = CreateActionButton("Connect", "\uE8AB", "#5F6FFF", true);
                connectBtn.Tag = pc;
                connectBtn.Click += ConnectPC_Click;
                actionsPanel.Children.Add(connectBtn);

                var stopBtn = CreateActionButton("Stop", "\uE71A", "#FF4444", false);
                stopBtn.Tag = pc;
                stopBtn.Click += StopPC_Click;
                actionsPanel.Children.Add(stopBtn);

                var rebootBtn = CreateActionButton("Reboot", "\uE72C", "#FF8800", false);
                rebootBtn.Tag = pc;
                rebootBtn.Click += RebootPC_Click;
                actionsPanel.Children.Add(rebootBtn);
            }
            else if (pc.IsStopped)
            {
                var startBtn = CreateActionButton("Start", "\uE768", "#00CC66", true);
                startBtn.Tag = pc;
                startBtn.Click += StartPC_Click;
                actionsPanel.Children.Add(startBtn);
            }
            else
            {
                actionsPanel.Children.Add(new ProgressRing { IsActive = true, Width = 16, Height = 16 });
                actionsPanel.Children.Add(new TextBlock
                {
                    Text = pc.State?.ToUpper() ?? "BUSY",
                    Foreground = new SolidColorBrush(ColorFromHex("#888899")),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            // Add more actions menu button (always visible)
            actionsPanel.Children.Add(CreateMoreActionsButton(pc));

            Grid.SetColumn(actionsPanel, 1);
            content.Children.Add(actionsPanel);

            card.Child = content;
            return card;
        }

        private Border CreateStatusBadge(string? state)
        {
            // Use standard WinUI 3 friendly colors with alpha for background
            var (bgColor, fgColor, text) = state?.ToLower() switch
            {
                "running" => (Color.FromArgb(40, 16, 185, 129), Color.FromArgb(255, 16, 185, 129), "RUNNING"),
                "stopped" => (Color.FromArgb(40, 107, 114, 128), Color.FromArgb(255, 107, 114, 128), "STOPPED"),
                "pending" or "starting" => (Color.FromArgb(40, 59, 130, 246), Color.FromArgb(255, 59, 130, 246), "STARTING"),
                "stopping" => (Color.FromArgb(40, 245, 158, 11), Color.FromArgb(255, 245, 158, 11), "STOPPING"),
                "rebooting" => (Color.FromArgb(40, 249, 115, 22), Color.FromArgb(255, 249, 115, 22), "REBOOTING"),
                _ => (Color.FromArgb(40, 107, 114, 128), Color.FromArgb(255, 107, 114, 128), state?.ToUpper() ?? "UNKNOWN")
            };

            var badge = new Border
            {
                Background = new SolidColorBrush(bgColor),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 3, 8, 3)
            };

            badge.Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(fgColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            return badge;
        }

        private StackPanel CreateInfoItem(string glyph, string text, string tooltip) // Kept for compatibility if used elsewhere, but mainly replaced by compact
        {
             return CreateCompactInfoItem(glyph, text);
        }

        private StackPanel CreateCompactInfoItem(string glyph, string text)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new FontIcon
            {
                Glyph = glyph,
                FontSize = 13,
                Opacity = 0.5,
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 12,
                Opacity = 0.8,
                VerticalAlignment = VerticalAlignment.Center
            });
            return stack;
        }

        private Button CreateActionButton(string text, string glyph, string colorHex, bool isPrimary)
        {
            var btn = new Button
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 6, 10, 6)
            };

            var stack = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                Spacing = string.IsNullOrEmpty(text) ? 0 : 6,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (isPrimary && text == "Connect")
            {
                // Use accent color for primary Connect button
                var accentColor = Color.FromArgb(255, 99, 102, 241); // Indigo
                stack.Children.Add(new FontIcon { Glyph = glyph, FontSize = 12, Foreground = new SolidColorBrush(accentColor) });
                if (!string.IsNullOrEmpty(text))
                {
                    stack.Children.Add(new TextBlock { Text = text, FontSize = 12, Foreground = new SolidColorBrush(accentColor), VerticalAlignment = VerticalAlignment.Center });
                }
            }
            else if (isPrimary && text == "Start")
            {
                // Green for Start
                var greenColor = Color.FromArgb(255, 16, 185, 129);
                stack.Children.Add(new FontIcon { Glyph = glyph, FontSize = 12, Foreground = new SolidColorBrush(greenColor) });
                if (!string.IsNullOrEmpty(text))
                {
                    stack.Children.Add(new TextBlock { Text = text, FontSize = 12, Foreground = new SolidColorBrush(greenColor), VerticalAlignment = VerticalAlignment.Center });
                }
            }
            else
            {
                // Secondary buttons with muted color
                var accentColor = ColorFromHex(colorHex);
                stack.Children.Add(new FontIcon { Glyph = glyph, FontSize = 12, Foreground = new SolidColorBrush(accentColor) });
                if (!string.IsNullOrEmpty(text))
                {
                    stack.Children.Add(new TextBlock { Text = text, FontSize = 12, Foreground = new SolidColorBrush(accentColor), VerticalAlignment = VerticalAlignment.Center });
                }
            }

            btn.Content = stack;
            return btn;
        }

        private Color GetStatusColor(string? state) => state?.ToLower() switch
        {
            "running" => ColorFromHex("#00CC66"),
            "stopped" => ColorFromHex("#666680"),
            "pending" or "starting" => ColorFromHex("#5F9FFF"),
            "stopping" => ColorFromHex("#FFD700"),
            "rebooting" => ColorFromHex("#FF8800"),
            _ => ColorFromHex("#666680")
        };

        private static Color ColorFromHex(string hex)
        {
            hex = hex.Replace("#", "");
            return Color.FromArgb(255,
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));
        }

        // Event Handlers
        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => UpdateUI();

        private void GridView_Click(object sender, RoutedEventArgs e)
        {
            _isGridView = true;
            UpdateUI();
        }

        private void ListView_Click(object sender, RoutedEventArgs e)
        {
            _isGridView = false;
            UpdateUI();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadPCsAsync();
        }

        private void OpenWebDashboard_Click(object sender, RoutedEventArgs e)
        {
            var uri = new Uri("https://smartpc.cloud/dashboard/sense-pc");
            _ = Windows.System.Launcher.LaunchUriAsync(uri);
        }

        private async void StartPC_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PCInstance pc)
            {
                btn.IsEnabled = false;
                var result = await _apiService.StartVMAsync(pc.InstanceId);
                btn.IsEnabled = true;
                await LoadPCsAsync(showLoading: false);
            }
        }

        private async void StopPC_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PCInstance pc)
            {
                if (!pc.IsRunning) return;
                
                var dialog = new Dialogs.StopConfirmationDialog(pc, this.XamlRoot);
                await dialog.ShowAsync();
                
                if (dialog.StopConfirmed)
                {
                    await LoadPCsAsync(showLoading: false);
                }
            }
        }

        private async void RebootPC_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PCInstance pc)
            {
                if (!pc.IsRunning) return;
                
                var dialog = new Dialogs.RebootConfirmationDialog(pc, this.XamlRoot);
                await dialog.ShowAsync();
                
                if (dialog.RebootConfirmed)
                {
                    await LoadPCsAsync(showLoading: false);
                }
            }
        }

        private async void ConnectPC_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PCInstance pc)
            {
                if (!pc.IsRunning)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "PC Not Running",
                        Content = "Please start the PC first before connecting.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                    return;
                }

                btn.IsEnabled = false;
                try
                {
                    // Launch session to get token
                    var session = await _apiService.LaunchSessionAsync(pc.InstanceId);
                    
                    System.Diagnostics.Debug.WriteLine("=== Opening DCV Session ===");
                    System.Diagnostics.Debug.WriteLine($"PC: {pc.SystemName}");
                    System.Diagnostics.Debug.WriteLine($"DnsName: {session?.DnsName ?? "NULL"}");
                    System.Diagnostics.Debug.WriteLine($"SessionId: {session?.SessionId ?? "NULL"}");
                    
                    // Get the token
                    var token = session?.SessionToken ?? session?.AuthToken ?? "";
                    var dnsName = session?.DnsName ?? "";
                    var sessionId = session?.SessionId ?? "";

                    // Use MainWindow's session management instead of navigating to DCVSessionsPage
                    var mainWindow = App.MainWindow as MainWindow;

                    if (mainWindow != null)
                    {
                        await mainWindow.AddSessionAsync(
                            pc.InstanceId,
                            pc.SystemName,
                            dnsName,
                            token,
                            sessionId
                        );
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Connect error: {ex.Message}");
                    var dialog = new ContentDialog
                    {
                        Title = "Connection Error",
                        Content = $"Failed to connect: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
                finally
                {
                    btn.IsEnabled = true;
                }
            }
        }


        /// <summary>

        /// Attempts direct DCV connection via public IP address
        /// </summary>
        private async Task<(bool success, string errorMessage)> LaunchNativeDCVClientDirect(string publicIp, string? sessionId, string token)
        {
            try
            {
                // Find DCV client
                var possiblePaths = new[]
                {
                    @"C:\Program Files\NICE\DCV\Client\bin\dcvviewer.exe",
                    @"C:\Program Files (x86)\NICE\DCV\Client\bin\dcvviewer.exe"
                };

                string? dcvClientPath = null;
                foreach (var path in possiblePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        dcvClientPath = path;
                        break;
                    }
                }

                if (dcvClientPath == null)
                {
                    return (false, "DCV client not installed");
                }

                // Try different connection formats
                var dcvSessionId = sessionId ?? "console";
                
                // Try port 443 first (HTTPS), then 8443 (default DCV)
                var ports = new[] { 443, 8443 };
                
                foreach (var port in ports)
                {
                    // Format: hostname:port#session-id --auth-token=TOKEN
                    var server = port == 443 ? publicIp : $"{publicIp}:{port}";
                    var connectionString = $"{server}#{dcvSessionId}";
                    var arguments = $"{connectionString} --auth-token={token}";
                    
                    System.Diagnostics.Debug.WriteLine($"Trying DCV connection: {server}#{dcvSessionId}");
                    
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dcvClientPath,
                        Arguments = arguments,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };

                    var process = System.Diagnostics.Process.Start(startInfo);
                    
                    if (process != null)
                    {
                        await Task.Delay(1000); // Give it more time to connect
                        
                        if (!process.HasExited)
                        {
                            System.Diagnostics.Debug.WriteLine($"DCV connected successfully on port {port}");
                            return (true, "");
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"DCV client exited on port {port} with code: {process.ExitCode}");
                    }
                }

                return (false, "Failed to connect on all ports");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Direct DCV connection error: {ex.Message}");
                return (false, $"Error: {ex.Message}");
            }
        }


        /// <summary>
        /// Attempts to launch the native NICE DCV client application
        /// Returns (success, errorMessage)
        /// </summary>
        private async Task<(bool success, string errorMessage)> LaunchNativeDCVClient(string dcvUrl)
        {
            return await LaunchNativeDCVClientWithResponse(new Models.SessionLaunchResponse { DcvUrl = dcvUrl });
        }

        /// <summary>
        /// Launches native DCV client with full session response containing all connection details
        /// </summary>
        private async Task<(bool success, string errorMessage)> LaunchNativeDCVClientWithResponse(Models.SessionLaunchResponse session)
        {
            try
            {
                // Common DCV client installation paths
                var possiblePaths = new[]
                {
                    @"C:\Program Files\NICE\DCV\Client\bin\dcvviewer.exe",
                    @"C:\Program Files (x86)\NICE\DCV\Client\bin\dcvviewer.exe",
                    Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\NICE\DCV\Client\bin\dcvviewer.exe"),
                    Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\NICE\DCV\Client\bin\dcvviewer.exe")
                };

                string? dcvClientPath = null;
                foreach (var path in possiblePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        dcvClientPath = path;
                        System.Diagnostics.Debug.WriteLine($"Found DCV client at: {path}");
                        break;
                    }
                }

                if (dcvClientPath == null)
                {
                    return (false, "DCV client not installed");
                }

                // Build proper DCV connection arguments
                // Use SessionToken (preferred) or AuthToken as fallback
                var token = session.SessionToken ?? session.AuthToken;
                
                if (!string.IsNullOrEmpty(session.DnsName) && !string.IsNullOrEmpty(token))
                {
                    // DCV native client format: dcvviewer.exe hostname#session-id --auth-token=TOKEN
                    var sessionId = session.SessionId ?? "console";
                    var server = $"{session.DnsName}#{sessionId}";
                    var arguments = $"{server} --auth-token={token}";
                    
                    System.Diagnostics.Debug.WriteLine($"DCV Connection: Server={server}");
                    System.Diagnostics.Debug.WriteLine($"DCV Token: {token.Substring(0, Math.Min(20, token.Length))}...");
                    
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dcvClientPath,
                        Arguments = arguments,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };

                    System.Diagnostics.Debug.WriteLine($"Launching: {dcvClientPath} {arguments}");
                    
                    var process = System.Diagnostics.Process.Start(startInfo);
                    
                    if (process == null)
                    {
                        return (false, "Failed to start DCV client process");
                    }

                    // Wait a moment to check if process stays alive
                    await Task.Delay(500);
                    
                    if (process.HasExited)
                    {
                        return (false, $"DCV client exited immediately (exit code: {process.ExitCode})");
                    }

                    System.Diagnostics.Debug.WriteLine("DCV client launched successfully");
                    return (true, "");
                }
                else if (!string.IsNullOrEmpty(session.GatewayUrl))
                {
                    // Try gateway URL as fallback
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dcvClientPath,
                        Arguments = $"\"{session.GatewayUrl}\"",
                        UseShellExecute = true
                    };
                    
                    var process = System.Diagnostics.Process.Start(startInfo);
                    if (process != null)
                    {
                        await Task.Delay(500);
                        if (!process.HasExited)
                            return (true, "");
                    }
                    return (false, "Gateway URL connection failed");
                }
                else if (!string.IsNullOrEmpty(session.DcvUrl))
                {
                    // Fallback to dcvUrl
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dcvClientPath,
                        Arguments = $"\"{session.DcvUrl}\"",
                        UseShellExecute = true
                    };
                    
                    var process = System.Diagnostics.Process.Start(startInfo);
                    if (process != null)
                    {
                        await Task.Delay(500);
                        if (!process.HasExited)
                            return (true, "");
                    }
                    return (false, "DcvUrl connection failed");
                }
                else
                {
                    return (false, "No connection URL available from API");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DCV client error: {ex.Message}");
                return (false, $"Error: {ex.Message}");
            }
        }

        private async void BuildNew_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.BuildNewPCDialog(this.XamlRoot);
            await dialog.ShowAsync();
            
            if (dialog.PCCreated)
            {
                // Refresh PC list after creation
                await LoadPCsAsync();
            }
        }

        #region Dropdown Menu

        private Button CreateMoreActionsButton(PCInstance pc)
        {
            var btn = new Button
            {
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8),
                Content = new FontIcon
                {
                    Glyph = "\uE712", // More (three dots)
                    FontSize = 16,
                    Foreground = new SolidColorBrush(ColorFromHex("#888899"))
                }
            };

            var flyout = new MenuFlyout();

            // Schedule
            var scheduleItem = new MenuFlyoutItem
            {
                Text = "Schedule",
                Icon = new FontIcon { Glyph = "\uE787" }, // Calendar
                Tag = pc
            };
            scheduleItem.Click += ScheduleMenuItem_Click;
            flyout.Items.Add(scheduleItem);

            // Idle Settings
            var idleItem = new MenuFlyoutItem
            {
                Text = "Idle Settings",
                Icon = new FontIcon { Glyph = "\uEC46" }, // Moon
                Tag = pc
            };
            idleItem.Click += IdleSettingsMenuItem_Click;
            flyout.Items.Add(idleItem);

            // Reboot
            var rebootItem = new MenuFlyoutItem
            {
                Text = "Reboot",
                Icon = new FontIcon { Glyph = "\uE72C" }, // Refresh
                Tag = pc,
                IsEnabled = pc.IsRunning
            };
            rebootItem.Click += RebootMenuItem_Click;
            flyout.Items.Add(rebootItem);

            // PC Resize (only when stopped and hourly plan)
            var resizeItem = new MenuFlyoutItem
            {
                Text = "PC Resize",
                Icon = new FontIcon { Glyph = "\uE950" }, // CPU
                Tag = pc,
                IsEnabled = pc.IsStopped && pc.BillingPlan?.ToLower() == "hourly"
            };
            resizeItem.Click += ResizeMenuItem_Click;
            flyout.Items.Add(resizeItem);

            // Add Volume (SSD) - only when running and hourly plan
            var volumeItem = new MenuFlyoutItem
            {
                Text = "Add Volume (SSD)",
                Icon = new FontIcon { Glyph = "\uEDA2" }, // Hard drive
                Tag = pc,
                IsEnabled = pc.IsRunning && pc.BillingPlan?.ToLower() == "hourly"
            };
            volumeItem.Click += AddVolumeMenuItem_Click;
            flyout.Items.Add(volumeItem);

            flyout.Items.Add(new MenuFlyoutSeparator());

            // Assign User (admin feature - only when stopped)
            var assignItem = new MenuFlyoutItem
            {
                Text = "Assign User",
                Icon = new FontIcon { Glyph = "\uE77B" }, // Contact
                Tag = pc,
                IsEnabled = pc.IsStopped
            };
            assignItem.Click += AssignUserMenuItem_Click;
            flyout.Items.Add(assignItem);

            flyout.Items.Add(new MenuFlyoutSeparator());

            // Delete (destructive)
            var deleteItem = new MenuFlyoutItem
            {
                Text = "Delete",
                Icon = new FontIcon { Glyph = "\uE74D" }, // Delete
                Tag = pc,
                Foreground = new SolidColorBrush(ColorFromHex("#FF4444"))
            };
            deleteItem.Click += DeleteMenuItem_Click;
            flyout.Items.Add(deleteItem);

            btn.Flyout = flyout;
            ToolTipService.SetToolTip(btn, "More actions");

            return btn;
        }

        private async void ScheduleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is PCInstance pc)
            {
                var dialog = new Dialogs.ScheduleDialog(this.XamlRoot, pc, _apiService);
                await dialog.ShowAsync();
                
                if (dialog.ScheduleSaved)
                {
                    await LoadPCsAsync(); // Refresh to show updated schedule
                }
            }
        }

        private async void IdleSettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is PCInstance pc)
            {
                var dialog = new Dialogs.IdleSettingsDialog(this.XamlRoot, pc, _apiService);
                await dialog.ShowAsync();
                
                if (dialog.SettingsSaved)
                {
                    await LoadPCsAsync(); // Refresh to show updated idle settings
                }
            }
        }

        private async void RebootMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is PCInstance pc)
            {
                if (!pc.IsRunning)
                {
                    // PC must be running to reboot
                    return;
                }

                var dialog = new Dialogs.RebootConfirmationDialog(pc, this.XamlRoot);
                await dialog.ShowAsync();
                
                if (dialog.RebootConfirmed)
                {
                    await LoadPCsAsync(); // Refresh to show updated state
                }
            }
        }

        private async void ResizeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is PCInstance pc)
            {
                if (!pc.IsStopped)
                {
                    // PC must be stopped to resize
                    return;
                }

                var dialog = new Dialogs.PCResizeDialog(pc, this.XamlRoot);
                await dialog.ShowAsync();
                
                if (dialog.ResizeConfirmed)
                {
                    await LoadPCsAsync(); // Refresh to show updated config
                }
            }
        }

        private async void AddVolumeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is PCInstance pc)
            {
                if (!pc.IsRunning)
                {
                    // PC must be running to add storage
                    return;
                }

                // Get current storage from details if available
                int currentStorage = 128; // Default
                if (_pcDetails.TryGetValue(pc.InstanceId, out var details) && details.Specs != null)
                {
                    // Try to parse storage from specs (e.g., "256 GB" -> 256)
                    var storageStr = details.Specs.Storage?.Replace("GB", "").Replace("TB", "000").Trim();
                    if (int.TryParse(storageStr, out var parsed))
                    {
                        currentStorage = parsed;
                    }
                }

                var dialog = new Dialogs.AddVolumeDialog(pc, this.XamlRoot, currentStorage);
                await dialog.ShowAsync();
                
                if (dialog.VolumeIncreased)
                {
                    await LoadPCsAsync(); // Refresh to show updated storage
                }
            }
        }

        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is PCInstance pc)
            {
                var dialog = new Dialogs.DeletePCDialog(this.XamlRoot, pc, _apiService);
                await dialog.ShowAsync();
                
                if (dialog.PCDeleted)
                {
                    await LoadPCsAsync(); // Refresh to remove deleted PC
                }
            }
        }

        private async void AssignUserMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is PCInstance pc)
            {
                if (!pc.IsStopped)
                {
                    // PC must be stopped to assign
                    return;
                }

                var dialog = new Dialogs.AssignUserDialog(pc, this.XamlRoot);
                await dialog.ShowAsync();
                
                if (dialog.AssignmentChanged)
                {
                    await LoadPCsAsync(); // Refresh to show updated assignment
                }
            }
        }

        #endregion
    }
}
