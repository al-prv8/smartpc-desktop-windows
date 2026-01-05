using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SensePC.Desktop.WinUI.Views
{
    public sealed partial class TicketConversationPage : Page
    {
        private SensePCApiService _apiService;
        private string _ticketId = "";
        private SupportTicket? _ticket;
        private List<AttachmentFile> _attachments = new();
        private bool _isClosed = false;

        public TicketConversationPage()
        {
            this.InitializeComponent();
            _apiService = new SensePCApiService(new SecureStorage());
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string ticketId)
            {
                _ticketId = ticketId;
                TicketTitle.Text = $"Ticket #{ticketId}";
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadTicketDataAsync();
        }

        private async System.Threading.Tasks.Task LoadTicketDataAsync()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                _ticket = await _apiService.GetTicketByIdAsync(_ticketId);
                var messages = await _apiService.GetTicketMessagesAsync(_ticketId);

                LoadingOverlay.Visibility = Visibility.Collapsed;

                if (_ticket == null)
                {
                    await ShowErrorAsync("Unable to load ticket details.");
                    return;
                }

                // Update header
                TicketSubject.Text = _ticket.Subject ?? "No Subject";
                _isClosed = _ticket.Status?.ToLower() == "closed";

                // Update status badge
                if (_isClosed)
                {
                    StatusBadge.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(30, 150, 150, 150));
                    StatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray);
                }
                else
                {
                    StatusBadge.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(30, 16, 185, 129));
                    StatusText.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129));
                }
                StatusText.Text = _ticket.Status ?? "Open";
                CategoryText.Text = $"Category: {_ticket.Category}";
                PriorityText.Text = $"Priority: {_ticket.Priority}";
                CreatedText.Text = $"Created: {FormatDate(_ticket.CreatedAt)}";

                // Show description in info card
                if (!string.IsNullOrEmpty(_ticket.Description))
                {
                    DescriptionSection.Visibility = Visibility.Visible;
                    DescriptionText.Text = _ticket.Description;
                }
                else
                {
                    DescriptionSection.Visibility = Visibility.Collapsed;
                }

                // Show/hide reply section based on status
                ReplySection.Visibility = _isClosed ? Visibility.Collapsed : Visibility.Visible;
                ClosedNotice.Visibility = _isClosed ? Visibility.Visible : Visibility.Collapsed;

                // Display messages
                MessagesContainer.Children.Clear();

                // Then show conversation messages
                if (messages != null && messages.Count > 0)
                {
                    foreach (var msg in messages)
                    {
                        var isAgent = msg.SenderType?.ToLower() == "agent";
                        var sender = isAgent ? "Support Team" : "You";
                        var msgCard = CreateMessageCard(sender, msg.Content ?? "", msg.Timestamp, isAgent);
                        MessagesContainer.Children.Add(msgCard);
                    }
                }

                if (MessagesContainer.Children.Count == 0)
                {
                    MessagesContainer.Children.Add(new TextBlock
                    {
                        Text = "No messages yet",
                        Opacity = 0.6,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 32, 0, 32)
                    });
                }

                // Scroll to bottom
                MessagesScrollViewer.UpdateLayout();
                MessagesScrollViewer.ChangeView(null, MessagesScrollViewer.ScrollableHeight, null);
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                await ShowErrorAsync($"Error: {ex.Message}");
            }
        }

        private Border CreateMessageCard(string sender, string content, string timestamp, bool isAgent)
        {
            var card = new Border
            {
                Background = isAgent
                    ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(15, 16, 185, 129))
                    : (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
                BorderBrush = isAgent
                    ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(50, 16, 185, 129))
                    : (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0, 0, 0, 3),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16)
            };

            var stack = new StackPanel { Spacing = 8 };

            // Sender row with badge
            var senderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var senderBadge = new Border
            {
                Background = isAgent
                    ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 16, 185, 129))
                    : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 59, 130, 246)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2)
            };
            senderBadge.Child = new TextBlock
            {
                Text = isAgent ? "Support" : "You",
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = isAgent
                    ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129))
                    : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 59, 130, 246))
            };
            senderRow.Children.Add(senderBadge);
            senderRow.Children.Add(new TextBlock { Text = FormatDate(timestamp), FontSize = 11, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center });
            stack.Children.Add(senderRow);

            // Content
            stack.Children.Add(new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap });

            card.Child = stack;
            return card;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(SupportPage));
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadTicketDataAsync();
        }

        private async void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    if (_attachments.Count >= 2) break;
                    if (!_attachments.Any(a => a.Name == file.Name))
                    {
                        _attachments.Add(new AttachmentFile { Name = file.Name, Path = file.Path });
                    }
                }
                UpdateAttachmentsList();
            }
        }

        private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AttachmentFile file)
            {
                _attachments.Remove(file);
                UpdateAttachmentsList();
            }
        }

        private void UpdateAttachmentsList()
        {
            AttachmentsListControl.ItemsSource = null;
            AttachmentsListControl.ItemsSource = _attachments.ToList();
            AttachmentCountText.Text = _attachments.Count > 0 ? $"{_attachments.Count} file(s)" : "";
        }

        private async void SendReply_Click(object sender, RoutedEventArgs e)
        {
            var replyText = ReplyTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(replyText))
            {
                return;
            }

            try
            {
                SendReplyButton.IsEnabled = false;
                LoadingOverlay.Visibility = Visibility.Visible;

                var success = await _apiService.SendTicketMessageAsync(_ticketId, replyText);

                if (success)
                {
                    ReplyTextBox.Text = "";
                    _attachments.Clear();
                    UpdateAttachmentsList();
                    await LoadTicketDataAsync();
                }
                else
                {
                    await ShowErrorAsync("Failed to send reply. Please try again.");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Error: {ex.Message}");
            }
            finally
            {
                SendReplyButton.IsEnabled = true;
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private string FormatDate(string? dateString)
        {
            if (string.IsNullOrEmpty(dateString)) return "";
            if (DateTime.TryParse(dateString, out var date))
            {
                return date.ToString("MMM dd, yyyy h:mm tt");
            }
            return dateString;
        }

        private async System.Threading.Tasks.Task ShowErrorAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
