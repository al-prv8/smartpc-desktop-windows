using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SensePC.Desktop.WinUI.Services;

/// <summary>
/// AWS Cognito authentication service implementation
/// </summary>
public class CognitoAuthService : ICognitoAuthService
{
    // From env.example analysis
    private const string UserPoolId = "us-east-1_vgBCKmL0c";
    private const string ClientId = "2lknj90rkjmtkcnph06q6r93ug";
    private static readonly RegionEndpoint Region = RegionEndpoint.USEast1;

    private readonly AmazonCognitoIdentityProviderClient _cognitoClient;
    private readonly ISecureStorage _secureStorage;

    public CognitoAuthService(ISecureStorage secureStorage)
    {
        _secureStorage = secureStorage;
        _cognitoClient = new AmazonCognitoIdentityProviderClient(
            new Amazon.Runtime.AnonymousAWSCredentials(), 
            Region
        );
    }

    public async Task<AuthResult> SignInAsync(string email, string password)
    {
        try
        {
            var request = new InitiateAuthRequest
            {
                AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                ClientId = ClientId,
                AuthParameters = new Dictionary<string, string>
                {
                    { "USERNAME", email },
                    { "PASSWORD", password }
                }
            };

            var response = await _cognitoClient.InitiateAuthAsync(request);

            // Check for MFA challenges
            if (response.ChallengeName == ChallengeNameType.SMS_MFA ||
                response.ChallengeName == ChallengeNameType.SOFTWARE_TOKEN_MFA)
            {
                return new AuthResult
                {
                    Success = false,
                    RequiresMFA = true,
                    MFAType = response.ChallengeName == ChallengeNameType.SOFTWARE_TOKEN_MFA ? "TOTP" : "EMAIL",
                    Session = response.Session
                };
            }

            if (response.ChallengeName == ChallengeNameType.NEW_PASSWORD_REQUIRED)
            {
                return new AuthResult
                {
                    Success = false,
                    RequiresNewPassword = true,
                    Session = response.Session
                };
            }

            // Successful auth
            if (response.AuthenticationResult != null)
            {
                // Store tokens securely
                await _secureStorage.SetAsync("id_token", response.AuthenticationResult.IdToken);
                await _secureStorage.SetAsync("access_token", response.AuthenticationResult.AccessToken);
                await _secureStorage.SetAsync("refresh_token", response.AuthenticationResult.RefreshToken);

                // Extract and store user_id from ID token (JWT claims)
                var userId = ExtractClaimFromJwt(response.AuthenticationResult.IdToken, "sub");
                if (!string.IsNullOrEmpty(userId))
                {
                    await _secureStorage.SetAsync("user_id", userId);
                }
                
                // Also store email
                await _secureStorage.SetAsync("user_email", email);

                Log.Information("User signed in successfully: {Email}, UserId: {UserId}", email, userId);

                return new AuthResult
                {
                    Success = true,
                    IdToken = response.AuthenticationResult.IdToken,
                    AccessToken = response.AuthenticationResult.AccessToken,
                    RefreshToken = response.AuthenticationResult.RefreshToken
                };
            }

            return new AuthResult { Success = false, Error = "Unexpected authentication response" };
        }
        catch (NotAuthorizedException ex)
        {
            Log.Warning("Sign in failed - invalid credentials: {Email}", email);
            return new AuthResult { Success = false, Error = "Invalid email or password" };
        }
        catch (UserNotFoundException)
        {
            return new AuthResult { Success = false, Error = "User not found" };
        }
        catch (UserNotConfirmedException)
        {
            return new AuthResult { Success = false, RequiresEmailVerification = true, Error = "Please verify your email first" };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Sign in error for {Email}", email);
            return new AuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<AuthResult> SignUpAsync(string email, string password, string firstName)
    {
        try
        {
            var request = new SignUpRequest
            {
                ClientId = ClientId,
                Username = email,
                Password = password,
                UserAttributes = new List<AttributeType>
                {
                    new AttributeType { Name = "email", Value = email },
                    new AttributeType { Name = "custom:firstName", Value = firstName },
                    new AttributeType { Name = "custom:role", Value = "user" }
                }
            };

            var response = await _cognitoClient.SignUpAsync(request);
            
            Log.Information("User signed up, confirmation required: {Email}", email);
            
            return new AuthResult
            {
                Success = true,
                RequiresEmailVerification = !response.UserConfirmed
            };
        }
        catch (UsernameExistsException)
        {
            return new AuthResult { Success = false, Error = "An account with this email already exists" };
        }
        catch (InvalidPasswordException ex)
        {
            return new AuthResult { Success = false, Error = "Password does not meet requirements" };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Sign up error for {Email}", email);
            return new AuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<AuthResult> ConfirmSignUpAsync(string email, string code)
    {
        try
        {
            var request = new ConfirmSignUpRequest
            {
                ClientId = ClientId,
                Username = email,
                ConfirmationCode = code
            };

            await _cognitoClient.ConfirmSignUpAsync(request);
            
            Log.Information("Email confirmed for {Email}", email);
            return new AuthResult { Success = true };
        }
        catch (CodeMismatchException)
        {
            return new AuthResult { Success = false, Error = "Invalid verification code" };
        }
        catch (ExpiredCodeException)
        {
            return new AuthResult { Success = false, Error = "Verification code has expired" };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Confirm sign up error for {Email}", email);
            return new AuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<AuthResult> ConfirmMFATotpAsync(string code, string session, string username)
    {
        try
        {
            var request = new RespondToAuthChallengeRequest
            {
                ClientId = ClientId,
                ChallengeName = ChallengeNameType.SOFTWARE_TOKEN_MFA,
                Session = session,
                ChallengeResponses = new Dictionary<string, string>
                {
                    { "USERNAME", username },
                    { "SOFTWARE_TOKEN_MFA_CODE", code }
                }
            };

            var response = await _cognitoClient.RespondToAuthChallengeAsync(request);

            if (response.AuthenticationResult != null)
            {
                await _secureStorage.SetAsync("id_token", response.AuthenticationResult.IdToken);
                await _secureStorage.SetAsync("access_token", response.AuthenticationResult.AccessToken);
                await _secureStorage.SetAsync("refresh_token", response.AuthenticationResult.RefreshToken);

                return new AuthResult
                {
                    Success = true,
                    IdToken = response.AuthenticationResult.IdToken,
                    AccessToken = response.AuthenticationResult.AccessToken,
                    RefreshToken = response.AuthenticationResult.RefreshToken
                };
            }

            return new AuthResult { Success = false, Error = "MFA verification failed" };
        }
        catch (CodeMismatchException)
        {
            return new AuthResult { Success = false, Error = "Invalid authenticator code" };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MFA TOTP confirmation error");
            return new AuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<AuthResult> ConfirmMFAEmailAsync(string code, string session, string username)
    {
        // Similar to TOTP but for email MFA
        try
        {
            var request = new RespondToAuthChallengeRequest
            {
                ClientId = ClientId,
                ChallengeName = ChallengeNameType.SMS_MFA, // Email uses same flow
                Session = session,
                ChallengeResponses = new Dictionary<string, string>
                {
                    { "USERNAME", username },
                    { "SMS_MFA_CODE", code }
                }
            };

            var response = await _cognitoClient.RespondToAuthChallengeAsync(request);

            if (response.AuthenticationResult != null)
            {
                await _secureStorage.SetAsync("id_token", response.AuthenticationResult.IdToken);
                await _secureStorage.SetAsync("access_token", response.AuthenticationResult.AccessToken);
                await _secureStorage.SetAsync("refresh_token", response.AuthenticationResult.RefreshToken);

                return new AuthResult
                {
                    Success = true,
                    IdToken = response.AuthenticationResult.IdToken,
                    AccessToken = response.AuthenticationResult.AccessToken,
                    RefreshToken = response.AuthenticationResult.RefreshToken
                };
            }

            return new AuthResult { Success = false, Error = "MFA verification failed" };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MFA Email confirmation error");
            return new AuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<AuthResult> ForgotPasswordAsync(string email)
    {
        try
        {
            var request = new ForgotPasswordRequest
            {
                ClientId = ClientId,
                Username = email
            };

            await _cognitoClient.ForgotPasswordAsync(request);
            
            Log.Information("Password reset requested for {Email}", email);
            return new AuthResult { Success = true };
        }
        catch (UserNotFoundException)
        {
            return new AuthResult { Success = false, Error = "No account found with this email" };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Forgot password error for {Email}", email);
            return new AuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<AuthResult> ConfirmForgotPasswordAsync(string email, string code, string newPassword)
    {
        try
        {
            var request = new ConfirmForgotPasswordRequest
            {
                ClientId = ClientId,
                Username = email,
                ConfirmationCode = code,
                Password = newPassword
            };

            await _cognitoClient.ConfirmForgotPasswordAsync(request);
            
            Log.Information("Password reset confirmed for {Email}", email);
            return new AuthResult { Success = true };
        }
        catch (CodeMismatchException)
        {
            return new AuthResult { Success = false, Error = "Invalid reset code" };
        }
        catch (ExpiredCodeException)
        {
            return new AuthResult { Success = false, Error = "Reset code has expired" };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Confirm forgot password error for {Email}", email);
            return new AuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task SignOutAsync()
    {
        try
        {
            await _secureStorage.RemoveAsync("id_token");
            await _secureStorage.RemoveAsync("access_token");
            await _secureStorage.RemoveAsync("refresh_token");
            
            Log.Information("User signed out");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during sign out");
        }
    }

    public async Task<AuthResult> RefreshTokensAsync(string refreshToken)
    {
        try
        {
            var request = new InitiateAuthRequest
            {
                AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
                ClientId = ClientId,
                AuthParameters = new Dictionary<string, string>
                {
                    { "REFRESH_TOKEN", refreshToken }
                }
            };

            var response = await _cognitoClient.InitiateAuthAsync(request);

            if (response.AuthenticationResult != null)
            {
                await _secureStorage.SetAsync("id_token", response.AuthenticationResult.IdToken);
                await _secureStorage.SetAsync("access_token", response.AuthenticationResult.AccessToken);

                return new AuthResult
                {
                    Success = true,
                    IdToken = response.AuthenticationResult.IdToken,
                    AccessToken = response.AuthenticationResult.AccessToken
                };
            }

            return new AuthResult { Success = false, Error = "Token refresh failed" };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Token refresh error");
            return new AuthResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<CognitoUser?> GetUserAsync(string accessToken)
    {
        try
        {
            var request = new GetUserRequest
            {
                AccessToken = accessToken
            };

            var response = await _cognitoClient.GetUserAsync(request);

            var user = new CognitoUser
            {
                Email = response.UserAttributes.Find(a => a.Name == "email")?.Value ?? "",
                FirstName = response.UserAttributes.Find(a => a.Name == "custom:firstName")?.Value ?? 
                           response.UserAttributes.Find(a => a.Name == "given_name")?.Value ?? "",
                Role = response.UserAttributes.Find(a => a.Name == "custom:role")?.Value ?? "user",
                UserId = response.UserAttributes.Find(a => a.Name == "sub")?.Value,
                OwnerId = response.UserAttributes.Find(a => a.Name == "custom:ownerid")?.Value
            };

            return user;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Get user error");
            return null;
        }
    }

    /// <summary>
    /// Change the current user's password
    /// </summary>
    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        try
        {
            var accessToken = await _secureStorage.GetAsync("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                Log.Warning("No access token found for password change");
                return false;
            }

            var request = new ChangePasswordRequest
            {
                AccessToken = accessToken,
                PreviousPassword = currentPassword,
                ProposedPassword = newPassword
            };

            await _cognitoClient.ChangePasswordAsync(request);
            Log.Information("Password changed successfully");
            return true;
        }
        catch (NotAuthorizedException ex)
        {
            Log.Warning(ex, "Current password is incorrect");
            return false;
        }
        catch (InvalidPasswordException ex)
        {
            Log.Warning(ex, "New password does not meet requirements");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Change password error");
            return false;
        }
    }

    /// <summary>
    /// Extract a claim from a JWT token (ID token)
    /// </summary>
    private static string? ExtractClaimFromJwt(string jwt, string claimName)
    {
        try
        {
            // JWT format: header.payload.signature
            var parts = jwt.Split('.');
            if (parts.Length != 3) return null;

            // Decode payload (base64url)
            var payload = parts[1];
            
            // Add padding if needed for base64 decoding
            var padded = payload.Length % 4 == 0 ? payload :
                payload + new string('=', 4 - payload.Length % 4);
            
            // Replace URL-safe characters
            padded = padded.Replace('-', '+').Replace('_', '/');
            
            var bytes = Convert.FromBase64String(padded);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            
            // Parse JSON and extract claim
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(claimName, out var value))
            {
                return value.GetString();
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error extracting claim {ClaimName} from JWT", claimName);
            return null;
        }
    }
}
