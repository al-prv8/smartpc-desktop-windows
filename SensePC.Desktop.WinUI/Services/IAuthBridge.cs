using System.Threading.Tasks;

namespace SensePC.Desktop.WinUI.Services;

/// <summary>
/// Interface for authentication bridge between native app and web app
/// </summary>
public interface IAuthBridge
{
    /// <summary>
    /// Get stored authentication token
    /// </summary>
    Task<string?> GetStoredTokenAsync();
    
    /// <summary>
    /// Store authentication token securely
    /// </summary>
    Task StoreTokenAsync(string token);
    
    /// <summary>
    /// Clear stored authentication token
    /// </summary>
    Task ClearTokenAsync();
    
    /// <summary>
    /// Check if user is authenticated
    /// </summary>
    Task<bool> IsAuthenticatedAsync();
}
