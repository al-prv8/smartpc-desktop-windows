using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.Web.WebView2.Core;
using SensePC.Desktop.WinUI.Services;
using SensePC.Desktop.WinUI.Views;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Serilog;

namespace SensePC.Desktop.WinUI
{
    public sealed partial class MainWindow : Window
    {
        private readonly ISecureStorage _secureStorage;
        private string? _userEmail;
        
        // Session sidebar width
        private const double SESSION_SIDEBAR_WIDTH = 220;
        
        // Active sessions
        public ObservableCollection<SessionInfo> ActiveSessions { get; } = new();
        private SessionInfo? _selectedSession;
        private bool _webViewInitialized = false;
        private WebView2? _activeSessionWebView;
        
        public MainWindow()
        {
            this.InitializeComponent();
            
            // Enable Mica backdrop for Windows 11 style
            TrySetMicaBackdrop();
            
            // Extend content into title bar for modern look
            ExtendsContentIntoTitleBar = true;
            
            _secureStorage = new SecureStorage();
            
            // Bind sessions list (with null check for safety)
            if (SessionListView != null)
            {
                SessionListView.ItemsSource = ActiveSessions;
            }
            
            // Navigate to login page immediately
            ShowLoginPage();
        }


        private void TrySetMicaBackdrop()
        {
            try
            {
                if (MicaController.IsSupported())
                {
                    SystemBackdrop = new MicaBackdrop() 
                    { 
                        Kind = MicaKind.BaseAlt 
                    };
                }
                else if (DesktopAcrylicController.IsSupported())
                {
                    SystemBackdrop = new DesktopAcrylicBackdrop();
                }
            }
            catch (Exception ex)
            {
                // Backdrop not supported, continue with default
                System.Diagnostics.Debug.WriteLine($"Backdrop setup failed: {ex.Message}");
            }
        }


        #region Authentication

        public void ShowLoginPage()
        {
            try
            {
                Log.Information("Navigating to LoginPage");
                NavView.Visibility = Visibility.Collapsed;
                LoginFrame.Visibility = Visibility.Visible;
                LoginFrame.Navigate(typeof(Views.LoginPage));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error navigating to LoginPage");
            }
        }

        public void ShowDashboard(string? userEmail = null)
        {
            try
            {
                Log.Information("Navigating to Dashboard");
                _userEmail = userEmail;
                
                if (!string.IsNullOrEmpty(userEmail))
                {
                    var tempName = userEmail.Split('@')[0];
                    SidebarUserName.Text = tempName;
                }
                
                LoginFrame.Visibility = Visibility.Collapsed;
                NavView.Visibility = Visibility.Visible;
                
                // Select SensePC by default
                NavView.SelectedItem = NavSensePC;
                DashboardFrame.Navigate(typeof(Views.SensePCPage));
                
                // Fetch balance, user profile, and notifications count
                _ = FetchBalanceAsync();
                _ = FetchUserProfileAsync();
                _ = FetchNotificationCountAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error navigating to Dashboard");
            }
        }

        public void OnLoginSuccess(string? userEmail = null)
        {
            ShowDashboard(userEmail);
        }

        private async void HandleLogout()
        {
            try
            {
                await _secureStorage.RemoveAsync("id_token");
                await _secureStorage.RemoveAsync("access_token");
                await _secureStorage.RemoveAsync("refresh_token");
                await _secureStorage.RemoveAsync("user_id");
                await _secureStorage.RemoveAsync("user_email");
                
                // Clear all sessions
                ActiveSessions.Clear();
                HideSessionSidebar();
                
                Log.Information("User logged out");
                ShowLoginPage();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during logout");
            }
        }

        #endregion

        #region Data Fetching

        private async Task FetchBalanceAsync()
        {
            try
            {
                var apiService = new SensePCApiService(_secureStorage);
                var balance = await apiService.GetBalanceAsync();
                
                if (balance.HasValue)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        SidebarBalanceText.Text = $"${balance.Value:F2}";
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error fetching balance");
            }
        }

        private async Task FetchUserProfileAsync()
        {
            try
            {
                var apiService = new SensePCApiService(_secureStorage);
                var profile = await apiService.GetUserProfileAsync();
                
                if (profile != null)
                {
                    var fullName = $"{profile.FirstName} {profile.LastName}".Trim();
                    
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (!string.IsNullOrEmpty(fullName))
                        {
                            SidebarUserName.Text = fullName;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error fetching user profile");
            }
        }

        private async Task FetchNotificationCountAsync()
        {
            try
            {
                var idToken = await _secureStorage.GetAsync("id_token");
                if (string.IsNullOrEmpty(idToken)) return;

                using var httpClient = new System.Net.Http.HttpClient();
                var request = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Get, 
                    "https://yns7wkdio7.execute-api.us-east-1.amazonaws.com/dev/");
                request.Headers.Add("Authorization", idToken);

                var response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = System.Text.Json.JsonSerializer.Deserialize<NotificationsCountResponse>(content, 
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    var unreadCount = result?.Notifications?.Count(n => !n.IsRead) ?? 0;

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (unreadCount > 0)
                        {
                            NotificationBadge.Value = unreadCount;
                            NotificationBadge.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            NotificationBadge.Visibility = Visibility.Collapsed;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error fetching notification count");
            }
        }

        #endregion

        #region Navigation

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            NavView.SelectedItem = NavSensePC;
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                var tag = selectedItem.Tag?.ToString();
                
                // Hide DCV content when navigating to regular pages
                DCVSessionContent.Visibility = Visibility.Collapsed;
                DashboardFrame.Visibility = Visibility.Visible;
                
                switch (tag)
                {
                    case "SensePC":
                        DashboardFrame.Navigate(typeof(Views.SensePCPage));
                        break;
                    case "Storage":
                        DashboardFrame.Navigate(typeof(Views.StoragePage));
                        break;
                    case "Users":
                        DashboardFrame.Navigate(typeof(Views.UsersPage));
                        break;
                    case "Billing":
                        DashboardFrame.Navigate(typeof(Views.BillingPage));
                        break;
                    case "Support":
                        DashboardFrame.Navigate(typeof(Views.SupportPage));
                        break;
                    case "Tutorials":
                        DashboardFrame.Navigate(typeof(Views.TutorialsPage));
                        break;
                    case "Security":
                        // TODO: Implement
                        break;
                    case "Notifications":
                        DashboardFrame.Navigate(typeof(Views.NotificationsPage));
                        break;
                    case "Settings":
                        DashboardFrame.Navigate(typeof(Views.SettingsPage));
                        _ = FetchNotificationCountAsync(); // Refresh count when leaving notifications
                        break;
                    case "Profile":
                        DashboardFrame.Navigate(typeof(Views.ProfilePage));
                        _ = FetchNotificationCountAsync(); // Refresh count when leaving notifications
                        break;
                    case "Logout":
                        HandleLogout();
                        break;
                    default:
                        // Refresh notification count when navigating to any page other than Notifications
                        _ = FetchNotificationCountAsync();
                        break;
                }
            }
        }

        #endregion

        #region Session Sidebar

        private void CollapseSessionSidebar_Click(object sender, RoutedEventArgs e)
        {
            HideSessionSidebar();
            
            // Return to regular content view
            DCVSessionContent.Visibility = Visibility.Collapsed;
            DashboardFrame.Visibility = Visibility.Visible;
        }

        private void ShowSessionSidebar()
        {
            SessionSidebar.Visibility = Visibility.Visible;
            SessionSidebarColumn.Width = new GridLength(SESSION_SIDEBAR_WIDTH);
            
            // Collapse main navigation pane to compact mode (icons only)
            NavView.IsPaneOpen = false;
            
            // Add top margin to DCV content to leave space for window controls
            DCVSessionContent.Margin = new Thickness(0, 32, 0, 0);
            
            // Show DCV content area
            DashboardFrame.Visibility = Visibility.Collapsed;
            DCVSessionContent.Visibility = Visibility.Visible;
        }

        private void HideSessionSidebar()
        {
            SessionSidebar.Visibility = Visibility.Collapsed;
            SessionSidebarColumn.Width = new GridLength(0);
            
            // Remove top margin when not showing DCV
            DCVSessionContent.Margin = new Thickness(0);
        }





        #endregion

        #region Session Management

        /// <summary>
        /// Adds a new PC session and shows the session sidebar
        /// </summary>
        public async Task AddSessionAsync(string instanceId, string systemName, string dnsName, string sessionToken, string sessionId)
        {
            // Check if session already exists
            foreach (var session in ActiveSessions)
            {
                if (session.InstanceId == instanceId)
                {
                    // Select existing session
                    SessionListView.SelectedItem = session;
                    return;
                }
            }

            var sessionInfo = new SessionInfo
            {
                InstanceId = instanceId,
                SystemName = systemName,
                DnsName = dnsName,
                SessionToken = sessionToken,
                SessionId = sessionId
            };

            ActiveSessions.Add(sessionInfo);
            
            // Show session sidebar
            ShowSessionSidebar();
            
            // Select the new session
            SessionListView.SelectedItem = sessionInfo;
            
            // Initialize WebView and connect
            await InitializeWebViewAndConnectAsync(sessionInfo);
        }

        private void SessionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SessionListView.SelectedItem is SessionInfo session)
            {
                _selectedSession = session;
                _ = SwitchToSessionAsync(session);
            }
        }

        private async Task SwitchToSessionAsync(SessionInfo session)
        {
            NoSessionSelected.Visibility = Visibility.Collapsed;
            if (_activeSessionWebView != null)
            {
                _activeSessionWebView.Visibility = Visibility.Visible;
            }
            
            await InitializeWebViewAndConnectAsync(session);
        }

        private async Task InitializeWebViewAndConnectAsync(SessionInfo session)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Initializing WebView for {session.SystemName}");
                
                // Create WebView2 if not already created
                if (_activeSessionWebView == null)
                {
                    _activeSessionWebView = new WebView2
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Margin = new Thickness(0)
                    };
                    WebViewContainer.Children.Add(_activeSessionWebView);
                }
                
                if (!_webViewInitialized)
                {
                    await _activeSessionWebView.EnsureCoreWebView2Async();
                    _webViewInitialized = true;
                }

                var dcvGatewayUrl = "https://smartpc.cloud";
                
                System.Diagnostics.Debug.WriteLine($"Navigating to {dcvGatewayUrl}");

                bool hasInjected = false;

                // Remove existing handler if any
                _activeSessionWebView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                
                void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
                {
                    if (args.IsSuccess && !hasInjected)
                    {
                        hasInjected = true;
                        DispatcherQueue.TryEnqueue(async () =>
                        {
                            await Task.Delay(1500);
                            var script = BuildDCVViewerScript(session);
                            await _activeSessionWebView.CoreWebView2.ExecuteScriptAsync(script);
                            System.Diagnostics.Debug.WriteLine("DCV viewer script injected");
                        });
                    }
                }

                _activeSessionWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                _activeSessionWebView.CoreWebView2.Navigate(dcvGatewayUrl);

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing WebView: {ex.Message}");
            }
        }

        private string BuildDCVViewerScript(SessionInfo session)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("(function() {");
            sb.AppendLine("  console.log('SensePC Desktop: Initializing DCV viewer...');");
            sb.AppendLine("  document.body.innerHTML = '';");
            sb.AppendLine("  document.body.style.cssText = 'margin:0;padding:0;background:#000;width:100vw;height:100vh;overflow:hidden';");
            sb.AppendLine("  var container = document.createElement('div');");
            sb.AppendLine("  container.id = 'dcv-display';");
            sb.AppendLine("  container.style.cssText = 'width:100%;height:100%;position:absolute;top:0;left:0';");
            sb.AppendLine("  document.body.appendChild(container);");
            sb.AppendLine("  var overlay = document.createElement('div');");
            sb.AppendLine("  overlay.id = 'sensepc-loading';");
            sb.AppendLine("  overlay.style.cssText = 'position:absolute;top:50%;left:50%;transform:translate(-50%,-50%);text-align:center;color:#fff;font-family:sans-serif;z-index:1000';");
            sb.AppendLine("  overlay.innerHTML = '<h2>Connecting...</h2>';");
            sb.AppendLine("  document.body.appendChild(overlay);");
            sb.AppendLine("  var script = document.createElement('script');");
            sb.AppendLine("  script.type = 'module';");
            sb.AppendLine("  script.textContent = `");
            sb.AppendLine("    import dcv from '/dcvjs/dcv.js';");
            sb.AppendLine("    window.dcvModule = dcv;");
            sb.AppendLine("    console.log('DCV SDK loaded as module');");
            sb.AppendLine("    window.dispatchEvent(new Event('dcvLoaded'));");
            sb.AppendLine("  `;");
            sb.AppendLine("  document.head.appendChild(script);");
            sb.AppendLine("  window.addEventListener('dcvLoaded', function() {");
            sb.AppendLine("    var dcv = window.dcvModule;");
            sb.AppendLine("    if (!dcv) { overlay.innerHTML = '<h2 style=\"color:#f87171\">DCV SDK not loaded</h2>'; return; }");
            sb.AppendLine("    console.log('Connecting to DCV...');");
            sb.AppendLine("    dcv.connect({");
            sb.AppendLine($"      url: 'https://{EscapeJsString(session.DnsName)}',");
            sb.AppendLine($"      sessionId: '{EscapeJsString(session.SessionId)}',");
            sb.AppendLine($"      authToken: '{EscapeJsString(session.SessionToken)}',");
            sb.AppendLine("      divId: 'dcv-display',");
            sb.AppendLine("      useGateway: true,");
            sb.AppendLine("      assetsBaseUrl: '/dcvjs',");
            sb.AppendLine("      baseUrl: '/dcvjs',");
            sb.AppendLine($"      resourceBaseUrl: 'https://{EscapeJsString(session.DnsName)}',");
            sb.AppendLine("      callbacks: {");
            sb.AppendLine("        firstFrame: function() {");
            sb.AppendLine("          console.log('DCV: Connected');");
            sb.AppendLine("          var ol = document.getElementById('sensepc-loading');");
            sb.AppendLine("          if (ol) ol.style.display = 'none';");
            sb.AppendLine("        },");
            sb.AppendLine("        disconnect: function(r) {");
            sb.AppendLine("          console.log('DCV: Disconnected', r);");
            sb.AppendLine("          var ol = document.getElementById('sensepc-loading');");
            sb.AppendLine("          if (ol) { ol.style.display = 'block'; ol.innerHTML = '<h2 style=\"color:#f87171\">Disconnected</h2>'; }");
            sb.AppendLine("        }");
            sb.AppendLine("      }");
            sb.AppendLine("    }).then(function(conn) {");
            sb.AppendLine("      window.addEventListener('resize', function() {");
            sb.AppendLine("        try { conn.requestResolution(window.innerWidth, window.innerHeight); } catch(e) {}");
            sb.AppendLine("      });");
            sb.AppendLine("    }).catch(function(e) {");
            sb.AppendLine("      console.error('DCV connection error:', e);");
            sb.AppendLine("      var ol = document.getElementById('sensepc-loading');");
            sb.AppendLine("      if (ol) ol.innerHTML = '<h2 style=\"color:#f87171\">Connection Failed</h2><p>' + (e.message || e) + '</p>';");
            sb.AppendLine("    });");
            sb.AppendLine("  });");
            sb.AppendLine("})();");
            
            return sb.ToString();
        }

        private static string EscapeJsString(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "");
        }

        private void CloseSession_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string instanceId)
            {
                CloseSession(instanceId);
            }
        }

        private void CloseSession(string instanceId)
        {
            SessionInfo? toRemove = null;
            foreach (var session in ActiveSessions)
            {
                if (session.InstanceId == instanceId)
                {
                    toRemove = session;
                    break;
                }
            }

            if (toRemove != null)
            {
                ActiveSessions.Remove(toRemove);
                
                if (ActiveSessions.Count == 0)
                {
                    HideSessionSidebar();
                    DCVSessionContent.Visibility = Visibility.Collapsed;
                    DashboardFrame.Visibility = Visibility.Visible;
                }
                else if (_selectedSession == toRemove && ActiveSessions.Count > 0)
                {
                    SessionListView.SelectedItem = ActiveSessions[0];
                }
            }
        }

        private void BackToPCList_Click(object sender, RoutedEventArgs e)
        {
            // Hide session view and show PC list
            DCVSessionContent.Visibility = Visibility.Collapsed;
            DashboardFrame.Visibility = Visibility.Visible;
            
            // Navigate to SensePC page
            NavView.SelectedItem = NavSensePC;
            DashboardFrame.Navigate(typeof(Views.SensePCPage));
        }

        #endregion
    }

    /// <summary>
    /// Session information for the sidebar list
    /// </summary>
    public class SessionInfo
    {
        public string InstanceId { get; set; } = "";
        public string SystemName { get; set; } = "";
        public string DnsName { get; set; } = "";
        public string SessionToken { get; set; } = "";
        public string SessionId { get; set; } = "";
    }

    /// <summary>
    /// Response model for notification count
    /// </summary>
    public class NotificationsCountResponse
    {
        public List<NotificationCountItem>? Notifications { get; set; }
    }

    public class NotificationCountItem
    {
        public bool IsRead { get; set; }
    }
}
