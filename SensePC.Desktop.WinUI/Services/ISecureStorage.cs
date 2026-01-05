using System.Threading.Tasks;

namespace SensePC.Desktop.WinUI.Services;

/// <summary>
/// Interface for secure token storage using Windows Credential Manager
/// </summary>
public interface ISecureStorage
{
    /// <summary>
    /// Store a value securely
    /// </summary>
    Task<bool> SetAsync(string key, string value);
    
    /// <summary>
    /// Retrieve a securely stored value
    /// </summary>
    Task<string?> GetAsync(string key);
    
    /// <summary>
    /// Remove a securely stored value
    /// </summary>
    Task<bool> RemoveAsync(string key);
    
    /// <summary>
    /// Check if a key exists
    /// </summary>
    Task<bool> ExistsAsync(string key);
}
