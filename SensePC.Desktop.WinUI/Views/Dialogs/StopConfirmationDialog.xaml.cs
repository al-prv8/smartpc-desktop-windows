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
    /// Dialog for confirming PC stop action - built programmatically
    /// </summary>
    public sealed class StopConfirmationDialog : ContentDialog
    {
        private readonly PCInstance _pc;
        private readonly SensePCApiService _apiService;
        private StackPanel _loadingPanel;
        private TextBlock _errorText;

        public bool StopConfirmed { get; private set; }

        public StopConfirmationDialog(PCInstance pc, XamlRoot xamlRoot)
        {
            this.XamlRoot = xamlRoot;
            _pc = pc;
            _apiService = new SensePCApiService(new SecureStorage());

            Title = "Stop PC";
            PrimaryButtonText = "Stop";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Close;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 350 };

            // Warning icon
            var icon = new FontIcon
            {
                Glyph = "\uE7E8", // Stop icon
                FontSize = 48,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 170, 0)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainStack.Children.Add(icon);

            // Message with PC name
            var messageText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            messageText.Inlines.Add(new Run { Text = "Are you sure you want to stop " });
            messageText.Inlines.Add(new Run { Text = _pc.SystemName, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            messageText.Inlines.Add(new Run { Text = "?" });
            mainStack.Children.Add(messageText);

            // Info text
            var infoText = new TextBlock
            {
                Text = "The PC will be shut down and you will be disconnected from any active sessions.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                FontSize = 12
            };
            mainStack.Children.Add(infoText);

            // Loading panel
            _loadingPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _loadingPanel.Children.Add(new ProgressRing { IsActive = true, Width = 16, Height = 16 });
            _loadingPanel.Children.Add(new TextBlock { Text = "Stopping PC...", VerticalAlignment = VerticalAlignment.Center });
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

        private async void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var deferral = args.GetDeferral();

            try
            {
                _loadingPanel.Visibility = Visibility.Visible;
                _errorText.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = false;

                var result = await _apiService.StopVMAsync(_pc.InstanceId);

                if (result.StatusCode == 200 || result.StatusCode == 0 || result.StatusCode == null)
                {
                    StopConfirmed = true;
                }
                else
                {
                    args.Cancel = true;
                    _errorText.Text = result.Message ?? "Failed to stop PC. Please try again.";
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
                deferral.Complete();
            }
        }
    }
}
