using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.UI;

namespace SensePC.Desktop.WinUI.Views
{
    public sealed partial class NotificationsPage : Page
    {
        private readonly ISecureStorage _secureStorage;
        private readonly HttpClient _httpClient;
        private List<NotificationItem> _notifications = new();
        
        // Notification API URL from env.example
        private const string NOTIFICATION_API = "https://yns7wkdio7.execute-api.us-east-1.amazonaws.com/dev/";

        public NotificationsPage()
        {
            this.InitializeComponent();
            _secureStorage = new SecureStorage();
            _httpClient = new HttpClient();
            Loaded += NotificationsPage_Loaded;
        }

        private async void NotificationsPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadNotificationsAsync();
        }

        private async Task LoadNotificationsAsync()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;

            try
            {
                var idToken = await _secureStorage.GetAsync("id_token");
                if (string.IsNullOrEmpty(idToken))
                {
                    ShowEmptyState();
                    return;
                }

                var request = new HttpRequestMessage(HttpMethod.Get, NOTIFICATION_API);
                request.Headers.Add("Authorization", idToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<NotificationsResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    _notifications = result?.Notifications ?? new List<NotificationItem>();
                    UpdateUI();
                }
                else
                {
                    ShowEmptyState();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadNotifications error: {ex.Message}");
                ShowEmptyState();
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowEmptyState()
        {
            EmptyState.Visibility = Visibility.Visible;
            AllNotificationsPanel.Children.Clear();
            UnreadNotificationsPanel.Children.Clear();
            AlertsNotificationsPanel.Children.Clear();
        }

        private void UpdateUI()
        {
            // Clear existing items
            AllNotificationsPanel.Children.Clear();
            UnreadNotificationsPanel.Children.Clear();
            AlertsNotificationsPanel.Children.Clear();

            if (_notifications.Count == 0)
            {
                ShowEmptyState();
                return;
            }

            EmptyState.Visibility = Visibility.Collapsed;

            // Filter lists
            var unread = _notifications.Where(n => !n.IsRead).ToList();
            var alerts = _notifications.Where(n => n.Severity != "info").ToList();

            // Update badges
            UpdateBadge(AllCountBadge, AllCountText, _notifications.Count);
            UpdateBadge(UnreadCountBadge, UnreadCountText, unread.Count);
            UpdateBadge(AlertsCountBadge, AlertsCountText, alerts.Count);

            // Populate panels
            foreach (var notification in _notifications)
            {
                AllNotificationsPanel.Children.Add(CreateNotificationCard(notification));
            }

            foreach (var notification in unread)
            {
                UnreadNotificationsPanel.Children.Add(CreateNotificationCard(notification));
            }

            foreach (var notification in alerts)
            {
                AlertsNotificationsPanel.Children.Add(CreateNotificationCard(notification));
            }
        }

        private void UpdateBadge(Border badge, TextBlock text, int count)
        {
            if (count > 0)
            {
                badge.Visibility = Visibility.Visible;
                text.Text = count.ToString();
            }
            else
            {
                badge.Visibility = Visibility.Collapsed;
            }
        }

        private Border CreateNotificationCard(NotificationItem notification)
        {
            var card = new Border
            {
                Background = notification.IsRead
                    ? (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]
                    : (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                BorderBrush = GetSeverityBrush(notification.Severity),
                BorderThickness = new Thickness(notification.IsRead ? 1 : 2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Tag = notification
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Severity Icon
            var iconBorder = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = GetSeverityBrush(notification.Severity),
                Margin = new Thickness(0, 0, 12, 0)
            };

            var icon = new FontIcon
            {
                Glyph = GetSeverityIcon(notification.Severity),
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = icon;
            Grid.SetColumn(iconBorder, 0);

            // Content
            var contentPanel = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };

            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var title = new TextBlock
            {
                Text = notification.Title ?? "Notification",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            titlePanel.Children.Add(title);

            if (!notification.IsRead)
            {
                var unreadDot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
                };
                titlePanel.Children.Add(unreadDot);
            }
            contentPanel.Children.Add(titlePanel);

            var content = new TextBlock
            {
                Text = notification.Content ?? "",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2
            };
            contentPanel.Children.Add(content);

            var timeText = new TextBlock
            {
                Text = GetTimeAgo(notification.Timestamp),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = GetSeverityBrush(notification.Severity),
                Margin = new Thickness(0, 4, 0, 0)
            };
            contentPanel.Children.Add(timeText);

            Grid.SetColumn(contentPanel, 1);

            // Dismiss button
            var dismissButton = new Button
            {
                Content = new FontIcon { Glyph = "\uE711", FontSize = 12 },
                Padding = new Thickness(6),
                Background = new SolidColorBrush(Colors.Transparent),
                VerticalAlignment = VerticalAlignment.Top,
                Tag = notification
            };
            dismissButton.Click += DismissButton_Click;
            Grid.SetColumn(dismissButton, 2);

            grid.Children.Add(iconBorder);
            grid.Children.Add(contentPanel);
            grid.Children.Add(dismissButton);

            card.Child = grid;

            System.Diagnostics.Debug.WriteLine($"Created notification card: {notification.Title}");
            return card;
        }

        private Brush GetSeverityBrush(string? severity)
        {
            return severity?.ToLower() switch
            {
                "critical" => new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)), // Red
                "warning" => new SolidColorBrush(Color.FromArgb(255, 245, 158, 11)), // Amber
                _ => new SolidColorBrush(Color.FromArgb(255, 34, 197, 94)) // Green for info
            };
        }

        private string GetSeverityIcon(string? severity)
        {
            return severity?.ToLower() switch
            {
                "critical" => "\uE783", // Error
                "warning" => "\uE7BA", // Warning
                _ => "\uE73E" // Checkmark for info
            };
        }

        private string GetTimeAgo(string? timestamp)
        {
            if (string.IsNullOrEmpty(timestamp)) return "";

            if (DateTime.TryParse(timestamp, out var date))
            {
                var diff = DateTime.UtcNow - date;
                if (diff.TotalMinutes < 60)
                    return $"{(int)diff.TotalMinutes} min ago";
                if (diff.TotalHours < 24)
                    return $"{(int)diff.TotalHours} hr ago";
                if (diff.TotalDays < 7)
                    return $"{(int)diff.TotalDays} days ago";
                return date.ToString("MMM d");
            }
            return "";
        }

        private async void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is NotificationItem notification)
            {
                await MarkAsReadAsync(notification.Timestamp);
            }
        }

        private async void MarkAllReadButton_Click(object sender, RoutedEventArgs e)
        {
            var unread = _notifications.Where(n => !n.IsRead).ToList();
            if (unread.Count == 0) return;

            MarkAllReadButton.IsEnabled = false;

            try
            {
                var timestamps = unread.Select(n => n.Timestamp).Where(t => !string.IsNullOrEmpty(t)).ToList();
                if (timestamps.Count > 0)
                {
                    await MarkAsReadAsync(timestamps!);
                }
            }
            finally
            {
                MarkAllReadButton.IsEnabled = true;
            }
        }

        private async Task MarkAsReadAsync(string? timestamp)
        {
            if (string.IsNullOrEmpty(timestamp)) return;
            await MarkAsReadAsync(new List<string> { timestamp });
        }

        private async Task MarkAsReadAsync(List<string> timestamps)
        {
            try
            {
                var idToken = await _secureStorage.GetAsync("id_token");
                if (string.IsNullOrEmpty(idToken)) return;

                var payload = new { timestamps };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(new HttpMethod("PATCH"), NOTIFICATION_API)
                {
                    Content = content
                };
                request.Headers.Add("Authorization", idToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    // Update local state
                    foreach (var ts in timestamps)
                    {
                        var notification = _notifications.FirstOrDefault(n => n.Timestamp == ts);
                        if (notification != null)
                        {
                            notification.IsRead = true;
                        }
                    }
                    UpdateUI();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MarkAsRead error: {ex.Message}");
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadNotificationsAsync();
        }
    }

    // Models
    public class NotificationsResponse
    {
        public List<NotificationItem>? Notifications { get; set; }
        public string? NextToken { get; set; }
    }

    public class NotificationItem
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Severity { get; set; } // "info", "warning", "critical"
        public string? Timestamp { get; set; }
        public bool IsRead { get; set; }
        public string? Route { get; set; }
    }
}
