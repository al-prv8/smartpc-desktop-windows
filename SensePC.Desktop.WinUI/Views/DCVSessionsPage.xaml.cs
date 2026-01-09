using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SensePC.Desktop.WinUI.Views
{
    /// <summary>
    /// Multi-session DCV viewer page with tabbed interface
    /// </summary>
    public sealed partial class DCVSessionsPage : Page
    {
        // Track active sessions
        private readonly Dictionary<string, DCVSessionInfo> _activeSessions = new();
        
        // Static instance for adding sessions from other pages
        private static DCVSessionsPage? _instance;
        public static DCVSessionsPage? Instance => _instance;

        // Secure storage for getting auth tokens
        private readonly ISecureStorage _secureStorage;

        public DCVSessionsPage()
        {
            this.InitializeComponent();
            _instance = this;
            _secureStorage = new SecureStorage();
            UpdateEmptyState();
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            // Check if we're opening a new session
            if (e.Parameter is DCVSessionRequest request)
            {
                OpenSession(request.InstanceId, request.SystemName, request.DnsName, request.SessionToken, request.SessionId);
            }
        }

        /// <summary>
        /// Opens a new DCV session tab
        /// </summary>
        public void OpenSession(string instanceId, string systemName, string dnsName, string sessionToken, string sessionId)
        {
            // Check if session already exists
            if (_activeSessions.ContainsKey(instanceId))
            {
                // Switch to existing tab
                foreach (TabViewItem tab in SessionTabView.TabItems)
                {
                    if (tab.Tag is string id && id == instanceId)
                    {
                        SessionTabView.SelectedItem = tab;
                        return;
                    }
                }
            }

            // Create WebView2 for this session - ensure full bleed (no gaps)
            var webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0)
            };

            // Create tab with modern WinUI 3 styling
            var tabItem = new TabViewItem
            {
                Header = systemName,
                IconSource = new FontIconSource { Glyph = "\uE7F4" }, // Computer icon
                Content = webView,
                Tag = instanceId,
                IsClosable = true,
                Padding = new Thickness(0) // No padding around content
            };


            // Add to tabs
            SessionTabView.TabItems.Add(tabItem);
            SessionTabView.SelectedItem = tabItem;

            // Track session
            var sessionInfo = new DCVSessionInfo
            {
                InstanceId = instanceId,
                SystemName = systemName,
                DnsName = dnsName,
                SessionToken = sessionToken,
                SessionId = sessionId,
                WebView = webView,
                TabItem = tabItem
            };
            _activeSessions[instanceId] = sessionInfo;

            // Initialize WebView2 and load DCV
            InitializeWebViewAsync(webView, sessionInfo);
            
            UpdateEmptyState();
            UpdateConnectionStatus();
        }

        private async void InitializeWebViewAsync(WebView2 webView, DCVSessionInfo session)
        {
            try
            {
                await webView.EnsureCoreWebView2Async();

                // Handle HTTP Basic Authentication for dev site
                webView.CoreWebView2.BasicAuthenticationRequested += (sender, args) =>
                {
                    args.Response.UserName = "smartpc";
                    args.Response.Password = "Smart2025!";
                    System.Diagnostics.Debug.WriteLine("Basic auth provided for dev site");
                };

                // Set user agent to match regular browser
                webView.CoreWebView2.Settings.UserAgent = 
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

                // Enable features needed for DCV
                webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                webView.CoreWebView2.Settings.IsScriptEnabled = true;
                webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;

                
                // Get auth tokens from secure storage
                var accessToken = await _secureStorage.GetAsync("access_token");
                var idToken = await _secureStorage.GetAsync("id_token");
                var refreshToken = await _secureStorage.GetAsync("refresh_token");
                var userId = await _secureStorage.GetAsync("user_id"); // Cognito sub
                
                System.Diagnostics.Debug.WriteLine($"Auth tokens - Access: {!string.IsNullOrEmpty(accessToken)}, ID: {!string.IsNullOrEmpty(idToken)}, UserId: {userId ?? "NULL"}");

                // Set cookies for authentication
                var cookieManager = webView.CoreWebView2.CookieManager;
                
                // Cognito Client ID - same as used in the app
                const string cognitoClientId = "2lknj90rkjmtkcnph06q6r93ug";
                
                // Set the auth.state cookie that the website uses
                if (!string.IsNullOrEmpty(idToken))
                {
                    // Format: {"isAuthenticated":true,"token":"jwt-token"}
                    var authStateValue = System.Text.Json.JsonSerializer.Serialize(new 
                    { 
                        isAuthenticated = true, 
                        token = idToken 
                    });
                    
                    var authStateCookie = cookieManager.CreateCookie("auth.state", authStateValue, "smartpc.cloud", "/");
                    authStateCookie.IsSecure = true;
                    authStateCookie.IsHttpOnly = false;
                    cookieManager.AddOrUpdateCookie(authStateCookie);
                    
                    System.Diagnostics.Debug.WriteLine("Set auth.state cookie");
                }
                
                // Set Cognito cookies - the middleware checks for these
                var userKey = !string.IsNullOrEmpty(userId) ? userId : "user";
                
                if (!string.IsNullOrEmpty(accessToken))
                {
                    // CognitoIdentityServiceProvider.{clientId}.{userId}.accessToken
                    var cognitoAccessCookie = cookieManager.CreateCookie(
                        $"CognitoIdentityServiceProvider.{cognitoClientId}.{userKey}.accessToken", 
                        accessToken, 
                        "smartpc.cloud", 
                        "/");
                    cognitoAccessCookie.IsSecure = true;
                    cognitoAccessCookie.IsHttpOnly = false;
                    cookieManager.AddOrUpdateCookie(cognitoAccessCookie);
                    System.Diagnostics.Debug.WriteLine("Set Cognito accessToken cookie");
                }
                
                if (!string.IsNullOrEmpty(idToken))
                {
                    // CognitoIdentityServiceProvider.{clientId}.{userId}.idToken
                    var cognitoIdCookie = cookieManager.CreateCookie(
                        $"CognitoIdentityServiceProvider.{cognitoClientId}.{userKey}.idToken", 
                        idToken, 
                        "smartpc.cloud", 
                        "/");
                    cognitoIdCookie.IsSecure = true;
                    cognitoIdCookie.IsHttpOnly = false;
                    cookieManager.AddOrUpdateCookie(cognitoIdCookie);
                    System.Diagnostics.Debug.WriteLine("Set Cognito idToken cookie");
                }
                
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    // CognitoIdentityServiceProvider.{clientId}.{userId}.refreshToken
                    var cognitoRefreshCookie = cookieManager.CreateCookie(
                        $"CognitoIdentityServiceProvider.{cognitoClientId}.{userKey}.refreshToken", 
                        refreshToken, 
                        "smartpc.cloud", 
                        "/");
                    cognitoRefreshCookie.IsSecure = true;
                    cognitoRefreshCookie.IsHttpOnly = false;
                    cookieManager.AddOrUpdateCookie(cognitoRefreshCookie);
                }
                
                // Set LastAuthUser cookie
                if (!string.IsNullOrEmpty(userKey))
                {
                    var lastAuthUserCookie = cookieManager.CreateCookie(
                        $"CognitoIdentityServiceProvider.{cognitoClientId}.LastAuthUser", 
                        userKey, 
                        "smartpc.cloud", 
                        "/");
                    lastAuthUserCookie.IsSecure = true;
                    lastAuthUserCookie.IsHttpOnly = false;
                    cookieManager.AddOrUpdateCookie(lastAuthUserCookie);
                }


                // Strategy: Navigate to the main smartpc.cloud website first to get proper origin
                // and access to the DCV SDK at /dcvjs/dcv.js, then inject the connection code
                // Note: session.DnsName (e.g. mypc.smartpc.cloud) is just the PC's DNS, not a web server
                var dcvGatewayUrl = "https://smartpc.cloud";
                
                System.Diagnostics.Debug.WriteLine($"Loading DCV viewer for {session.SystemName}");
                System.Diagnostics.Debug.WriteLine($"SDK URL: {dcvGatewayUrl}");
                System.Diagnostics.Debug.WriteLine($"DCV Server: {session.DnsName}");
                System.Diagnostics.Debug.WriteLine($"Session ID: {session.SessionId}");

                
                bool hasInjected = false;
                
                webView.CoreWebView2.NavigationCompleted += async (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation completed. Success: {e.IsSuccess}, Status: {e.WebErrorStatus}");
                    
                    if (e.IsSuccess && !hasInjected)
                    {
                        hasInjected = true;
                        
                        // Wait for React to hydrate before taking over the page
                        System.Diagnostics.Debug.WriteLine("Waiting for page to fully load...");
                        await Task.Delay(1500);
                        
                        System.Diagnostics.Debug.WriteLine("Injecting DCV viewer...");
                        
                        // Inject the DCV viewer HTML/JS after navigation
                        var script = BuildDCVViewerScript(session);
                        await webView.CoreWebView2.ExecuteScriptAsync(script);
                        
                        session.TabItem.Header = $"● {session.SystemName}";
                        System.Diagnostics.Debug.WriteLine("DCV viewer injected");
                    }

                    else if (!e.IsSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine($"Navigation failed: {e.WebErrorStatus}");
                    }
                };
                
                // Navigate to the DCV gateway - this gives us proper origin for SDK
                webView.CoreWebView2.Navigate(dcvGatewayUrl);



            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 initialization error: {ex.Message}");
                ShowErrorInTab(session, ex.Message);
            }
        }

        /// <summary>
        /// Builds JavaScript to inject into the page that will load the DCV SDK and connect
        /// </summary>
        private string BuildDCVViewerScript(DCVSessionInfo session)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("(function() {");
            sb.AppendLine("  console.log('SensePC Desktop: Initializing DCV viewer...');");
            sb.AppendLine("  ");
            sb.AppendLine("  // Create DCV container");
            sb.AppendLine("  document.body.innerHTML = '';");
            sb.AppendLine("  document.body.style.cssText = 'margin:0;padding:0;background:#000;width:100vw;height:100vh;overflow:hidden';");
            sb.AppendLine("  var container = document.createElement('div');");
            sb.AppendLine("  container.id = 'dcv-display';");
            sb.AppendLine("  container.style.cssText = 'width:100%;height:100%;position:absolute;top:0;left:0';");
            sb.AppendLine("  document.body.appendChild(container);");
            sb.AppendLine("  ");
            sb.AppendLine("  // Add loading overlay");
            sb.AppendLine("  var overlay = document.createElement('div');");
            sb.AppendLine("  overlay.id = 'sensepc-loading';");
            sb.AppendLine("  overlay.style.cssText = 'position:absolute;top:50%;left:50%;transform:translate(-50%,-50%);text-align:center;color:#fff;font-family:sans-serif;z-index:1000';");
            sb.AppendLine("  overlay.innerHTML = '<h2>Connecting...</h2>';");
            sb.AppendLine("  document.body.appendChild(overlay);");
            sb.AppendLine("  ");
            sb.AppendLine("  // Load DCV SDK as ES module");
            sb.AppendLine("  var script = document.createElement('script');");
            sb.AppendLine("  script.type = 'module';");
            sb.AppendLine("  script.textContent = `");
            sb.AppendLine("    import dcv from '/dcvjs/dcv.js';");
            sb.AppendLine("    window.dcvModule = dcv;");
            sb.AppendLine("    console.log('DCV SDK loaded as module');");
            sb.AppendLine("    window.dispatchEvent(new Event('dcvLoaded'));");
            sb.AppendLine("  `;");
            sb.AppendLine("  document.head.appendChild(script);");
            sb.AppendLine("  ");
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
            sb.AppendLine("          console.log('DCV: Connected - first frame received');");
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
            sb.AppendLine("  });");  // Close the dcvLoaded event listener
            sb.AppendLine("})();");

            
            return sb.ToString();
        }



        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
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

        private void ShowErrorInTab(DCVSessionInfo session, string errorMessage)
        {
            var errorPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            errorPanel.Children.Add(new FontIcon { Glyph = "\uE783", FontSize = 48 });
            errorPanel.Children.Add(new TextBlock 
            { 
                Text = "Failed to load DCV session", 
                FontSize = 16, 
                Margin = new Thickness(0, 12, 0, 4) 
            });
            errorPanel.Children.Add(new TextBlock 
            { 
                Text = errorMessage, 
                FontSize = 12, 
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) 
            });
            
            if (session.TabItem != null)
            {
                session.TabItem.Content = errorPanel;
            }
        }

        private void SessionTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            CloseTab(args.Tab);
        }

        private void CloseTab(TabViewItem tab)
        {
            if (tab.Tag is string instanceId)
            {
                if (_activeSessions.TryGetValue(instanceId, out var session))
                {
                    // Clean up WebView
                    session.WebView?.Close();
                    _activeSessions.Remove(instanceId);
                }
            }

            SessionTabView.TabItems.Remove(tab);
            UpdateEmptyState();
            UpdateConnectionStatus();
        }

        private void SessionTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Can be used for focus management
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate back to PC list
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(SensePCPage));
            }
        }

        private void UpdateEmptyState()
        {
            bool hasSessions = SessionTabView.TabItems.Count > 0;
            EmptyStatePanel.Visibility = hasSessions ? Visibility.Collapsed : Visibility.Visible;
            SessionTabView.Visibility = hasSessions ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateConnectionStatus()
        {
            int count = _activeSessions.Count;
            ConnectionStatusText.Text = count switch
            {
                0 => "No sessions",
                1 => "1 session active",
                _ => $"{count} sessions active"
            };
        }

        /// <summary>
        /// Check if a session is already open
        /// </summary>
        public bool HasSession(string instanceId)
        {
            return _activeSessions.ContainsKey(instanceId);
        }

        /// <summary>
        /// Get the number of active sessions
        /// </summary>
        public int SessionCount => _activeSessions.Count;
    }

    /// <summary>
    /// Session information container
    /// </summary>
    public class DCVSessionInfo
    {
        public string InstanceId { get; set; } = "";
        public string SystemName { get; set; } = "";
        public string DnsName { get; set; } = "";
        public string SessionToken { get; set; } = "";
        public string SessionId { get; set; } = "";
        public WebView2? WebView { get; set; }
        public TabViewItem? TabItem { get; set; }
    }

    /// <summary>
    /// Request to open a new DCV session
    /// </summary>
    public class DCVSessionRequest
    {
        public string InstanceId { get; set; } = "";
        public string SystemName { get; set; } = "";
        public string DnsName { get; set; } = "";
        public string SessionToken { get; set; } = "";
        public string SessionId { get; set; } = "";
    }
}

