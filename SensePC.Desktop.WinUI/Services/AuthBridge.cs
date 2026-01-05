using Serilog;
using System;
using System.Threading.Tasks;

namespace SensePC.Desktop.WinUI.Services;

/// <summary>
/// Authentication bridge for securely managing tokens between native and web contexts
/// </summary>
public class AuthBridge : IAuthBridge
{
    private const string TokenKey = "auth_token";
    private const string RefreshTokenKey = "refresh_token";
    private const string TokenExpiryKey = "token_expiry";
    
    private readonly ISecureStorage _secureStorage;
    
    public AuthBridge(ISecureStorage secureStorage)
    {
        _secureStorage = secureStorage;
    }

    public async Task<string?> GetStoredTokenAsync()
    {
        try
        {
            // Check if token is expired
            var expiryStr = await _secureStorage.GetAsync(TokenExpiryKey);
            if (!string.IsNullOrEmpty(expiryStr) && long.TryParse(expiryStr, out var expiryTicks))
            {
                var expiry = new DateTime(expiryTicks, DateTimeKind.Utc);
                if (DateTime.UtcNow > expiry)
                {
                    Log.Information("Stored token has expired");
                    await ClearTokenAsync();
                    return null;
                }
            }

            var token = await _secureStorage.GetAsync(TokenKey);
            
            if (!string.IsNullOrEmpty(token))
            {
                Log.Debug("Retrieved stored token");
            }

            return token;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve stored token");
            return null;
        }
    }

    public async Task StoreTokenAsync(string token)
    {
        try
        {
            // Store token
            await _secureStorage.SetAsync(TokenKey, token);
            
            // Set expiry (24 hours from now as default, can be adjusted based on token claims)
            var expiry = DateTime.UtcNow.AddHours(24);
            await _secureStorage.SetAsync(TokenExpiryKey, expiry.Ticks.ToString());
            
            Log.Information("Token stored securely with expiry: {Expiry}", expiry);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to store token");
            throw;
        }
    }

    public async Task ClearTokenAsync()
    {
        try
        {
            await _secureStorage.RemoveAsync(TokenKey);
            await _secureStorage.RemoveAsync(RefreshTokenKey);
            await _secureStorage.RemoveAsync(TokenExpiryKey);
            
            Log.Information("All tokens cleared");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to clear tokens");
            throw;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetStoredTokenAsync();
        return !string.IsNullOrEmpty(token);
    }
}
