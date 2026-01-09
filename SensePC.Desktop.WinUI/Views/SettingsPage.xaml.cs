using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace SensePC.Desktop.WinUI.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
        }

        private async void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Clear Cache",
                Content = "Are you sure you want to clear all cached data? This will not affect your files or settings.",
                PrimaryButtonText = "Clear Cache",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                // Clear cache logic here
                ClearCacheButton.IsEnabled = false;

                try
                {
                    // Simulate cache clearing
                    await Task.Delay(1000);

                    await ShowSuccessDialog("Cache Cleared", "Temporary files have been removed successfully.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Clear cache error: {ex.Message}");
                    await ShowErrorDialog("Clear Cache Failed", "Could not clear cache. Please try again.");
                }
                finally
                {
                    ClearCacheButton.IsEnabled = true;
                }
            }
        }

        private async void ExportDataButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Export Data",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = "Your data export will include:", TextWrapping = TextWrapping.Wrap },
                        new StackPanel
                        {
                            Spacing = 4,
                            Children =
                            {
                                new TextBlock { Text = "• Profile information" },
                                new TextBlock { Text = "• Account settings" },
                                new TextBlock { Text = "• Activity logs" },
                                new TextBlock { Text = "• PC configurations" }
                            }
                        },
                        new TextBlock 
                        { 
                            Text = "The export file will be downloaded to your Downloads folder.",
                            FontSize = 12,
                            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                        }
                    }
                },
                PrimaryButtonText = "Export",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ExportDataButton.IsEnabled = false;

                try
                {
                    // Simulate data export
                    await Task.Delay(2000);

                    await ShowSuccessDialog("Export Complete", "Your data has been exported to your Downloads folder.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Export data error: {ex.Message}");
                    await ShowErrorDialog("Export Failed", "Could not export your data. Please try again.");
                }
                finally
                {
                    ExportDataButton.IsEnabled = true;
                }
            }
        }

        private async void ResetDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Reset to Defaults",
                Content = "This will restore all settings to their default values. Your account data and files will not be affected.",
                PrimaryButtonText = "Reset",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                // Reset all settings to defaults
                LanguageComboBox.SelectedIndex = 0;
                TimeZoneComboBox.SelectedIndex = 3; // Eastern Time
                ActivityStatusToggle.IsOn = true;
                SoundEffectsToggle.IsOn = true;
                SessionTimeoutComboBox.SelectedIndex = 1; // 30 minutes
                AutoReconnectToggle.IsOn = true;

                DarkThemeRadio.IsChecked = true;
                BlueAccentRadio.IsChecked = true;
                ReducedMotionToggle.IsOn = false;
                ReduceTransparencyToggle.IsOn = false;

                EmailVerificationRadio.IsChecked = true;
                LoginNotificationsToggle.IsOn = true;
                TrustedDevicesToggle.IsOn = true;

                await ShowSuccessDialog("Settings Reset", "All settings have been restored to their default values.");
            }
        }

        private async void DeleteAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var firstDialog = new ContentDialog
            {
                Title = "Delete Account",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            Children =
                            {
                                new FontIcon { Glyph = "\uE7BA", FontSize = 24, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red) },
                                new TextBlock { Text = "This action is irreversible!", FontWeight = Microsoft.UI.Text.FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center }
                            }
                        },
                        new TextBlock 
                        { 
                            Text = "Deleting your account will permanently remove:", 
                            TextWrapping = TextWrapping.Wrap 
                        },
                        new StackPanel
                        {
                            Spacing = 4,
                            Children =
                            {
                                new TextBlock { Text = "• All your cloud storage files" },
                                new TextBlock { Text = "• All your smart PC configurations" },
                                new TextBlock { Text = "• Your profile and account settings" },
                                new TextBlock { Text = "• Access to your subscription" }
                            }
                        }
                    }
                },
                PrimaryButtonText = "Continue",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await firstDialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            // Second confirmation
            var confirmBox = new TextBox
            {
                PlaceholderText = "Type DELETE to confirm"
            };

            var secondDialog = new ContentDialog
            {
                Title = "Confirm Account Deletion",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = "To confirm deletion, please type DELETE below:",
                            TextWrapping = TextWrapping.Wrap 
                        },
                        confirmBox
                    }
                },
                PrimaryButtonText = "Delete My Account",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await secondDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                if (confirmBox.Text?.Trim().ToUpper() == "DELETE")
                {
                    // Account deletion would happen here
                    await ShowSuccessDialog("Account Scheduled for Deletion", 
                        "Your account has been scheduled for deletion. You will receive a confirmation email shortly.");
                }
                else
                {
                    await ShowErrorDialog("Confirmation Failed", "Please type DELETE exactly to confirm account deletion.");
                }
            }
        }

        private async Task ShowSuccessDialog(string title, string message)
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

        private async Task ShowErrorDialog(string title, string message)
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
    }
}
