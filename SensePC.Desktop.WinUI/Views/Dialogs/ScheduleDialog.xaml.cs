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
    /// Dialog for configuring auto start/stop schedule - built programmatically
    /// </summary>
    public sealed class ScheduleDialog : ContentDialog
    {
        private readonly PCInstance _pc;
        private readonly SensePCApiService _apiService;
        
        private ToggleSwitch _enableToggle;
        private StackPanel _scheduleContent;
        private ComboBox _frequencyCombo;
        private ComboBox _timeZoneCombo;
        private TimePicker _startTimePicker;
        private TimePicker _stopTimePicker;
        private StackPanel _loadingPanel;
        private TextBlock _errorText;

        public bool ScheduleSaved { get; private set; }

        public ScheduleDialog(XamlRoot xamlRoot, PCInstance pc, SensePCApiService apiService)
        {
            this.XamlRoot = xamlRoot;
            _pc = pc;
            _apiService = apiService;

            Title = "Schedule Settings";
            PrimaryButtonText = "Save";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 400 };

            // Enable toggle
            _enableToggle = new ToggleSwitch
            {
                Header = "Enable Auto Schedule",
                IsOn = false
            };
            _enableToggle.Toggled += (s, e) =>
            {
                _scheduleContent.Visibility = _enableToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            };
            mainStack.Children.Add(_enableToggle);

            // Schedule content (hidden by default)
            _scheduleContent = new StackPanel { Spacing = 16, Visibility = Visibility.Collapsed };

            // Frequency combo
            _frequencyCombo = new ComboBox { Header = "Frequency", HorizontalAlignment = HorizontalAlignment.Stretch };
            _frequencyCombo.Items.Add(new ComboBoxItem { Content = "Every Day", Tag = "everyday" });
            _frequencyCombo.Items.Add(new ComboBoxItem { Content = "Weekdays Only", Tag = "weekdays" });
            _frequencyCombo.Items.Add(new ComboBoxItem { Content = "Weekends Only", Tag = "weekends" });
            _frequencyCombo.SelectedIndex = 0;
            _scheduleContent.Children.Add(_frequencyCombo);

            // Time zone combo
            _timeZoneCombo = new ComboBox { Header = "Time Zone", HorizontalAlignment = HorizontalAlignment.Stretch };
            _timeZoneCombo.Items.Add(new ComboBoxItem { Content = "UTC", Tag = "UTC" });
            _timeZoneCombo.Items.Add(new ComboBoxItem { Content = "US Eastern (EST/EDT)", Tag = "America/New_York" });
            _timeZoneCombo.Items.Add(new ComboBoxItem { Content = "US Pacific (PST/PDT)", Tag = "America/Los_Angeles" });
            _timeZoneCombo.Items.Add(new ComboBoxItem { Content = "UK (GMT/BST)", Tag = "Europe/London" });
            _timeZoneCombo.Items.Add(new ComboBoxItem { Content = "India (IST)", Tag = "Asia/Kolkata" });
            _timeZoneCombo.Items.Add(new ComboBoxItem { Content = "Bangladesh (BST)", Tag = "Asia/Dhaka" });
            _timeZoneCombo.SelectedIndex = 0;
            _scheduleContent.Children.Add(_timeZoneCombo);

            // Start time picker
            _startTimePicker = new TimePicker
            {
                Header = "Auto Start Time",
                ClockIdentifier = "24HourClock",
                SelectedTime = new TimeSpan(9, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _scheduleContent.Children.Add(_startTimePicker);

            // Stop time picker
            _stopTimePicker = new TimePicker
            {
                Header = "Auto Stop Time",
                ClockIdentifier = "24HourClock",
                SelectedTime = new TimeSpan(18, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _scheduleContent.Children.Add(_stopTimePicker);

            // Info text
            var infoText = new TextBlock
            {
                Text = "Your PC will automatically start and stop at the specified times.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.6,
                FontSize = 12
            };
            _scheduleContent.Children.Add(infoText);

            mainStack.Children.Add(_scheduleContent);

            // Loading panel
            _loadingPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _loadingPanel.Children.Add(new ProgressRing { IsActive = true, Width = 16, Height = 16 });
            _loadingPanel.Children.Add(new TextBlock { Text = "Saving schedule...", VerticalAlignment = VerticalAlignment.Center });
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

                string? startTime = null;
                string? stopTime = null;
                string frequency = "everyday";
                string timeZone = "UTC";

                if (_enableToggle.IsOn)
                {
                    if (_startTimePicker.SelectedTime.HasValue)
                        startTime = _startTimePicker.SelectedTime.Value.ToString(@"hh\:mm");
                    if (_stopTimePicker.SelectedTime.HasValue)
                        stopTime = _stopTimePicker.SelectedTime.Value.ToString(@"hh\:mm");
                    if (_frequencyCombo.SelectedItem is ComboBoxItem freqItem)
                        frequency = freqItem.Tag?.ToString() ?? "everyday";
                    if (_timeZoneCombo.SelectedItem is ComboBoxItem tzItem)
                        timeZone = tzItem.Tag?.ToString() ?? "UTC";
                }

                var schedule = new ScheduleInfo
                {
                    Enabled = _enableToggle.IsOn,
                    AutoStartTime = startTime,
                    AutoStopTime = stopTime,
                    Frequency = frequency,
                    TimeZone = timeZone
                };

                var success = await _apiService.SaveScheduleAsync(_pc.InstanceId, schedule);

                if (success)
                {
                    ScheduleSaved = true;
                }
                else
                {
                    args.Cancel = true;
                    _errorText.Text = "Failed to save schedule. Please try again.";
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
