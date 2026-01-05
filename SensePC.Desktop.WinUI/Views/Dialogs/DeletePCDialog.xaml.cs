using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using SensePC.Desktop.WinUI.Models;
using SensePC.Desktop.WinUI.Services;
using System;
using Windows.UI;

namespace SensePC.Desktop.WinUI.Views.Dialogs
{
    /// <summary>
    /// Dialog for permanently deleting a PC - built programmatically
    /// </summary>
    public sealed class DeletePCDialog : ContentDialog
    {
        private readonly PCInstance _pc;
        private readonly SensePCApiService _apiService;
        
        private TextBox _confirmationTextBox;
        private StackPanel _loadingPanel;
        private TextBlock _errorText;

        public bool PCDeleted { get; private set; }

        public DeletePCDialog(XamlRoot xamlRoot, PCInstance pc, SensePCApiService apiService)
        {
            this.XamlRoot = xamlRoot;
            _pc = pc;
            _apiService = apiService;

            Title = "Delete PC";
            PrimaryButtonText = "Delete Permanently";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Close;
            IsPrimaryButtonEnabled = false;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 400 };

            // Warning icon
            var iconStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var icon = new FontIcon
            {
                Glyph = "\uE74D", // Warning icon
                FontSize = 48,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 68, 68))
            };
            iconStack.Children.Add(icon);
            mainStack.Children.Add(iconStack);

            // Warning heading
            var warningHeading = new TextBlock
            {
                Text = "This action cannot be undone!",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 68, 68)),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 16
            };
            mainStack.Children.Add(warningHeading);

            // PC name
            var pcNameText = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            pcNameText.Inlines.Add(new Run { Text = "You are about to permanently delete " });
            pcNameText.Inlines.Add(new Run { Text = _pc.SystemName, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
            mainStack.Children.Add(pcNameText);

            // Consequences box
            var consequencesBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 68, 68)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 8, 0, 8)
            };
            var consequencesStack = new StackPanel { Spacing = 4 };
            consequencesStack.Children.Add(new TextBlock { Text = "This will permanently:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            consequencesStack.Children.Add(CreateBulletPoint("• Delete all files and data on the PC"));
            consequencesStack.Children.Add(CreateBulletPoint("• Remove all installed applications"));
            consequencesStack.Children.Add(CreateBulletPoint("• End all active sessions"));
            consequencesStack.Children.Add(CreateBulletPoint("• Remove any scheduled tasks"));
            consequencesBox.Child = consequencesStack;
            mainStack.Children.Add(consequencesBox);

            // Confirmation section
            var confirmLabel = new TextBlock { TextWrapping = TextWrapping.Wrap };
            confirmLabel.Inlines.Add(new Run { Text = "To confirm, type \"" });
            confirmLabel.Inlines.Add(new Run { Text = _pc.SystemName, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
            confirmLabel.Inlines.Add(new Run { Text = "\" below:" });
            mainStack.Children.Add(confirmLabel);

            _confirmationTextBox = new TextBox
            {
                PlaceholderText = "Enter PC name to confirm",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _confirmationTextBox.TextChanged += (s, e) =>
            {
                IsPrimaryButtonEnabled = _confirmationTextBox.Text.Equals(_pc.SystemName, StringComparison.OrdinalIgnoreCase);
            };
            mainStack.Children.Add(_confirmationTextBox);

            // Loading panel
            _loadingPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _loadingPanel.Children.Add(new ProgressRing { IsActive = true, Width = 16, Height = 16 });
            _loadingPanel.Children.Add(new TextBlock { Text = "Deleting PC...", VerticalAlignment = VerticalAlignment.Center });
            mainStack.Children.Add(_loadingPanel);

            // Error text
            _errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 68, 68)),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            mainStack.Children.Add(_errorText);

            Content = mainStack;
        }

        private TextBlock CreateBulletPoint(string text)
        {
            return new TextBlock
            {
                Text = text,
                Opacity = 0.9,
                FontSize = 12,
                Margin = new Thickness(8, 0, 0, 0)
            };
        }

        private async void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (!_confirmationTextBox.Text.Equals(_pc.SystemName, StringComparison.OrdinalIgnoreCase))
            {
                args.Cancel = true;
                _errorText.Text = "PC name doesn't match. Please type the name exactly.";
                _errorText.Visibility = Visibility.Visible;
                return;
            }

            var deferral = args.GetDeferral();

            try
            {
                _loadingPanel.Visibility = Visibility.Visible;
                _errorText.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = false;
                _confirmationTextBox.IsEnabled = false;

                var success = await _apiService.DeleteVMAsync(_pc.InstanceId, _pc.Region);

                if (success)
                {
                    PCDeleted = true;
                }
                else
                {
                    args.Cancel = true;
                    _errorText.Text = "Failed to delete PC. Please try again.";
                    _errorText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                args.Cancel = true;
                _errorText.Text = $"Error: {ex.Message}";
                _errorText.Visibility = Visibility.Visible;
            }
            finally
            {
                _loadingPanel.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = true;
                _confirmationTextBox.IsEnabled = true;
                deferral.Complete();
            }
        }
    }
}
