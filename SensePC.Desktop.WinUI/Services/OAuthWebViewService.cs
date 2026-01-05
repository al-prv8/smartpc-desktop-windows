using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace SensePC.Desktop.WinUI.Services
{
    /// <summary>
    /// Handles OAuth authentication via WebView2 for social login (Google, Apple)
    /// Uses Cognito Hosted UI for authentication
    /// </summary>
    public class OAuthWebViewService
    {
        private readonly ISecureStorage _secureStorage;
        
        // OAuth Configuration - from env.example and amplify-config.ts
        private const string OAuthDomain = "auth.smartpc.cloud";
        private const string CognitoClientId = "2lknj90rkjmtkcnph06q6r93ug";
        // Must match what's registered in Cognito - the website uses AUTH_REDIRECT_URL/auth/callback
        private const string RedirectUri = "https://smartpc.cloud/auth/callback";
        private const string TokenEndpoint = "https://auth.smartpc.cloud/oauth2/token";

        public OAuthWebViewService(ISecureStorage secureStorage)
        {
            _secureStorage = secureStorage;
        }

        /// <summary>
        /// Initiates OAuth login via WebView2 in a ContentDialog
        /// </summary>
        /// <param name="provider">OAuth provider: "Google" or "Apple"</param>
        /// <param name="xamlRoot">XamlRoot for the dialog</param>
        /// <returns>True if authentication succeeded</returns>
        public async Task<OAuthResult> AuthenticateAsync(string provider, XamlRoot xamlRoot)
        {
            var result = new OAuthResult();
            var tcs = new TaskCompletionSource<bool>();

            // Build OAuth URL based on provider
            var authUrl = BuildAuthUrl(provider);

            // Create WebView2 control
            var webView = new WebView2
            {
                Width = 450,
                Height = 550
            };

            // Create the dialog
            var dialog = new ContentDialog
            {
                Title = $"Sign in with {provider}",
                CloseButtonText = "Cancel",
                XamlRoot = xamlRoot,
                Content = webView
            };

            // Handle navigation to capture the callback
            webView.NavigationStarting += async (sender, args) =>
            {
                var uri = new Uri(args.Uri);
                
                // Check if this is the callback URL (matches the redirect URI)
                if (uri.Host == "smartpc.cloud" && uri.AbsolutePath.Contains("/auth/callback"))
                {
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    var code = query["code"];
                    var error = query["error"];

                    if (!string.IsNullOrEmpty(error))
                    {
                        result.Error = query["error_description"] ?? error;
                        tcs.TrySetResult(false);
                        dialog.Hide();
                        return;
                    }

                    if (!string.IsNullOrEmpty(code))
                    {
                        args.Cancel = true; // Stop navigation
                        
                        // Exchange code for tokens
                        var tokenResult = await ExchangeCodeForTokensAsync(code);
                        if (tokenResult.success)
                        {
                            result.Success = true;
                            result.IdToken = tokenResult.idToken;
                            result.AccessToken = tokenResult.accessToken;
                            result.RefreshToken = tokenResult.refreshToken;

                            // Store tokens
                            await _secureStorage.SetAsync("id_token", tokenResult.idToken ?? "");
                            await _secureStorage.SetAsync("access_token", tokenResult.accessToken ?? "");
                            await _secureStorage.SetAsync("refresh_token", tokenResult.refreshToken ?? "");

                            tcs.TrySetResult(true);
                        }
                        else
                        {
                            result.Error = tokenResult.error ?? "Failed to exchange code for tokens";
                            tcs.TrySetResult(false);
                        }
                        dialog.Hide();
                    }
                }
            };

            // Initialize WebView and navigate
            try
            {
                await webView.EnsureCoreWebView2Async();
                webView.CoreWebView2.Navigate(authUrl);
            }
            catch (Exception ex)
            {
                result.Error = $"Failed to initialize WebView: {ex.Message}";
                return result;
            }

            // Show dialog
            var dialogResult = await dialog.ShowAsync();
            if (dialogResult == ContentDialogResult.None && !result.Success)
            {
                // Wait for the OAuth flow to complete or timeout
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(5)));
                if (completed != tcs.Task)
                {
                    result.Error = "Authentication timed out";
                }
            }

            return result;
        }

        private string BuildAuthUrl(string provider)
        {
            var identityProvider = provider.ToLower() switch
            {
                "google" => "Google",
                "apple" => "SignInWithApple",
                _ => throw new ArgumentException($"Unknown provider: {provider}")
            };

            var scopes = "openid email profile";
            var responseType = "code";

            return $"https://{OAuthDomain}/oauth2/authorize?" +
                   $"identity_provider={identityProvider}&" +
                   $"client_id={CognitoClientId}&" +
                   $"response_type={responseType}&" +
                   $"scope={Uri.EscapeDataString(scopes)}&" +
                   $"redirect_uri={Uri.EscapeDataString(RedirectUri)}";
        }

        private async Task<(bool success, string? idToken, string? accessToken, string? refreshToken, string? error)> 
            ExchangeCodeForTokensAsync(string code)
        {
            try
            {
                using var httpClient = new HttpClient();
                
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "client_id", CognitoClientId },
                    { "code", code },
                    { "redirect_uri", RedirectUri }
                });

                var response = await httpClient.PostAsync(TokenEndpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var tokens = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return (true, tokens?.IdToken, tokens?.AccessToken, tokens?.RefreshToken, null);
                }
                else
                {
                    var error = JsonSerializer.Deserialize<TokenError>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return (false, null, null, null, error?.Error ?? "Token exchange failed");
                }
            }
            catch (Exception ex)
            {
                return (false, null, null, null, ex.Message);
            }
        }

        private class TokenResponse
        {
            public string? IdToken { get; set; }
            public string? AccessToken { get; set; }
            public string? RefreshToken { get; set; }
            public int ExpiresIn { get; set; }
            public string? TokenType { get; set; }
        }

        private class TokenError
        {
            public string? Error { get; set; }
            public string? ErrorDescription { get; set; }
        }
    }

    public class OAuthResult
    {
        public bool Success { get; set; }
        public string? IdToken { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? Error { get; set; }
    }
}
