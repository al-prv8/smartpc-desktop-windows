using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace SensePC.Desktop.WinUI.Views.Dialogs
{
    /// <summary>
    /// Dialog for building new PC - redirects to web dashboard
    /// </summary>
    public sealed class BuildNewPCDialog : ContentDialog
    {
        public bool PCCreated { get; private set; }

        public BuildNewPCDialog(XamlRoot xamlRoot)
        {
            this.XamlRoot = xamlRoot;

            Title = "Build New PC";
            PrimaryButtonText = "Open Dashboard";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 400 };

            // Icon
            var icon = new FontIcon
            {
                Glyph = "\uE710", // Add icon
                FontSize = 48,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 95, 111, 255)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainStack.Children.Add(icon);

            // Title text
            var titleText = new TextBlock
            {
                Text = "Create a New Cloud PC",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainStack.Children.Add(titleText);

            // Description
            var descText = new TextBlock
            {
                Text = "To create a new PC with full customization options, you'll be redirected to the SensePC web dashboard where you can configure your PC specifications, region, and more.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8
            };
            mainStack.Children.Add(descText);

            // Info box
            var infoBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 95, 111, 255)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };
            var infoStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            infoStack.Children.Add(new FontIcon { Glyph = "\uE946", FontSize = 16 });
            infoStack.Children.Add(new TextBlock
            {
                Text = "After creating your PC, return here and refresh to see it.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            });
            infoBox.Child = infoStack;
            mainStack.Children.Add(infoBox);

            Content = mainStack;
        }

        private async void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                // Open the web dashboard
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://smartpc.cloud/dashboard/build-new"));
                PCCreated = true; // Signal that user may have created a PC
            }
            catch
            {
                // Ignore launch errors
            }
        }
    }
}
