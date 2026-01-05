using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SensePC.Desktop.WinUI.Models;
using SensePC.Desktop.WinUI.Services;
using System;
using Windows.UI;

namespace SensePC.Desktop.WinUI.Views.Dialogs
{
    /// <summary>
    /// Dialog for configuring idle timeout settings - built programmatically
    /// </summary>
    public sealed class IdleSettingsDialog : ContentDialog
    {
        private readonly PCInstance _pc;
        private readonly SensePCApiService _apiService;
        
        private ToggleSwitch _enableToggle;
        private StackPanel _timeoutContent;
        private RadioButtons _timeoutRadios;
        private StackPanel _loadingPanel;
        private TextBlock _errorText;

        public bool SettingsSaved { get; private set; }

        public IdleSettingsDialog(XamlRoot xamlRoot, PCInstance pc, SensePCApiService apiService)
        {
            this.XamlRoot = xamlRoot;
            _pc = pc;
            _apiService = apiService;

            Title = "Idle Timeout Settings";
            PrimaryButtonText = "Save";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 400 };

            // Description
            var descText = new TextBlock
            {
                Text = "Automatically stop your PC when idle to save costs.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8
            };
            mainStack.Children.Add(descText);

            // Enable toggle
            _enableToggle = new ToggleSwitch
            {
                Header = "Enable Idle Timeout",
                IsOn = false
            };
            _enableToggle.Toggled += (s, e) =>
            {
                _timeoutContent.Visibility = _enableToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            };
            mainStack.Children.Add(_enableToggle);

            // Timeout content (hidden by default)
            _timeoutContent = new StackPanel { Spacing = 12, Visibility = Visibility.Collapsed };

            var timeoutLabel = new TextBlock
            {
                Text = "Stop PC after being idle for:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            _timeoutContent.Children.Add(timeoutLabel);

            // Radio buttons for timeout options
            _timeoutRadios = new RadioButtons();
            _timeoutRadios.Items.Add(CreateRadioOption("15 minutes", 15));
            _timeoutRadios.Items.Add(CreateRadioOption("30 minutes", 30));
            _timeoutRadios.Items.Add(CreateRadioOption("1 hour", 60));
            _timeoutRadios.Items.Add(CreateRadioOption("2 hours", 120));
            _timeoutRadios.SelectedIndex = 1; // Default to 30 minutes
            _timeoutContent.Children.Add(_timeoutRadios);

            mainStack.Children.Add(_timeoutContent);

            // Loading panel
            _loadingPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _loadingPanel.Children.Add(new ProgressRing { IsActive = true, Width = 16, Height = 16 });
            _loadingPanel.Children.Add(new TextBlock { Text = "Saving settings...", VerticalAlignment = VerticalAlignment.Center });
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

        private RadioButton CreateRadioOption(string text, int minutes)
        {
            return new RadioButton
            {
                Content = text,
                Tag = minutes
            };
        }

        private async void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var deferral = args.GetDeferral();

            try
            {
                _loadingPanel.Visibility = Visibility.Visible;
                _errorText.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = false;

                int timeoutMinutes = 0;
                if (_enableToggle.IsOn && _timeoutRadios.SelectedItem is RadioButton selectedRadio)
                {
                    timeoutMinutes = (int)(selectedRadio.Tag ?? 30);
                }

                var success = await _apiService.SaveIdleSettingsAsync(_pc.InstanceId, timeoutMinutes);

                if (success)
                {
                    SettingsSaved = true;
                }
                else
                {
                    args.Cancel = true;
                    _errorText.Text = "Failed to save settings. Please try again.";
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
