using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace SensePC.Desktop.WinUI.Services
{
    /// <summary>
    /// Handles OAuth authentication by opening the user's default browser
    /// and capturing the callback via a local HTTP listener.
    /// NOTE: This requires registering the callback URL in Cognito User Pool.
    /// Currently not used - OAuthWebViewService is preferred.
    /// </summary>
    public class OAuthBrowserService
    {
        private readonly ISecureStorage _secureStorage;
        
        // OAuth Configuration
        private const string OAuthDomain = "auth.smartpc.cloud";
        private const string CognitoClientId = "2lknj90rkjmtkcnph06q6r93ug";
        private const string TokenEndpoint = "https://auth.smartpc.cloud/oauth2/token";
        
        // Fixed port for callback - MUST be registered in Cognito User Pool
        private const int CallbackPort = 8888;
        private const string RedirectUri = "http://localhost:8888/callback";

        public OAuthBrowserService(ISecureStorage secureStorage)
        {
            _secureStorage = secureStorage;
        }

        /// <summary>
        /// Initiates OAuth login via the user's default browser
        /// </summary>
        public async Task<OAuthResult> AuthenticateAsync(string provider)
        {
            var result = new OAuthResult();
            HttpListener? listener = null;

            try
            {
                // Start HTTP listener on fixed port
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{CallbackPort}/");
                listener.Start();

                // Build OAuth URL and open in browser
                var authUrl = BuildAuthUrl(provider);
                OpenBrowser(authUrl);

                // Wait for callback
                var context = await listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                // Parse the callback URL
                var query = HttpUtility.ParseQueryString(request.Url?.Query ?? "");
                var code = query["code"];
                var error = query["error"];

                // Send response to browser
                var responseHtml = GenerateResponseHtml(error == null);
                var buffer = System.Text.Encoding.UTF8.GetBytes(responseHtml);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/html";
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.Close();

                if (!string.IsNullOrEmpty(error))
                {
                    result.Error = query["error_description"] ?? error;
                    return result;
                }

                if (!string.IsNullOrEmpty(code))
                {
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
                    }
                    else
                    {
                        result.Error = tokenResult.error ?? "Failed to exchange code for tokens";
                    }
                }
                else
                {
                    result.Error = "No authorization code received";
                }
            }
            catch (Exception ex)
            {
                result.Error = $"Authentication failed: {ex.Message}";
                Debug.WriteLine($"OAuth error: {ex.Message}");
            }
            finally
            {
                listener?.Stop();
                listener?.Close();
            }

            return result;
        }

        private void OpenBrowser(string url)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(startInfo);
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

        private string GenerateResponseHtml(bool success)
        {
            if (success)
            {
                return @"<!DOCTYPE html>
<html>
<head>
    <title>Sign In Successful</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; 
               display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0;
               background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); }
        .container { text-align: center; background: white; padding: 48px; border-radius: 16px; box-shadow: 0 25px 50px rgba(0,0,0,0.25); }
        h1 { color: #22c55e; margin: 0 0 16px; }
        p { color: #666; margin: 0; }
        .icon { font-size: 64px; margin-bottom: 16px; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>✅</div>
        <h1>Sign In Successful!</h1>
        <p>You can now close this window and return to the SensePC app.</p>
    </div>
</body>
</html>";
            }
            else
            {
                return @"<!DOCTYPE html>
<html>
<head>
    <title>Sign In Failed</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; 
               display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0;
               background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); }
        .container { text-align: center; background: white; padding: 48px; border-radius: 16px; box-shadow: 0 25px 50px rgba(0,0,0,0.25); }
        h1 { color: #ef4444; margin: 0 0 16px; }
        p { color: #666; margin: 0; }
        .icon { font-size: 64px; margin-bottom: 16px; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>❌</div>
        <h1>Sign In Failed</h1>
        <p>Please close this window and try again.</p>
    </div>
</body>
</html>";
            }
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
                    var errorResponse = JsonSerializer.Deserialize<TokenError>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return (false, null, null, null, errorResponse?.Error ?? "Token exchange failed");
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
}
