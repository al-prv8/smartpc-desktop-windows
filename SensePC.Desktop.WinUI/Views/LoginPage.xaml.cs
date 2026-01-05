using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SensePC.Desktop.WinUI.ViewModels;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SensePC.Desktop.WinUI.Views
{
    public sealed partial class LoginPage : Page
    {
        public LoginViewModel ViewModel { get; }

        public LoginPage()
        {
            // TODO: Get from DI container
            var secureStorage = new SecureStorage();
            var authService = new CognitoAuthService(secureStorage);
            ViewModel = new LoginViewModel(authService);

            this.InitializeComponent();

            // Handle MFA dialog display
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.ShowMfaDialog))
                {
                    if (ViewModel.ShowMfaDialog)
                    {
                        _ = MfaDialog.ShowAsync();
                    }
                    else
                    {
                        MfaDialog.Hide();
                    }
                }
            };

            // Handle login success - navigate to dashboard
            ViewModel.OnLoginSuccess += () =>
            {
                // Get the MainWindow and call OnLoginSuccess with email
                if (App.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.OnLoginSuccess(ViewModel.Email);
                }
            };
        }

        private void SignUp_Click(object sender, RoutedEventArgs e)
        {
            // Open sign up page in browser
            var startInfo = new ProcessStartInfo
            {
                FileName = "https://smartpc.cloud/auth/sign-up",
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            // Open forgot password page in browser
            var startInfo = new ProcessStartInfo
            {
                FileName = "https://smartpc.cloud/auth",
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }

        private async void GoogleLogin_Click(object sender, RoutedEventArgs e)
        {
            await HandleSocialLoginAsync("Google");
        }

        private async void AppleLogin_Click(object sender, RoutedEventArgs e)
        {
            await HandleSocialLoginAsync("Apple");
        }

        private async Task HandleSocialLoginAsync(string provider)
        {
            GoogleLoginButton.IsEnabled = false;
            AppleLoginButton.IsEnabled = false;
            
            var buttonText = provider == "Google" ? GoogleButtonText : AppleButtonText;
            var originalText = buttonText.Text;
            buttonText.Text = "Signing in...";

            try
            {
                var secureStorage = new SecureStorage();
                var oauthService = new OAuthWebViewService(secureStorage);
                
                // Opens WebView2 dialog inside the app for OAuth
                var result = await oauthService.AuthenticateAsync(provider, this.XamlRoot);

                if (result.Success)
                {
                    // Navigate to dashboard
                    if (App.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.OnLoginSuccess("OAuth User");
                    }
                }
                else if (!string.IsNullOrEmpty(result.Error))
                {
                    // Show error
                    var dialog = new ContentDialog
                    {
                        Title = "Sign in failed",
                        Content = result.Error,
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Social login error: {ex.Message}");
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "Failed to sign in. Please try again.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            finally
            {
                buttonText.Text = originalText;
                GoogleLoginButton.IsEnabled = true;
                AppleLoginButton.IsEnabled = true;
            }
        }
    }
}
