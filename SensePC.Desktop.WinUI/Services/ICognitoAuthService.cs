using System.Threading.Tasks;

namespace SensePC.Desktop.WinUI.Services;

/// <summary>
/// Authentication result from Cognito operations
/// </summary>
public class AuthResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? IdToken { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    
    // MFA flow indicators
    public bool RequiresMFA { get; set; }
    public string? MFAType { get; set; } // "TOTP" or "EMAIL"
    public bool RequiresNewPassword { get; set; }
    public bool RequiresEmailVerification { get; set; }
    
    // Session for continuing MFA
    public string? Session { get; set; }
}

/// <summary>
/// User information from Cognito
/// </summary>
public class CognitoUser
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public string? UserId { get; set; }
    public string? OwnerId { get; set; }
}

/// <summary>
/// Interface for AWS Cognito authentication
/// </summary>
public interface ICognitoAuthService
{
    /// <summary>
    /// Sign in with email and password
    /// </summary>
    Task<AuthResult> SignInAsync(string email, string password);
    
    /// <summary>
    /// Sign up a new user
    /// </summary>
    Task<AuthResult> SignUpAsync(string email, string password, string firstName);
    
    /// <summary>
    /// Confirm sign up with OTP code
    /// </summary>
    Task<AuthResult> ConfirmSignUpAsync(string email, string code);
    
    /// <summary>
    /// Confirm MFA with TOTP code
    /// </summary>
    Task<AuthResult> ConfirmMFATotpAsync(string code, string session, string username);
    
    /// <summary>
    /// Confirm MFA with email code
    /// </summary>
    Task<AuthResult> ConfirmMFAEmailAsync(string code, string session, string username);
    
    /// <summary>
    /// Request password reset
    /// </summary>
    Task<AuthResult> ForgotPasswordAsync(string email);
    
    /// <summary>
    /// Confirm password reset with code
    /// </summary>
    Task<AuthResult> ConfirmForgotPasswordAsync(string email, string code, string newPassword);
    
    /// <summary>
    /// Sign out and clear tokens
    /// </summary>
    Task SignOutAsync();
    
    /// <summary>
    /// Refresh tokens using refresh token
    /// </summary>
    Task<AuthResult> RefreshTokensAsync(string refreshToken);
    
    /// <summary>
    /// Get user info from current session
    /// </summary>
    Task<CognitoUser?> GetUserAsync(string accessToken);
    
    /// <summary>
    /// Change the current user's password
    /// </summary>
    Task<bool> ChangePasswordAsync(string currentPassword, string newPassword);
}
