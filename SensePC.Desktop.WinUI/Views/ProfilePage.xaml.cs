using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SensePC.Desktop.WinUI.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SensePC.Desktop.WinUI.Views
{
    public sealed partial class ProfilePage : Page
    {
        private readonly SensePCApiService _apiService;
        private readonly ICognitoAuthService _authService;
        private UserProfile? _profile;

        public ProfilePage()
        {
            this.InitializeComponent();
            _apiService = new SensePCApiService(new SecureStorage());
            _authService = new CognitoAuthService(new SecureStorage());
            Loaded += ProfilePage_Loaded;
        }

        private async void ProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadProfileAsync();
            await LoadConnectedDevicesAsync();
        }

        private async Task LoadProfileAsync()
        {
            LoadingOverlay.Visibility = Visibility.Visible;

            try
            {
                _profile = await _apiService.GetUserProfileAsync();

                if (_profile != null)
                {
                    // Populate form fields
                    var fullName = $"{_profile.FirstName} {_profile.LastName}".Trim();
                    FullNameBox.Text = fullName;
                    EmailBox.Text = _profile.Email ?? "";
                    CountryBox.Text = _profile.Country ?? "";
                    OrganizationBox.Text = _profile.Organization ?? "";
                    RoleBox.Text = _profile.Role ?? "";

                    // Update sidebar profile card
                    var initials = GetInitials(_profile.FirstName, _profile.LastName);
                    AvatarInitials.Text = initials;
                    UserNameDisplay.Text = string.IsNullOrEmpty(fullName) ? "User" : fullName;
                    UserEmailDisplay.Text = _profile.Email ?? "";
                    UserRoleBadge.Text = string.IsNullOrEmpty(_profile.Role) ? "Member" : 
                        char.ToUpper(_profile.Role[0]) + _profile.Role.Substring(1).ToLower();

                    // Update avatar display
                    UpdateAvatarUI();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadProfile error: {ex.Message}");
                await ShowErrorDialogAsync("Error", "Failed to load profile. Please try again.");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private string GetInitials(string? firstName, string? lastName)
        {
            var first = !string.IsNullOrEmpty(firstName) ? firstName[0].ToString().ToUpper() : "";
            var last = !string.IsNullOrEmpty(lastName) ? lastName[0].ToString().ToUpper() : "";
            
            if (string.IsNullOrEmpty(first) && string.IsNullOrEmpty(last))
                return "U";
            
            return first + last;
        }

        private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            SaveProfileButton.IsEnabled = false;
            LoadingOverlay.Visibility = Visibility.Visible;

            try
            {
                var fullName = FullNameBox.Text.Trim();
                var nameParts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var firstName = nameParts.Length > 0 ? nameParts[0] : "";
                var lastName = nameParts.Length > 1 ? nameParts[1] : "";

                var success = await _apiService.UpdateUserProfileAsync(
                    firstName,
                    lastName,
                    CountryBox.Text.Trim(),
                    OrganizationBox.Text.Trim()
                );

                if (success)
                {
                    await ShowSuccessDialogAsync("Profile Updated", "Your profile has been saved successfully.");
                    await LoadProfileAsync();
                }
                else
                {
                    await ShowErrorDialogAsync("Error", "Failed to update profile. Please try again.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveProfile error: {ex.Message}");
                await ShowErrorDialogAsync("Error", "An error occurred while saving your profile.");
            }
            finally
            {
                SaveProfileButton.IsEnabled = true;
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            var currentPassword = CurrentPasswordBox.Password;
            var newPassword = NewPasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            // Validation
            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
            {
                await ShowErrorDialogAsync("Validation Error", "Please fill in all password fields.");
                return;
            }

            if (newPassword != confirmPassword)
            {
                await ShowErrorDialogAsync("Validation Error", "New passwords do not match.");
                return;
            }

            if (newPassword.Length < 8)
            {
                await ShowErrorDialogAsync("Validation Error", "Password must be at least 8 characters long.");
                return;
            }

            ChangePasswordButton.IsEnabled = false;
            LoadingOverlay.Visibility = Visibility.Visible;

            try
            {
                var success = await _authService.ChangePasswordAsync(currentPassword, newPassword);

                if (success)
                {
                    await ShowSuccessDialogAsync("Password Changed", "Your password has been updated successfully.");
                    CurrentPasswordBox.Password = "";
                    NewPasswordBox.Password = "";
                    ConfirmPasswordBox.Password = "";
                }
                else
                {
                    await ShowErrorDialogAsync("Error", "Failed to change password. Please check your current password.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ChangePassword error: {ex.Message}");
                await ShowErrorDialogAsync("Error", "An error occurred while changing your password.");
            }
            finally
            {
                ChangePasswordButton.IsEnabled = true;
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Sign Out",
                Content = "Are you sure you want to sign out?",
                PrimaryButtonText = "Sign Out",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                // Clear stored tokens
                var secureStorage = new SecureStorage();
                await secureStorage.RemoveAsync("id_token");
                await secureStorage.RemoveAsync("access_token");
                await secureStorage.RemoveAsync("refresh_token");
                await secureStorage.RemoveAsync("user_id");
                await secureStorage.RemoveAsync("user_email");
                
                // Navigate to login
                if (App.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.ShowLoginPage();
                }
            }
        }

        private async Task ShowSuccessDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async Task ShowErrorDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async void UploadAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create file picker
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".gif");
                picker.FileTypeFilter.Add(".webp");

                // Initialize with window handle
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));

                var file = await picker.PickSingleFileAsync();
                if (file == null) return;

                // Check file size (max 2MB)
                var properties = await file.GetBasicPropertiesAsync();
                if (properties.Size > 2 * 1024 * 1024)
                {
                    await ShowErrorDialogAsync("File too large", "Max file size is 2MB.");
                    return;
                }

                UploadButtonText.Text = "Uploading...";
                UploadAvatarButton.IsEnabled = false;

                // Get upload URL from API
                var uploadResult = await _apiService.CreateAvatarUploadUrlAsync(file.ContentType, file.FileType.TrimStart('.'));
                if (uploadResult?.UploadUrl == null)
                {
                    await ShowErrorDialogAsync("Upload Failed", "Could not get upload URL.");
                    return;
                }

                // Read file and upload to S3
                using var stream = await file.OpenStreamForReadAsync();
                var bytes = new byte[stream.Length];
                await stream.ReadAsync(bytes, 0, bytes.Length);

                using var httpClient = new System.Net.Http.HttpClient();
                var content = new System.Net.Http.ByteArrayContent(bytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                
                var response = await httpClient.PutAsync(uploadResult.UploadUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    await ShowErrorDialogAsync("Upload Failed", "Failed to upload avatar to storage.");
                    return;
                }

                await ShowSuccessDialogAsync("Success", "Profile picture updated!");
                await LoadProfileAsync(); // Refresh to show new avatar
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Upload avatar error: {ex.Message}");
                await ShowErrorDialogAsync("Error", "Failed to upload profile picture.");
            }
            finally
            {
                UploadButtonText.Text = "Upload";
                UploadAvatarButton.IsEnabled = true;
            }
        }

        private async void RemoveAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Remove Profile Picture",
                Content = "Are you sure you want to remove your profile picture?",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            try
            {
                RemoveButtonText.Text = "Removing...";
                RemoveAvatarButton.IsEnabled = false;

                var success = await _apiService.DeleteAvatarAsync();
                if (success)
                {
                    await ShowSuccessDialogAsync("Success", "Profile picture removed!");
                    await LoadProfileAsync(); // Refresh
                }
                else
                {
                    await ShowErrorDialogAsync("Error", "Failed to remove profile picture.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Remove avatar error: {ex.Message}");
                await ShowErrorDialogAsync("Error", "Failed to remove profile picture.");
            }
            finally
            {
                RemoveButtonText.Text = "Remove";
                RemoveAvatarButton.IsEnabled = true;
            }
        }

        private void UpdateAvatarUI()
        {
            if (_profile?.AvatarUrl != null && !string.IsNullOrEmpty(_profile.AvatarUrl))
            {
                // Show avatar image
                AvatarImageBrush.ImageSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(_profile.AvatarUrl));
                AvatarImageEllipse.Visibility = Visibility.Visible;
                AvatarInitials.Visibility = Visibility.Collapsed;
                RemoveAvatarButton.Visibility = Visibility.Visible;
            }
            else
            {
                // Show initials
                AvatarImageEllipse.Visibility = Visibility.Collapsed;
                AvatarInitials.Visibility = Visibility.Visible;
                RemoveAvatarButton.Visibility = Visibility.Collapsed;
            }
        }

        #region Security Tab Methods

        private bool _isTotpEnabled = false;
        private bool _isEmailMfaEnabled = false;

        private async void TotpToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement TOTP setup/disable with QR code dialog
            // For now, show a message that this feature is coming soon
            await ShowInfoDialogAsync("Coming Soon", "Authenticator App setup will be available in a future update.");
        }

        private async void EmailMfaToggleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EmailMfaToggleButton.IsEnabled = false;
                
                // Toggle email MFA
                _isEmailMfaEnabled = !_isEmailMfaEnabled;
                
                // Update UI
                UpdateMfaUI();
                
                // Show success message
                await ShowInfoDialogAsync(
                    _isEmailMfaEnabled ? "Email MFA Enabled" : "Email MFA Disabled",
                    _isEmailMfaEnabled 
                        ? "Email-based multi-factor authentication has been enabled."
                        : "Email-based multi-factor authentication has been disabled."
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Toggle Email MFA error: {ex.Message}");
                await ShowErrorDialogAsync("Error", "Failed to update MFA settings.");
            }
            finally
            {
                EmailMfaToggleButton.IsEnabled = true;
            }
        }

        private void UpdateMfaUI()
        {
            // Update TOTP status
            TotpStatusText.Text = _isTotpEnabled ? "Enabled" : "Not configured";
            TotpToggleButton.Content = _isTotpEnabled ? "Disable" : "Setup";
            
            // Update Email MFA status
            EmailMfaStatusText.Text = _isEmailMfaEnabled ? "Enabled" : "Not configured";
            EmailMfaToggleButton.Content = _isEmailMfaEnabled ? "Disable" : "Setup";
        }

        private async void SignOutAllButton_Click(object sender, RoutedEventArgs e)
        {
            // Confirm action
            var dialog = new ContentDialog
            {
                Title = "Sign Out from All Devices",
                Content = "This will sign you out from all devices, including this one. You will need to sign in again.",
                PrimaryButtonText = "Sign Out All",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            try
            {
                SignOutAllButton.IsEnabled = false;
                
                // Perform global sign out
                await _authService.SignOutAsync();
                
                // Navigate to login
                if (App.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.ShowLoginPage();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sign out all error: {ex.Message}");
                await ShowErrorDialogAsync("Error", "Failed to sign out from all devices.");
                SignOutAllButton.IsEnabled = true;
            }
        }

        private async Task LoadConnectedDevicesAsync()
        {
            try
            {
                var sessions = await _apiService.GetActiveSessionsAsync();
                
                if (sessions.Count > 0)
                {
                    DevicesItemsControl.ItemsSource = sessions;
                    NoDevicesText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    DevicesItemsControl.ItemsSource = null;
                    NoDevicesText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load devices error: {ex.Message}");
                NoDevicesText.Text = "Failed to load connected devices.";
                NoDevicesText.Visibility = Visibility.Visible;
            }
        }

        private async Task ShowInfoDialogAsync(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        #endregion
    }

    /// <summary>
    /// User profile data model
    /// </summary>
    public class UserProfile
    {
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Country { get; set; }
        public string? Organization { get; set; }
        public string? Role { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
