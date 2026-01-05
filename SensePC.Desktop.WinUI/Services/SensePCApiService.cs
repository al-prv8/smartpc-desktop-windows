using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SensePC.Desktop.WinUI.Models;
using SensePC.Desktop.WinUI.Views.Dialogs;

namespace SensePC.Desktop.WinUI.Services
{
    /// <summary>
    /// API service for SensePC operations - fetching PCs, managing VMs, launching sessions
    /// </summary>
    public class SensePCApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ISecureStorage _secureStorage;

        public SensePCApiService(ISecureStorage secureStorage)
        {
            _secureStorage = secureStorage;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Get the ID token for API authorization
        /// </summary>
        private async Task<string?> GetIdTokenAsync()
        {
            return await _secureStorage.GetAsync("id_token");
        }

        /// <summary>
        /// Get the user ID from stored tokens or extract from ID token
        /// </summary>
        private async Task<string?> GetUserIdAsync()
        {
            // First try to get stored user_id
            var userId = await _secureStorage.GetAsync("user_id");
            if (!string.IsNullOrEmpty(userId))
            {
                return userId;
            }

            // Fallback: extract from ID token
            var idToken = await GetIdTokenAsync();
            if (!string.IsNullOrEmpty(idToken))
            {
                userId = ExtractClaimFromJwt(idToken, "sub");
                if (!string.IsNullOrEmpty(userId))
                {
                    // Store for next time
                    await _secureStorage.SetAsync("user_id", userId);
                    return userId;
                }
            }

            return null;
        }

        /// <summary>
        /// Extract a claim from a JWT token
        /// </summary>
        private static string? ExtractClaimFromJwt(string jwt, string claimName)
        {
            try
            {
                var parts = jwt.Split('.');
                if (parts.Length != 3) return null;

                var payload = parts[1];
                var padded = payload.Length % 4 == 0 ? payload :
                    payload + new string('=', 4 - payload.Length % 4);
                padded = padded.Replace('-', '+').Replace('_', '/');
                
                var bytes = Convert.FromBase64String(padded);
                var json = System.Text.Encoding.UTF8.GetString(bytes);
                
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(claimName, out var value))
                {
                    return value.GetString();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Fetch list of user's PCs
        /// </summary>
        public async Task<List<PCInstance>> FetchPCsAsync()
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                var userId = await GetUserIdAsync();

                if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(userId))
                {
                    System.Diagnostics.Debug.WriteLine("No auth token or user ID found");
                    return new List<PCInstance>();
                }

                var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiConfig.FetchPCUrl}?userId={userId}");
                request.Headers.Add("Authorization", $"Bearer {idToken}");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"FetchPCs response: {content}");

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"FetchPCs failed: {response.StatusCode}");
                    return new List<PCInstance>();
                }

                // Try to parse as array first, then as object with instances property
                try
                {
                    var instances = JsonSerializer.Deserialize<List<PCInstance>>(content);
                    return instances ?? new List<PCInstance>();
                }
                catch
                {
                    var wrapper = JsonSerializer.Deserialize<FetchPCResponse>(content);
                    return wrapper?.GetInstances() ?? new List<PCInstance>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FetchPCs error: {ex.Message}");
                return new List<PCInstance>();
            }
        }

        private const string BILLING_API_URL = "https://558xjerom8.execute-api.us-east-1.amazonaws.com/prod/billing/";
        private const string USER_MANAGEMENT_API_URL = "https://v0605yjfrf.execute-api.us-east-1.amazonaws.com/prod/";

        /// <summary>
        /// Get current user balance from billing API
        /// </summary>
        public async Task<decimal?> GetBalanceAsync()
        {
            try
            {
                var token = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(token)) return null;

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{BILLING_API_URL}balance");
                request.Headers.Add("Authorization", token);
                
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                var balanceResponse = JsonSerializer.Deserialize<BalanceResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return balanceResponse?.Balance;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetBalance error: {ex.Message}");
                return null;
            }
        }

        #region User Management

        /// <summary>
        /// Get all users in the organization
        /// </summary>
        public async Task<List<ApiUser>> GetUsersAsync()
        {
            try
            {
                var token = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(token)) return new List<ApiUser>();

                using var request = new HttpRequestMessage(HttpMethod.Get, USER_MANAGEMENT_API_URL);
                request.Headers.Add("Authorization", token);
                
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return new List<ApiUser>();

                var content = await response.Content.ReadAsStringAsync();
                var usersResponse = JsonSerializer.Deserialize<GetUsersResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return usersResponse?.Users ?? new List<ApiUser>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUsers error: {ex.Message}");
                return new List<ApiUser>();
            }
        }

        /// <summary>
        /// Invite a new user to the organization
        /// </summary>
        public async Task<bool> InviteUserAsync(string name, string email, string role)
        {
            try
            {
                var token = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;

                var payload = new { name, email, role, group = "" };
                var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Post, USER_MANAGEMENT_API_URL);
                request.Headers.Add("Authorization", token);
                request.Content = jsonContent;
                
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InviteUser error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete a user from the organization
        /// </summary>
        public async Task<bool> DeleteUserAsync(string userId, string email)
        {
            try
            {
                var token = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;

                var payload = new { id = userId, email };
                var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Delete, USER_MANAGEMENT_API_URL);
                request.Headers.Add("Authorization", token);
                request.Content = jsonContent;
                
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteUser error: {ex.Message}");
                return false;
            }
        }

        #endregion

        /// <summary>
        /// Start a VM instance
        /// </summary>
        public async Task<VmActionResponse> StartVMAsync(string instanceId)
        {
            return await ExecuteVmActionAsync("start", instanceId);
        }

        /// <summary>
        /// Stop a VM instance
        /// </summary>
        public async Task<VmActionResponse> StopVMAsync(string instanceId)
        {
            return await ExecuteVmActionAsync("stop", instanceId);
        }

        /// <summary>
        /// Restart a VM instance
        /// </summary>
        public async Task<VmActionResponse> RestartVMAsync(string instanceId)
        {
            return await ExecuteVmActionAsync("restart", instanceId);
        }

        /// <summary>
        /// Execute a VM action (start, stop, restart)
        /// </summary>
        private async Task<VmActionResponse> ExecuteVmActionAsync(string action, string instanceId)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                {
                    return new VmActionResponse { Message = "Not authenticated", StatusCode = 401 };
                }

                var payload = new
                {
                    action = action,
                    instanceId = instanceId,
                    region = "us-east-1" // Default region
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, ApiConfig.VmManagementUrl);
                request.Headers.Add("Authorization", idToken);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"{action} VM response: {responseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    return new VmActionResponse 
                    { 
                        Message = $"Failed to {action} VM: {responseContent}", 
                        StatusCode = (int)response.StatusCode 
                    };
                }

                try
                {
                    return JsonSerializer.Deserialize<VmActionResponse>(responseContent) 
                        ?? new VmActionResponse { Message = "Success", StatusCode = 200 };
                }
                catch
                {
                    return new VmActionResponse { Message = responseContent, StatusCode = 200 };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{action} VM error: {ex.Message}");
                return new VmActionResponse { Message = ex.Message, StatusCode = 500 };
            }
        }

        /// <summary>
        /// Get real-time instance details (uptime, schedule, idle, specs)
        /// </summary>
        public async Task<Dictionary<string, InstanceDetails>> GetInstanceDetailsAsync(List<string> systemNames)
        {
            try
            {
                var userId = await GetUserIdAsync();
                if (string.IsNullOrEmpty(userId) || systemNames.Count == 0)
                {
                    return new Dictionary<string, InstanceDetails>();
                }

                var payload = new
                {
                    userId = userId,
                    instanceNames = systemNames
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(ApiConfig.InstanceDetailsUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"InstanceDetails response: {responseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    return new Dictionary<string, InstanceDetails>();
                }

                var details = JsonSerializer.Deserialize<List<InstanceDetails>>(responseContent);
                var result = new Dictionary<string, InstanceDetails>();

                if (details != null)
                {
                    foreach (var detail in details)
                    {
                        if (!string.IsNullOrEmpty(detail.SystemName))
                        {
                            result[detail.SystemName] = detail;
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InstanceDetails error: {ex.Message}");
                return new Dictionary<string, InstanceDetails>();
            }
        }

        /// <summary>
        /// Launch a DCV session for connecting to the PC
        /// </summary>
        public async Task<SessionLaunchResponse> LaunchSessionAsync(string instanceId)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                var userId = await GetUserIdAsync();

                if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(userId))
                {
                    return new SessionLaunchResponse { Message = "Not authenticated", StatusCode = 401 };
                }

                var payload = new
                {
                    action = "start-session",
                    userId = userId,
                    instanceId = instanceId
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, ApiConfig.SessionStartUrl);
                request.Headers.Add("Authorization", $"Bearer {idToken}");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"LaunchSession response: {responseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    return new SessionLaunchResponse 
                    { 
                        Message = $"Failed to launch session: {responseContent}", 
                        StatusCode = (int)response.StatusCode 
                    };
                }

                return JsonSerializer.Deserialize<SessionLaunchResponse>(responseContent) 
                    ?? new SessionLaunchResponse { Message = "Session launched", StatusCode = 200 };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LaunchSession error: {ex.Message}");
                return new SessionLaunchResponse { Message = ex.Message, StatusCode = 500 };
            }
        }

        /// <summary>
        /// Save schedule settings for a PC
        /// </summary>
        public async Task<bool> SaveScheduleAsync(string instanceId, ScheduleInfo schedule)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                    return false;

                var payload = new
                {
                    instanceId = instanceId,
                    timeZone = schedule.TimeZone ?? "UTC",
                    frequency = schedule.Frequency ?? "everyday",
                    autoStartTime = schedule.AutoStartTime,
                    autoStopTime = schedule.AutoStopTime,
                    enabled = schedule.Enabled
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, ApiConfig.ScheduleUrl);
                request.Headers.Add("Authorization", idToken);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveSchedule error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save idle timeout settings for a PC
        /// </summary>
        public async Task<bool> SaveIdleTimeoutAsync(string instanceId, int timeoutMinutes)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                    return false;

                var payload = new
                {
                    instanceId = instanceId,
                    idleTimeout = timeoutMinutes
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, ApiConfig.IdleUrl);
                request.Headers.Add("Authorization", idToken);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveIdleTimeout error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save idle settings (wrapper for SaveIdleTimeoutAsync)
        /// </summary>
        public Task<bool> SaveIdleSettingsAsync(string instanceId, int timeoutMinutes)
        {
            return SaveIdleTimeoutAsync(instanceId, timeoutMinutes);
        }

        /// <summary>
        /// Save schedule settings for a PC
        /// </summary>
        public async Task<bool> SaveScheduleAsync(string instanceId, object schedule)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                    return false;

                var payload = new
                {
                    instanceId = instanceId,
                    schedule = schedule
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, ApiConfig.ScheduleUrl);
                request.Headers.Add("Authorization", $"Bearer {idToken}");
                request.Content = content;

                System.Diagnostics.Debug.WriteLine($"SaveSchedule request: {json}");

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveSchedule error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete a VM
        /// </summary>
        public async Task<bool> DeleteVMAsync(string instanceId, string region)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                    return false;

                var payload = new
                {
                    action = "delete",
                    instanceId = instanceId,
                    region = region
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, ApiConfig.VmManagementUrl);
                request.Headers.Add("Authorization", $"Bearer {idToken}");
                request.Content = content;

                System.Diagnostics.Debug.WriteLine($"DeleteVM request: {json}");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"DeleteVM response ({response.StatusCode}): {responseContent}");

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteVM error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Resize a PC (change CPU/RAM configuration)
        /// Only works when PC is stopped and on Hourly plan
        /// </summary>
        public async Task<bool> ResizePCAsync(string computerName, string targetConfigId)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                var userId = await GetUserIdAsync();

                if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(userId))
                {
                    System.Diagnostics.Debug.WriteLine("ResizePC: Missing auth tokens");
                    return false;
                }

                var payload = new
                {
                    userId = userId,
                    computerName = computerName,
                    targetConfigId = targetConfigId
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, ApiConfig.ResizeUrl)
                {
                    Content = content
                };
                request.Headers.Add("Authorization", $"Bearer {idToken}");

                System.Diagnostics.Debug.WriteLine($"ResizePC request: {json}");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"ResizePC response ({response.StatusCode}): {responseContent}");

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResizePC error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Increase storage volume for a PC
        /// Only works when PC is running and on Hourly plan
        /// </summary>
        public async Task<bool> IncreaseVolumeAsync(string computerName, int newVolumeSizeGiB)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                var userId = await GetUserIdAsync();

                if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(userId))
                {
                    System.Diagnostics.Debug.WriteLine("IncreaseVolume: Missing auth tokens");
                    return false;
                }

                var payload = new
                {
                    userId = userId,
                    computerName = computerName,
                    newVolumeSizeGiB = newVolumeSizeGiB
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, ApiConfig.IncreaseVolumeUrl)
                {
                    Content = content
                };
                request.Headers.Add("Authorization", $"Bearer {idToken}");

                System.Diagnostics.Debug.WriteLine($"IncreaseVolume request: {json}");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"IncreaseVolume response ({response.StatusCode}): {responseContent}");

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IncreaseVolume error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get list of team members for assignment
        /// </summary>
        public async Task<List<TeamMember>> GetTeamMembersAsync()
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken)) return new List<TeamMember>();

                var request = new HttpRequestMessage(HttpMethod.Get, ApiConfig.UsersUrl);
                request.Headers.Add("Authorization", $"Bearer {idToken}");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<UsersResponse>(responseContent);
                    return result?.Users ?? new List<TeamMember>();
                }
                return new List<TeamMember>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTeamMembers error: {ex.Message}");
                return new List<TeamMember>();
            }
        }

        /// <summary>
        /// Get PC assignments (which users are assigned to which PCs)
        /// </summary>
        public async Task<Dictionary<string, List<PCAssignment>>> GetAssignmentsAsync()
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken)) return new Dictionary<string, List<PCAssignment>>();

                var request = new HttpRequestMessage(HttpMethod.Get, ApiConfig.AssignUrl);
                request.Headers.Add("Authorization", $"Bearer {idToken}");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<Dictionary<string, List<PCAssignment>>>(responseContent) 
                        ?? new Dictionary<string, List<PCAssignment>>();
                }
                return new Dictionary<string, List<PCAssignment>>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAssignments error: {ex.Message}");
                return new Dictionary<string, List<PCAssignment>>();
            }
        }

        /// <summary>
        /// Assign a PC to a team member
        /// </summary>
        public async Task<bool> AssignPCAsync(string instanceId, string memberId, string systemName)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken)) return false;

                var payload = new { instanceId, memberId, systemName };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, ApiConfig.AssignUrl)
                {
                    Content = content
                };
                request.Headers.Add("Authorization", $"Bearer {idToken}");

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AssignPC error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unassign a PC from a team member
        /// </summary>
        public async Task<bool> UnassignPCAsync(string instanceId, string memberId)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken)) return false;

                var payload = new { instanceId, memberId };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Delete, ApiConfig.AssignUrl)
                {
                    Content = content
                };
                request.Headers.Add("Authorization", $"Bearer {idToken}");

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UnassignPC error: {ex.Message}");
                return false;
            }
        }

        #region Profile Management

        /// <summary>
        /// Get the current user's profile
        /// </summary>
        public async Task<Views.UserProfile?> GetUserProfileAsync()
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken)) return null;

                var request = new HttpRequestMessage(HttpMethod.Get, ApiConfig.ProfileUrl);
                request.Headers.Add("Authorization", $"Bearer {idToken}");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<Views.UserProfile>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUserProfile error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Update the current user's profile
        /// </summary>
        public async Task<bool> UpdateUserProfileAsync(string firstName, string lastName, string country, string organization)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken)) return false;

                var payload = new 
                { 
                    firstName, 
                    lastName, 
                    country, 
                    organization 
                };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Put, ApiConfig.ProfileUrl)
                {
                    Content = content
                };
                request.Headers.Add("Authorization", $"Bearer {idToken}");

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateUserProfile error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Create a presigned URL for uploading avatar
        /// </summary>
        public async Task<AvatarUploadResponse?> CreateAvatarUploadUrlAsync(string contentType, string fileExt)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken)) return null;

                var payload = new { contentType, fileExt };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var avatarUrl = ApiConfig.ProfileUrl.TrimEnd('/') + "/avatar";
                var request = new HttpRequestMessage(HttpMethod.Post, avatarUrl)
                {
                    Content = content
                };
                request.Headers.Add("Authorization", idToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<AvatarUploadResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateAvatarUploadUrl error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Delete the user's avatar
        /// </summary>
        public async Task<bool> DeleteAvatarAsync()
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken)) return false;

                var avatarUrl = ApiConfig.ProfileUrl.TrimEnd('/') + "/avatar";
                var request = new HttpRequestMessage(HttpMethod.Delete, avatarUrl);
                request.Headers.Add("Authorization", idToken);

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteAvatar error: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// Get all active sessions for the current user (connected devices)
        /// </summary>
        public async Task<List<ActiveSession>> GetActiveSessionsAsync()
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken)) return new List<ActiveSession>();

                var request = new HttpRequestMessage(HttpMethod.Get, ApiConfig.ClientSessionUrl);
                request.Headers.Add("Authorization", $"Bearer {idToken}");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var sessions = JsonSerializer.Deserialize<List<ActiveSession>>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<ActiveSession>();

                    // Mark current session
                    var currentSessionId = await _secureStorage.GetAsync("session_id");
                    foreach (var session in sessions)
                    {
                        session.IsCurrentSession = session.SessionId == currentSessionId;
                    }
                    return sessions;
                }
                return new List<ActiveSession>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetActiveSessions error: {ex.Message}");
                return new List<ActiveSession>();
            }
        }

        #endregion

        #region Billing API

        /// <summary>
        /// Get wallet balance, promo, and cashback
        /// </summary>
        public async Task<BillingBalance?> GetBillingBalanceAsync()
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                {
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiConfig.BillingUrl}balance");
                request.Headers.Add("Authorization", idToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<BillingBalance>(content);
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetBillingBalance error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get monthly spending summary
        /// </summary>
        public async Task<MonthlySpending?> GetMonthlySpendingAsync()
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                {
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiConfig.BillingUrl}monthly-spending");
                request.Headers.Add("Authorization", idToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<MonthlySpending>(content);
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetMonthlySpending error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get usage history for billing
        /// </summary>
        public async Task<List<UsageHistoryItem>> GetUsageHistoryAsync(int limit = 10)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                {
                    return new List<UsageHistoryItem>();
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiConfig.BillingUrl}usage-history?pageSize={limit}");
                request.Headers.Add("Authorization", idToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<UsageHistoryResponse>(content);
                    return result?.Items ?? new List<UsageHistoryItem>();
                }
                return new List<UsageHistoryItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUsageHistory error: {ex.Message}");
                return new List<UsageHistoryItem>();
            }
        }

        /// <summary>
        /// Get recharge/wallet history
        /// </summary>
        public async Task<List<RechargeHistoryItem>> GetRechargeHistoryAsync(int limit = 10)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                {
                    return new List<RechargeHistoryItem>();
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiConfig.BillingUrl}recharge?pageSize={limit}");
                request.Headers.Add("Authorization", idToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<RechargeHistoryResponse>(content);
                    return result?.History ?? new List<RechargeHistoryItem>();
                }
                return new List<RechargeHistoryItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetRechargeHistory error: {ex.Message}");
                return new List<RechargeHistoryItem>();
            }
        }

        /// <summary>
        /// Get payment methods
        /// </summary>
        public async Task<PaymentMethodResponse?> GetPaymentMethodsAsync()
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                {
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiConfig.BillingUrl}payment-methods");
                request.Headers.Add("Authorization", idToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<PaymentMethodResponse>(content);
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPaymentMethods error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Set a payment method as default
        /// </summary>
        public async Task<bool> SetDefaultPaymentMethodAsync(string paymentMethodId)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                {
                    return false;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiConfig.BillingUrl}set-default-card");
                request.Headers.Add("Authorization", idToken);
                
                var body = new { paymentMethodId };
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetDefaultPaymentMethod error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Remove a payment method
        /// </summary>
        public async Task<bool> DetachPaymentMethodAsync(string paymentMethodId)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                {
                    return false;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiConfig.BillingUrl}payment-methods/detach");
                request.Headers.Add("Authorization", idToken);
                
                var body = new { paymentMethodId };
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DetachPaymentMethod error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Add a new payment method via backend Stripe integration
        /// </summary>
        public async Task<AddPaymentMethodResult> AddPaymentMethodAsync(
            string cardNumber, 
            int expMonth, 
            int expYear, 
            string cvc, 
            string cardholderName,
            bool setAsDefault = false)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                {
                    return new AddPaymentMethodResult { Success = false, ErrorMessage = "Not authenticated" };
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiConfig.BillingUrl}payment-methods");
                request.Headers.Add("Authorization", idToken);
                
                var body = new 
                { 
                    cardNumber = cardNumber.Replace(" ", "").Replace("-", ""),
                    expMonth,
                    expYear,
                    cvc,
                    cardholderName,
                    setAsDefault
                };
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return new AddPaymentMethodResult { Success = true };
                }
                
                // Try to parse error message
                try
                {
                    var errorObj = JsonSerializer.Deserialize<JsonElement>(content);
                    if (errorObj.TryGetProperty("message", out var msgProp))
                    {
                        return new AddPaymentMethodResult { Success = false, ErrorMessage = msgProp.GetString() };
                    }
                }
                catch { }
                
                return new AddPaymentMethodResult { Success = false, ErrorMessage = content };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddPaymentMethod error: {ex.Message}");
                return new AddPaymentMethodResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Recharge wallet with specified amount
        /// </summary>
        public async Task<RechargeResult?> RechargeWalletAsync(decimal amount, string? paymentMethodId = null)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                {
                    return new RechargeResult { Success = false, ErrorMessage = "Not authenticated" };
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiConfig.BillingUrl}recharge");
                request.Headers.Add("Authorization", idToken);
                
                var body = new { amount, paymentMethodId };
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return new RechargeResult { Success = true };
                }
                
                return new RechargeResult { Success = false, ErrorMessage = content };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RechargeWallet error: {ex.Message}");
                return new RechargeResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Get auto-recharge settings
        /// </summary>
        public async Task<AutoRechargeSettings?> GetAutoRechargeAsync()
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                {
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiConfig.BillingUrl}auto-recharge");
                request.Headers.Add("Authorization", idToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<AutoRechargeSettings>(content);
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAutoRecharge error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Update auto-recharge settings
        /// </summary>
        public async Task<bool> UpdateAutoRechargeAsync(bool enabled, decimal threshold, decimal rechargeAmount)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                {
                    return false;
                }

                using var request = new HttpRequestMessage(HttpMethod.Put, $"{ApiConfig.BillingUrl}auto-recharge");
                request.Headers.Add("Authorization", idToken);
                
                var body = new { enabled, threshold, rechargeAmount };
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateAutoRecharge error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get storage pricing tiers
        /// </summary>
        public async Task<List<StoragePricingTier>> GetStoragePricingAsync()
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                {
                    return new List<StoragePricingTier>();
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiConfig.BillingUrl}storage/tier-pricing");
                request.Headers.Add("Authorization", idToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<StoragePricingResponse>(content);
                    return result?.Tiers ?? new List<StoragePricingTier>();
                }
                return new List<StoragePricingTier>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetStoragePricing error: {ex.Message}");
                return new List<StoragePricingTier>();
            }
        }

        #endregion

        #region Support API

        /// <summary>
        /// Get all tickets for the current user
        /// </summary>
        public async Task<List<SupportTicket>> GetTicketsAsync()
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                var userId = await GetUserIdAsync();
                if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(userId))
                {
                    return new List<SupportTicket>();
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiConfig.SupportUrl}tickets");
                request.Headers.Add("Authorization", idToken);
                request.Headers.Add("x-user-id", userId);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TicketsResponse>(content);
                    return result?.Tickets ?? new List<SupportTicket>();
                }
                return new List<SupportTicket>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTickets error: {ex.Message}");
                return new List<SupportTicket>();
            }
        }

        /// <summary>
        /// Get a ticket by ID
        /// </summary>
        public async Task<SupportTicket?> GetTicketByIdAsync(string ticketId)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                var userId = await GetUserIdAsync();
                if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(userId))
                {
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiConfig.SupportUrl}ticket/{ticketId}");
                request.Headers.Add("Authorization", idToken);
                request.Headers.Add("x-user-id", userId);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<SupportTicket>(content);
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTicketById error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create a new support ticket
        /// </summary>
        public async Task<bool> CreateTicketAsync(string subject, string category, string priority, string description)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                var userId = await GetUserIdAsync();
                if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(userId))
                {
                    System.Diagnostics.Debug.WriteLine("CreateTicket: Missing idToken or userId");
                    return false;
                }

                // Get user email from token if available
                var email = ExtractClaimFromJwt(idToken, "email") ?? "";
                var role = "owner"; // Default role

                var body = new 
                { 
                    userId,
                    subject, 
                    description,
                    category = category.ToLower(), 
                    priority = priority.ToLower(),
                    email,
                    role,
                    attachments = new object[] { }
                };
                
                var jsonBody = JsonSerializer.Serialize(body);
                System.Diagnostics.Debug.WriteLine($"CreateTicket request: {jsonBody}");
                
                var jsonContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiConfig.SupportUrl}ticket");
                request.Headers.Add("Authorization", idToken);
                request.Headers.Add("x-user-id", userId);
                request.Content = jsonContent;

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"CreateTicket response: {response.StatusCode} - {responseContent}");
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateTicket error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get messages for a ticket
        /// </summary>
        public async Task<List<TicketMessage>> GetTicketMessagesAsync(string ticketId)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                var userId = await GetUserIdAsync();
                if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(userId))
                {
                    return new List<TicketMessage>();
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiConfig.SupportUrl}ticket/{ticketId}/messages");
                request.Headers.Add("Authorization", idToken);
                request.Headers.Add("x-user-id", userId);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TicketMessagesResponse>(content);
                    return result?.Messages ?? new List<TicketMessage>();
                }
                return new List<TicketMessage>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTicketMessages error: {ex.Message}");
                return new List<TicketMessage>();
            }
        }

        /// <summary>
        /// Send a message/reply to a support ticket
        /// </summary>
        public async Task<bool> SendTicketMessageAsync(string ticketId, string content)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                var userId = await GetUserIdAsync();
                if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(userId))
                {
                    return false;
                }

                var body = new
                {
                    senderId = userId,
                    senderType = "customer",
                    type = "message",
                    content,
                    attachments = new object[] { }
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiConfig.SupportUrl}ticket/{ticketId}/message");
                request.Headers.Add("Authorization", idToken);
                request.Headers.Add("x-user-id", userId);
                request.Content = jsonContent;

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendTicketMessage error: {ex.Message}");
                return false;
            }
        }

        #endregion
    }

    /// <summary>
    /// Avatar upload URL response
    /// </summary>
    public class AvatarUploadResponse
    {
        public string? UploadUrl { get; set; }
        public string? Key { get; set; }
    }

    /// <summary>
    /// Team member model for PC assignment
    /// </summary>
    public class TeamMember
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string? Email { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("role")]
        public string? Role { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("assignedPCs")]
        public List<string>? AssignedPCs { get; set; }
    }

    /// <summary>
    /// PC assignment model
    /// </summary>
    public class PCAssignment
    {
        [System.Text.Json.Serialization.JsonPropertyName("instanceId")]
        public string InstanceId { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("memberId")]
        public string MemberId { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("systemName")]
        public string? SystemName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("assignedAt")]
        public string? AssignedAt { get; set; }
    }

    /// <summary>
    /// Response wrapper for users API
    /// </summary>
    public class UsersResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("users")]
        public List<TeamMember> Users { get; set; } = new();
    }

    /// <summary>
    /// Response from billing balance API
    /// </summary>
    public class BalanceResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("balance")]
        public decimal Balance { get; set; }
    }

    /// <summary>
    /// Response wrapper for user management API
    /// </summary>
    public class GetUsersResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("users")]
        public List<ApiUser> Users { get; set; } = new();
    }

    /// <summary>
    /// User model from user management API
    /// </summary>
    public class ApiUser
    {
        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string Email { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("role")]
        public string Role { get; set; } = "member";

        [System.Text.Json.Serialization.JsonPropertyName("owner_id")]
        public string OwnerId { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("country")]
        public string? Country { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("organization")]
        public string? Organization { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("phoneNumber")]
        public string? PhoneNumber { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>
        /// Get display name (FirstName LastName or Email)
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(FirstName) || !string.IsNullOrEmpty(LastName)
            ? $"{FirstName} {LastName}".Trim()
            : Email;
    }

    /// <summary>
    /// Active session/connected device model
    /// </summary>
    public class ActiveSession
    {
        [System.Text.Json.Serialization.JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("deviceName")]
        public string? DeviceName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("ip")]
        public string? IpAddress { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lastSeen")]
        public string? LastSeen { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("location")]
        public SessionLocation? Location { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("occupied")]
        public bool Occupied { get; set; }

        public bool IsCurrentSession { get; set; }

        public string DisplayName => DeviceName ?? "Unknown Device";

        public string LastActivityFormatted
        {
            get
            {
                if (string.IsNullOrEmpty(LastSeen)) return "Unknown";
                if (DateTime.TryParse(LastSeen, out var dt))
                    return dt.ToString("MMM dd, yyyy h:mm tt");
                return LastSeen;
            }
        }

        public string LocationDisplay
        {
            get
            {
                if (Location == null) return "";
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(Location.City)) parts.Add(Location.City);
                if (!string.IsNullOrEmpty(Location.Country)) parts.Add(Location.Country);
                else if (!string.IsNullOrEmpty(Location.Region)) parts.Add(Location.Region);
                return parts.Count > 0 ? string.Join(", ", parts) : "";
            }
        }
    }

    /// <summary>
    /// Session location details
    /// </summary>
    public class SessionLocation
    {
        [System.Text.Json.Serialization.JsonPropertyName("city")]
        public string? City { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("region")]
        public string? Region { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("country")]
        public string? Country { get; set; }
    }

    #region Billing Models

    /// <summary>
    /// Billing balance including promo and cashback
    /// </summary>
    public class BillingBalance
    {
        [System.Text.Json.Serialization.JsonPropertyName("balance")]
        public decimal Balance { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("promoBalance")]
        public decimal PromoBalance { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("cashback")]
        public decimal Cashback { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lastRecharge")]
        public LastRechargeInfo? LastRecharge { get; set; }
    }

    public class LastRechargeInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }
    }

    /// <summary>
    /// Monthly spending summary
    /// </summary>
    public class MonthlySpending
    {
        [System.Text.Json.Serialization.JsonPropertyName("currentMonth")]
        public decimal CurrentMonth { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lastMonth")]
        public decimal LastMonth { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("percentChange")]
        public decimal PercentChange { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("trend")]
        public string Trend { get; set; } = "no change";
    }

    /// <summary>
    /// Usage history item for billing
    /// </summary>
    public class UsageHistoryItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("instanceId")]
        public string InstanceId { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("billingAmount")]
        public string BillingAmount { get; set; } = "0";

        [System.Text.Json.Serialization.JsonPropertyName("billingPlan")]
        public string BillingPlan { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("systemName")]
        public string SystemName { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("instanceCost")]
        public string InstanceCost { get; set; } = "0";

        [System.Text.Json.Serialization.JsonPropertyName("storageCost")]
        public string StorageCost { get; set; } = "0";

        [System.Text.Json.Serialization.JsonPropertyName("startTime")]
        public string StartTime { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("endTime")]
        public string EndTime { get; set; } = "";

        public string FormattedDate => !string.IsNullOrEmpty(Timestamp) && DateTime.TryParse(Timestamp, out var dt)
            ? dt.ToString("MMM dd, yyyy")
            : Timestamp;

        public decimal Amount => decimal.TryParse(BillingAmount, out var amt) ? amt : 0;

        public string AmountDisplay => $"${Amount:N2}";
    }

    /// <summary>
    /// Usage history API response wrapper
    /// </summary>
    public class UsageHistoryResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("items")]
        public List<UsageHistoryItem>? Items { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("hasMore")]
        public bool HasMore { get; set; }
    }

    /// <summary>
    /// Recharge/wallet history item
    /// </summary>
    public class RechargeHistoryItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("eventTimestamp")]
        public string EventTimestamp { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("eventType")]
        public string EventType { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("amount")]
        public string AmountStr { get; set; } = "0";

        public decimal Amount => decimal.TryParse(AmountStr, out var amt) ? amt : 0;

        public string FormattedDate => !string.IsNullOrEmpty(EventTimestamp) && DateTime.TryParse(EventTimestamp, out var dt)
            ? dt.ToString("MMM dd, yyyy")
            : EventTimestamp;

        public string EventTypeDisplay => EventType switch
        {
            "RECHARGE_WALLET" => "Recharge",
            "REFUND_PROCESSED" => "Refund",
            "CASHBACK_ADDED" => "Cashback",
            "PROMO_BALANCE_ADDED" => "Promo",
            _ => EventType
        };

        public bool IsRefund => EventType == "REFUND_PROCESSED";

        public string AmountDisplay => IsRefund ? $"-${Amount:N2}" : $"+${Amount:N2}";
    }

    /// <summary>
    /// Recharge history API response wrapper
    /// </summary>
    public class RechargeHistoryResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("history")]
        public List<RechargeHistoryItem>? History { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("hasMore")]
        public bool HasMore { get; set; }
    }

    /// <summary>
    /// Payment method information
    /// </summary>
    public class PaymentMethod
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("brand")]
        public string Brand { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("last4")]
        public string Last4 { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("expMonth")]
        public int ExpMonth { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expYear")]
        public int ExpYear { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("isDefault")]
        public bool IsDefault { get; set; }

        public string DisplayName => $"{Brand} •••• {Last4}";
        public string ExpiryDisplay => $"{ExpMonth:D2}/{ExpYear}";
    }

    /// <summary>
    /// Payment methods API response
    /// </summary>
    public class PaymentMethodResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("paymentMethods")]
        public List<PaymentMethod>? PaymentMethods { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("defaultPaymentMethodId")]
        public string? DefaultPaymentMethodId { get; set; }
    }

    /// <summary>
    /// Result of a recharge operation
    /// </summary>
    public class RechargeResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Result of adding a payment method
    /// </summary>
    public class AddPaymentMethodResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? PaymentMethodId { get; set; }
    }

    /// <summary>
    /// Auto-recharge settings
    /// </summary>
    public class AutoRechargeSettings
    {
        [System.Text.Json.Serialization.JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("threshold")]
        public decimal Threshold { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("rechargeAmount")]
        public decimal RechargeAmount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("paymentMethodId")]
        public string? PaymentMethodId { get; set; }
    }

    /// <summary>
    /// Storage pricing tier
    /// </summary>
    public class StoragePricingTier
    {
        [System.Text.Json.Serialization.JsonPropertyName("tier")]
        public string Tier { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("sizeGB")]
        public int SizeGB { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pricePerMonth")]
        public decimal PricePerMonth { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pricePerHour")]
        public decimal PricePerHour { get; set; }

        public string DisplayName => $"{SizeGB} GB";
        public string PriceDisplay => $"${PricePerMonth:N2}/mo";
    }

    /// <summary>
    /// Storage pricing API response
    /// </summary>
    public class StoragePricingResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("tiers")]
        public List<StoragePricingTier>? Tiers { get; set; }
    }

    #endregion

    #region Support Models

    /// <summary>
    /// Support ticket model
    /// </summary>
    public class SupportTicket
    {
        [System.Text.Json.Serialization.JsonPropertyName("ticketId")]
        public string TicketId { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("subject")]
        public string Subject { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("priority")]
        public string Priority { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("lastUpdated")]
        public string? LastUpdated { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    /// <summary>
    /// Ticket message model
    /// </summary>
    public class TicketMessage
    {
        [System.Text.Json.Serialization.JsonPropertyName("messageId")]
        public string MessageId { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("senderId")]
        public string SenderId { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("senderType")]
        public string SenderType { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("senderName")]
        public string? SenderName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("content")]
        public string Content { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = "";
    }

    /// <summary>
    /// Tickets API response
    /// </summary>
    public class TicketsResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("tickets")]
        public List<SupportTicket>? Tickets { get; set; }
    }

    /// <summary>
    /// Ticket messages API response
    /// </summary>
    public class TicketMessagesResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("messages")]
        public List<TicketMessage>? Messages { get; set; }
    }

    #endregion
}
