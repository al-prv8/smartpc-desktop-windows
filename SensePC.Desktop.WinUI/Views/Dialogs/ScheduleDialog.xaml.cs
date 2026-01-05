using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SensePC.Desktop.WinUI.Models;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace SensePC.Desktop.WinUI.Views.Dialogs
{
    /// <summary>
    /// Dialog for configuring auto start/stop schedule - matches website functionality
    /// </summary>
    public sealed class ScheduleDialog : ContentDialog
    {
        private readonly PCInstance _pc;
        private readonly SensePCApiService _apiService;
        
        // UI elements
        private StackPanel _loadingSchedulePanel;
        private StackPanel _contentPanel;
        private ToggleSwitch _enableToggle;
        private ComboBox _timeZoneCombo;
        private ComboBox _frequencyCombo;
        private StackPanel _customDatePanel;
        private DatePicker _startDatePicker;
        private DatePicker _endDatePicker;
        private TimePicker _startTimePicker;
        private TimePicker _stopTimePicker;
        private CheckBox _clearStartTimeCheck;
        private CheckBox _clearStopTimeCheck;
        private StackPanel _savingPanel;
        private TextBlock _errorText;
        private Button _deleteButton;

        private ScheduleData? _existingSchedule;
        public bool ScheduleSaved { get; private set; }

        // Common time zones with friendly names
        private readonly List<TimeZoneOption> _timeZoneOptions;

        public ScheduleDialog(XamlRoot xamlRoot, PCInstance pc, SensePCApiService apiService)
        {
            this.XamlRoot = xamlRoot;
            _pc = pc;
            _apiService = apiService;

            // Initialize timezone options dynamically
            _timeZoneOptions = GetTimeZoneOptions();

            Title = "Schedule Your PC";
            PrimaryButtonText = "Save Schedule";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;
            IsPrimaryButtonEnabled = false;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
            Loaded += OnDialogLoaded;
        }

        private List<TimeZoneOption> GetTimeZoneOptions()
        {
            var options = new List<TimeZoneOption>();
            
            // Detect system timezone and add it first
            try
            {
                var localZone = TimeZoneInfo.Local;
                options.Add(new TimeZoneOption(localZone.Id, $"{localZone.DisplayName} (Your Time Zone)"));
            }
            catch { }

            // Common time zones
            var commonZones = new[]
            {
                ("UTC", "UTC (Coordinated Universal Time)"),
                ("America/New_York", "US Eastern (EST/EDT)"),
                ("America/Los_Angeles", "US Pacific (PST/PDT)"),
                ("America/Chicago", "US Central (CST/CDT)"),
                ("Europe/London", "UK (GMT/BST)"),
                ("Europe/Berlin", "Central Europe (CET/CEST)"),
                ("Asia/Kolkata", "India (IST)"),
                ("Asia/Dhaka", "Bangladesh (BST)"),
                ("Asia/Tokyo", "Japan (JST)"),
                ("Asia/Shanghai", "China (CST)"),
                ("Australia/Sydney", "Australia Eastern (AEST/AEDT)"),
            };

            foreach (var (id, name) in commonZones)
            {
                if (!options.Any(o => o.Id == id))
                {
                    options.Add(new TimeZoneOption(id, name));
                }
            }

            return options;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 420 };

            // Description
            mainStack.Children.Add(new TextBlock
            {
                Text = "Auto start/stop your PC on a schedule to save costs.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Loading schedule panel
            _loadingSchedulePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20)
            };
            _loadingSchedulePanel.Children.Add(new ProgressRing { IsActive = true, Width = 24, Height = 24 });
            _loadingSchedulePanel.Children.Add(new TextBlock 
            { 
                Text = "Loading schedule...", 
                VerticalAlignment = VerticalAlignment.Center 
            });
            mainStack.Children.Add(_loadingSchedulePanel);

            // Content panel (hidden during loading)
            _contentPanel = new StackPanel { Spacing = 16, Visibility = Visibility.Collapsed };

            // Enable toggle (only shown if existing schedule)
            _enableToggle = new ToggleSwitch
            {
                Header = "Schedule Status",
                OnContent = "Enabled",
                OffContent = "Disabled",
                IsOn = true,
                Visibility = Visibility.Collapsed
            };
            _contentPanel.Children.Add(_enableToggle);

            // Time Zone
            _timeZoneCombo = new ComboBox
            {
                Header = "Time Zone",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "Select time zone"
            };
            foreach (var tz in _timeZoneOptions)
            {
                _timeZoneCombo.Items.Add(new ComboBoxItem { Content = tz.DisplayName, Tag = tz.Id });
            }
            if (_timeZoneCombo.Items.Count > 0)
                _timeZoneCombo.SelectedIndex = 0;
            _contentPanel.Children.Add(_timeZoneCombo);

            // Frequency
            _frequencyCombo = new ComboBox
            {
                Header = "Frequency",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _frequencyCombo.Items.Add(new ComboBoxItem { Content = "Everyday", Tag = "everyday" });
            _frequencyCombo.Items.Add(new ComboBoxItem { Content = "Weekdays", Tag = "weekdays" });
            _frequencyCombo.Items.Add(new ComboBoxItem { Content = "Weekends", Tag = "weekends" });
            _frequencyCombo.Items.Add(new ComboBoxItem { Content = "Custom date range", Tag = "custom" });
            _frequencyCombo.SelectedIndex = 0;
            _frequencyCombo.SelectionChanged += OnFrequencyChanged;
            _contentPanel.Children.Add(_frequencyCombo);

            // Custom date range panel (hidden by default)
            _customDatePanel = new StackPanel { Spacing = 12, Visibility = Visibility.Collapsed };
            _customDatePanel.Children.Add(new TextBlock 
            { 
                Text = "Custom Date Range", 
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13
            });

            var dateGrid = new Grid();
            dateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dateGrid.ColumnSpacing = 12;

            var startDateStack = new StackPanel { Spacing = 4 };
            startDateStack.Children.Add(new TextBlock { Text = "Start Date", FontSize = 12 });
            _startDatePicker = new DatePicker { MinWidth = 140 };
            startDateStack.Children.Add(_startDatePicker);
            Grid.SetColumn(startDateStack, 0);
            dateGrid.Children.Add(startDateStack);

            var endDateStack = new StackPanel { Spacing = 4 };
            endDateStack.Children.Add(new TextBlock { Text = "End Date", FontSize = 12 });
            _endDatePicker = new DatePicker { MinWidth = 140 };
            endDateStack.Children.Add(_endDatePicker);
            Grid.SetColumn(endDateStack, 1);
            dateGrid.Children.Add(endDateStack);

            _customDatePanel.Children.Add(dateGrid);
            _contentPanel.Children.Add(_customDatePanel);

            // Auto Start Time
            var startTimeStack = new StackPanel { Spacing = 4 };
            startTimeStack.Children.Add(new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                Children = 
                {
                    new TextBlock { Text = "Auto Start Time", VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = " (optional)", FontSize = 11, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center }
                }
            });
            var startTimeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            _startTimePicker = new TimePicker
            {
                ClockIdentifier = "24HourClock",
                MinWidth = 140
            };
            startTimeRow.Children.Add(_startTimePicker);
            _clearStartTimeCheck = new CheckBox { Content = "No auto-start", VerticalAlignment = VerticalAlignment.Center };
            _clearStartTimeCheck.Checked += (s, e) => _startTimePicker.IsEnabled = false;
            _clearStartTimeCheck.Unchecked += (s, e) => _startTimePicker.IsEnabled = true;
            startTimeRow.Children.Add(_clearStartTimeCheck);
            startTimeStack.Children.Add(startTimeRow);
            _contentPanel.Children.Add(startTimeStack);

            // Auto Stop Time
            var stopTimeStack = new StackPanel { Spacing = 4 };
            stopTimeStack.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = "Auto Stop Time", VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = " (optional)", FontSize = 11, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center }
                }
            });
            var stopTimeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            _stopTimePicker = new TimePicker
            {
                ClockIdentifier = "24HourClock",
                MinWidth = 140
            };
            stopTimeRow.Children.Add(_stopTimePicker);
            _clearStopTimeCheck = new CheckBox { Content = "No auto-stop", VerticalAlignment = VerticalAlignment.Center };
            _clearStopTimeCheck.Checked += (s, e) => _stopTimePicker.IsEnabled = false;
            _clearStopTimeCheck.Unchecked += (s, e) => _stopTimePicker.IsEnabled = true;
            stopTimeRow.Children.Add(_clearStopTimeCheck);
            stopTimeStack.Children.Add(stopTimeRow);
            _contentPanel.Children.Add(stopTimeStack);

            // Delete button (only shown for existing schedules)
            _deleteButton = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE74D", FontSize = 14 },
                        new TextBlock { Text = "Delete Schedule" }
                    }
                },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 8, 0, 0)
            };
            _deleteButton.Click += DeleteButton_Click;
            _contentPanel.Children.Add(_deleteButton);

            mainStack.Children.Add(_contentPanel);

            // Saving panel
            _savingPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _savingPanel.Children.Add(new ProgressRing { IsActive = true, Width = 16, Height = 16 });
            _savingPanel.Children.Add(new TextBlock { Text = "Saving...", VerticalAlignment = VerticalAlignment.Center });
            mainStack.Children.Add(_savingPanel);

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

        private async void OnDialogLoaded(object sender, RoutedEventArgs e)
        {
            await LoadExistingScheduleAsync();
        }

        private async Task LoadExistingScheduleAsync()
        {
            try
            {
                _existingSchedule = await _apiService.GetScheduleAsync(_pc.InstanceId);

                if (_existingSchedule != null)
                {
                    // Schedule exists - populate form
                    _enableToggle.IsOn = _existingSchedule.Enabled;
                    _enableToggle.Visibility = Visibility.Visible;

                    // Set timezone
                    for (int i = 0; i < _timeZoneCombo.Items.Count; i++)
                    {
                        if (_timeZoneCombo.Items[i] is ComboBoxItem tzItem && 
                            tzItem.Tag?.ToString() == _existingSchedule.TimeZone)
                        {
                            _timeZoneCombo.SelectedIndex = i;
                            break;
                        }
                    }

                    // Set frequency
                    for (int i = 0; i < _frequencyCombo.Items.Count; i++)
                    {
                        if (_frequencyCombo.Items[i] is ComboBoxItem freqItem && 
                            freqItem.Tag?.ToString() == _existingSchedule.Frequency)
                        {
                            _frequencyCombo.SelectedIndex = i;
                            break;
                        }
                    }

                    // Set custom dates if available
                    if (!string.IsNullOrEmpty(_existingSchedule.StartDate) && 
                        DateTimeOffset.TryParse(_existingSchedule.StartDate, out var startDate))
                    {
                        _startDatePicker.SelectedDate = startDate;
                    }
                    if (!string.IsNullOrEmpty(_existingSchedule.EndDate) && 
                        DateTimeOffset.TryParse(_existingSchedule.EndDate, out var endDate))
                    {
                        _endDatePicker.SelectedDate = endDate;
                    }

                    // Set times
                    if (!string.IsNullOrEmpty(_existingSchedule.AutoStartTime) && 
                        TimeSpan.TryParse(_existingSchedule.AutoStartTime, out var startTime))
                    {
                        _startTimePicker.SelectedTime = startTime;
                        _clearStartTimeCheck.IsChecked = false;
                    }
                    else
                    {
                        _clearStartTimeCheck.IsChecked = true;
                    }

                    if (!string.IsNullOrEmpty(_existingSchedule.AutoStopTime) && 
                        TimeSpan.TryParse(_existingSchedule.AutoStopTime, out var stopTime))
                    {
                        _stopTimePicker.SelectedTime = stopTime;
                        _clearStopTimeCheck.IsChecked = false;
                    }
                    else
                    {
                        _clearStopTimeCheck.IsChecked = true;
                    }

                    _deleteButton.Visibility = Visibility.Visible;
                    PrimaryButtonText = "Update Schedule";
                }
                else
                {
                    // No existing schedule - use defaults
                    _startTimePicker.SelectedTime = new TimeSpan(9, 0, 0);
                    _stopTimePicker.SelectedTime = new TimeSpan(18, 0, 0);
                    PrimaryButtonText = "Create Schedule";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load schedule error: {ex.Message}");
                // Use defaults on error
                _startTimePicker.SelectedTime = new TimeSpan(9, 0, 0);
                _stopTimePicker.SelectedTime = new TimeSpan(18, 0, 0);
            }
            finally
            {
                _loadingSchedulePanel.Visibility = Visibility.Collapsed;
                _contentPanel.Visibility = Visibility.Visible;
                IsPrimaryButtonEnabled = true;
            }
        }

        private void OnFrequencyChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_frequencyCombo.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "custom")
            {
                _customDatePanel.Visibility = Visibility.Visible;
            }
            else
            {
                _customDatePanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var confirmDialog = new ContentDialog
            {
                Title = "Delete Schedule",
                Content = "Are you sure you want to delete this schedule?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;

            _savingPanel.Visibility = Visibility.Visible;
            _errorText.Visibility = Visibility.Collapsed;
            _deleteButton.IsEnabled = false;

            try
            {
                var success = await _apiService.DeleteScheduleAsync(_pc.InstanceId);
                if (success)
                {
                    ScheduleSaved = true;
                    this.Hide();
                }
                else
                {
                    _errorText.Text = "Failed to delete schedule.";
                    _errorText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                _errorText.Text = $"Error: {ex.Message}";
                _errorText.Visibility = Visibility.Visible;
            }
            finally
            {
                _savingPanel.Visibility = Visibility.Collapsed;
                _deleteButton.IsEnabled = true;
            }
        }

        private async void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var deferral = args.GetDeferral();

            try
            {
                _savingPanel.Visibility = Visibility.Visible;
                _errorText.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = false;

                var frequency = (_frequencyCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "everyday";
                var timeZone = (_timeZoneCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "UTC";

                var schedule = new ScheduleData
                {
                    InstanceId = _pc.InstanceId,
                    TimeZone = timeZone,
                    Frequency = frequency,
                    Enabled = _existingSchedule != null ? _enableToggle.IsOn : true
                };

                // Custom date range
                if (frequency == "custom")
                {
                    if (_startDatePicker.SelectedDate.HasValue)
                        schedule.StartDate = _startDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd");
                    if (_endDatePicker.SelectedDate.HasValue)
                        schedule.EndDate = _endDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd");
                }

                // Auto times (optional)
                if (_clearStartTimeCheck.IsChecked != true && _startTimePicker.SelectedTime.HasValue)
                {
                    schedule.AutoStartTime = _startTimePicker.SelectedTime.Value.ToString(@"hh\:mm");
                }

                if (_clearStopTimeCheck.IsChecked != true && _stopTimePicker.SelectedTime.HasValue)
                {
                    schedule.AutoStopTime = _stopTimePicker.SelectedTime.Value.ToString(@"hh\:mm");
                }

                var success = await _apiService.SaveScheduleAsync(_pc.InstanceId, schedule);

                if (success)
                {
                    ScheduleSaved = true;
                    // Dialog will close
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
                _savingPanel.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = true;
                deferral.Complete();
            }
        }
    }

    internal record TimeZoneOption(string Id, string DisplayName);
}
