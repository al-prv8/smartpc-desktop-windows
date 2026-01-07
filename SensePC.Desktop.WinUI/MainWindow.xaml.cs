using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Composition.SystemBackdrops;
using SensePC.Desktop.WinUI.Services;
using System;
using Serilog;
using WinRT;

namespace SensePC.Desktop.WinUI
{
    public sealed partial class MainWindow : Window
    {
        private readonly ISecureStorage _secureStorage;
        private string? _userEmail;
        
        public MainWindow()
        {
            this.InitializeComponent();
            
            // Enable Mica backdrop for Windows 11 style
            TrySetMicaBackdrop();
            
            // Extend content into title bar for modern look
            ExtendsContentIntoTitleBar = true;
            
            _secureStorage = new SecureStorage();
            
            // Navigate to login page immediately
            ShowLoginPage();
        }

        private void TrySetMicaBackdrop()
        {
            if (MicaController.IsSupported())
            {
                // Use Mica Alt for slightly darker variant
                SystemBackdrop = new MicaBackdrop() 
                { 
                    Kind = MicaKind.BaseAlt 
                };
            }
            else if (DesktopAcrylicController.IsSupported())
            {
                // Fallback to Acrylic if Mica not supported
                SystemBackdrop = new DesktopAcrylicBackdrop();
            }
        }

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
                
                // Set temporary name from email, will be updated with full name
                if (!string.IsNullOrEmpty(userEmail))
                {
                    var tempName = userEmail.Split('@')[0];
                    SidebarUserName.Text = tempName;
                }
                
                LoginFrame.Visibility = Visibility.Collapsed;
                NavView.Visibility = Visibility.Visible;
                
                // Select the Sense PC nav item by default
                NavView.SelectedItem = NavSensePC;
                DashboardFrame.Navigate(typeof(Views.SensePCPage));
                
                // Fetch balance and user profile in background
                _ = FetchBalanceAsync();
                _ = FetchUserProfileAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error navigating to Dashboard");
            }
        }

        private async System.Threading.Tasks.Task FetchBalanceAsync()
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

        private async System.Threading.Tasks.Task FetchUserProfileAsync()
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

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Select Sense PC by default when NavigationView loads
            NavView.SelectedItem = NavSensePC;
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                var tag = selectedItem.Tag?.ToString();
                
                switch (tag)
                {
                    case "SensePC":
                        DashboardFrame.Navigate(typeof(Views.SensePCPage));
                        break;
                    case "Users":
                        DashboardFrame.Navigate(typeof(Views.UsersPage));
                        break;
                    case "Logout":
                        HandleLogout();
                        break;
                    // Add other page navigations as needed
                    case "Tutorials":
                        DashboardFrame.Navigate(typeof(Views.TutorialsPage));
                        break;
                    case "Profile":
                        DashboardFrame.Navigate(typeof(Views.ProfilePage));
                        break;
                    case "Notifications":
                        DashboardFrame.Navigate(typeof(Views.NotificationsPage));
                        break;
                    case "Billing":
                        DashboardFrame.Navigate(typeof(Views.BillingPage));
                        break;
                    case "Support":
                        DashboardFrame.Navigate(typeof(Views.SupportPage));
                        break;
                    case "Storage":
                        DashboardFrame.Navigate(typeof(Views.StoragePage));
                        break;
                    case "Security":
                        // TODO: Implement Security page
                        break;
                }
            }
        }

        private async void HandleLogout()
        {
            try
            {
                // Clear stored tokens
                await _secureStorage.RemoveAsync("id_token");
                await _secureStorage.RemoveAsync("access_token");
                await _secureStorage.RemoveAsync("refresh_token");
                await _secureStorage.RemoveAsync("user_id");
                await _secureStorage.RemoveAsync("user_email");
                
                Log.Information("User logged out");
                
                // Navigate back to login
                ShowLoginPage();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during logout");
            }
        }
        
        /// <summary>
        /// Called after successful login to show dashboard
        /// </summary>
        public void OnLoginSuccess(string? userEmail = null)
        {
            ShowDashboard(userEmail);
        }
    }
}
