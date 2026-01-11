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
        private bool _isResolved = false;
        private bool _isUpdatingPriority = false;

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
                _isResolved = _ticket.Status?.ToLower() == "resolved";

                // Update status badges (header and info panel)
                UpdateStatusBadges(_ticket.Status ?? "Open");

                // Show description
                if (!string.IsNullOrEmpty(_ticket.Description))
                {
                    DescriptionSection.Visibility = Visibility.Visible;
                    DescriptionText.Text = _ticket.Description;
                }
                else
                {
                    DescriptionSection.Visibility = Visibility.Collapsed;
                }

                // Display ticket attachments
                if (_ticket.Attachments != null && _ticket.Attachments.Count > 0)
                {
                    TicketAttachmentsPanel.Visibility = Visibility.Visible;
                    DisplayTicketAttachments(_ticket.Attachments);
                }
                else
                {
                    TicketAttachmentsPanel.Visibility = Visibility.Collapsed;
                }

                // Update info panel
                EmailText.Text = _ticket.Email ?? "N/A";
                RoleText.Text = string.IsNullOrEmpty(_ticket.Role) ? "N/A" : CapitalizeFirst(_ticket.Role);
                TicketIdText.Text = _ticketId;
                CreatedText.Text = FormatDate(_ticket.CreatedAt);
                CategoryText.Text = CapitalizeFirst(_ticket.Category ?? "");

                // Set priority dropdown
                _isUpdatingPriority = true;
                SetPriorityComboSelection(_ticket.Priority?.ToLower() ?? "medium");
                _isUpdatingPriority = false;
                PriorityCombo.IsEnabled = !_isClosed;

                // Show/hide reply section and related elements
                if (_isClosed)
                {
                    ReplySection.Visibility = Visibility.Collapsed;
                    ClosedNotice.Visibility = Visibility.Visible;
                    ReopenWarning.Visibility = Visibility.Collapsed;
                    MarkResolvedButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ReplySection.Visibility = Visibility.Visible;
                    ClosedNotice.Visibility = Visibility.Collapsed;
                    ReopenWarning.Visibility = _isResolved ? Visibility.Visible : Visibility.Collapsed;
                    MarkResolvedButton.Visibility = _isResolved ? Visibility.Collapsed : Visibility.Visible;
                }

                // Display messages
                MessagesContainer.Children.Clear();

                if (messages != null && messages.Count > 0)
                {
                    foreach (var msg in messages)
                    {
                        var isAgent = msg.SenderType?.ToLower() == "agent";
                        var msgCard = CreateMessageCard(msg, isAgent);
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

        private void UpdateStatusBadges(string status)
        {
            var lowerStatus = status.ToLower();
            Windows.UI.Color bgColor;
            Windows.UI.Color fgColor;

            switch (lowerStatus)
            {
                case "closed":
                    bgColor = Windows.UI.Color.FromArgb(30, 150, 150, 150);
                    fgColor = Windows.UI.Color.FromArgb(255, 150, 150, 150);
                    break;
                case "resolved":
                    bgColor = Windows.UI.Color.FromArgb(30, 59, 130, 246);
                    fgColor = Windows.UI.Color.FromArgb(255, 59, 130, 246);
                    break;
                case "in-progress":
                    bgColor = Windows.UI.Color.FromArgb(30, 234, 179, 8);
                    fgColor = Windows.UI.Color.FromArgb(255, 234, 179, 8);
                    break;
                default: // open
                    bgColor = Windows.UI.Color.FromArgb(30, 16, 185, 129);
                    fgColor = Windows.UI.Color.FromArgb(255, 16, 185, 129);
                    break;
            }

            var bgBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B));
            var fgBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(fgColor.A, fgColor.R, fgColor.G, fgColor.B));

            // Header badge
            StatusBadge.Background = bgBrush;
            StatusText.Foreground = fgBrush;
            StatusText.Text = CapitalizeFirst(status);

            // Info panel badge
            InfoStatusBadge.Background = bgBrush;
            InfoStatusText.Foreground = fgBrush;
            InfoStatusText.Text = CapitalizeFirst(status);
        }

        private void SetPriorityComboSelection(string priority)
        {
            foreach (ComboBoxItem item in PriorityCombo.Items)
            {
                if (item.Tag?.ToString() == priority)
                {
                    PriorityCombo.SelectedItem = item;
                    return;
                }
            }
            // Default to medium
            PriorityCombo.SelectedIndex = 1;
        }

        private void DisplayTicketAttachments(List<TicketAttachment> attachments)
        {
            var attachmentPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var att in attachments)
            {
                var attCard = new Border
                {
                    Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 4, 8, 4)
                };
                var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                stack.Children.Add(new FontIcon { Glyph = "\uE7C3", FontSize = 12 });
                stack.Children.Add(new TextBlock { Text = att.Name, FontSize = 12 });
                
                var downloadBtn = new Button
                {
                    Content = new FontIcon { Glyph = "\uE896", FontSize = 12 },
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    Padding = new Thickness(4),
                    Tag = att
                };
                downloadBtn.Click += DownloadAttachment_Click;
                stack.Children.Add(downloadBtn);
                
                attCard.Child = stack;
                attachmentPanel.Children.Add(attCard);
            }
            TicketAttachmentsList.Items.Clear();
            TicketAttachmentsList.Items.Add(attachmentPanel);
        }

        private async void DownloadAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TicketAttachment attachment)
            {
                try
                {
                    var downloadUrl = await _apiService.PresignTicketDownloadAsync(_ticketId, attachment.FileKey);
                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        await Windows.System.Launcher.LaunchUriAsync(new Uri(downloadUrl));
                    }
                    else
                    {
                        await ShowErrorAsync("Failed to get download URL for attachment.");
                    }
                }
                catch (Exception ex)
                {
                    await ShowErrorAsync($"Download failed: {ex.Message}");
                }
            }
        }

        private Border CreateMessageCard(TicketMessage msg, bool isAgent)
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
            senderRow.Children.Add(new TextBlock { Text = FormatDate(msg.Timestamp), FontSize = 11, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center });
            stack.Children.Add(senderRow);

            // Content
            stack.Children.Add(new TextBlock { Text = msg.Content ?? "", TextWrapping = TextWrapping.Wrap });

            // Message attachments
            if (msg.Attachments != null && msg.Attachments.Count > 0)
            {
                var attPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
                foreach (var att in msg.Attachments)
                {
                    var attCard = new Border
                    {
                        Background = (Brush)Application.Current.Resources["ControlFillColorTertiaryBrush"],
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 4, 8, 4)
                    };
                    var attStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                    attStack.Children.Add(new FontIcon { Glyph = "\uE7C3", FontSize = 12 });
                    attStack.Children.Add(new TextBlock { Text = att.Name, FontSize = 12 });
                    
                    var dlBtn = new Button
                    {
                        Content = new FontIcon { Glyph = "\uE896", FontSize = 10 },
                        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                        Padding = new Thickness(2),
                        Tag = att
                    };
                    dlBtn.Click += DownloadAttachment_Click;
                    attStack.Children.Add(dlBtn);
                    
                    attCard.Child = attStack;
                    attPanel.Children.Add(attCard);
                }
                stack.Children.Add(attPanel);
            }

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

        private async void MarkResolved_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MarkResolvedButton.IsEnabled = false;
                LoadingOverlay.Visibility = Visibility.Visible;

                var success = await _apiService.UpdateTicketStatusAsync(_ticketId, "resolved");
                if (success)
                {
                    await ShowSuccessAsync("Ticket marked as resolved.");
                    await LoadTicketDataAsync();
                }
                else
                {
                    await ShowErrorAsync("Failed to update ticket status. Please try again.");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Error: {ex.Message}");
            }
            finally
            {
                MarkResolvedButton.IsEnabled = true;
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void PriorityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingPriority || _ticket == null) return;
            if (PriorityCombo.SelectedItem is ComboBoxItem item && item.Tag is string newPriority)
            {
                if (newPriority == _ticket.Priority?.ToLower()) return;

                try
                {
                    PriorityCombo.IsEnabled = false;
                    var success = await _apiService.UpdateTicketPriorityAsync(_ticketId, newPriority);
                    if (success)
                    {
                        _ticket.Priority = newPriority;
                        await ShowSuccessAsync($"Priority updated to {CapitalizeFirst(newPriority)}.");
                    }
                    else
                    {
                        await ShowErrorAsync("Failed to update priority.");
                        _isUpdatingPriority = true;
                        SetPriorityComboSelection(_ticket.Priority?.ToLower() ?? "medium");
                        _isUpdatingPriority = false;
                    }
                }
                catch (Exception ex)
                {
                    await ShowErrorAsync($"Error: {ex.Message}");
                }
                finally
                {
                    PriorityCombo.IsEnabled = !_isClosed;
                }
            }
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

                // Upload attachments if any
                var uploadedAttachments = new List<TicketAttachment>();
                foreach (var attachment in _attachments)
                {
                    try
                    {
                        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(attachment.Path);
                        var properties = await file.GetBasicPropertiesAsync();
                        var fileSize = (long)properties.Size;
                        var contentType = GetContentType(file.FileType);

                        // Get presigned upload URL
                        var uploadResponse = await _apiService.PresignTicketUploadAsync(_ticketId, file.Name, contentType, fileSize);
                        if (uploadResponse != null)
                        {
                            // Read file bytes
                            var buffer = await Windows.Storage.FileIO.ReadBufferAsync(file);
                            var bytes = new byte[buffer.Length];
                            using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(buffer))
                            {
                                reader.ReadBytes(bytes);
                            }

                            // Upload to presigned URL
                            var uploadSuccess = await _apiService.UploadToTicketPresignedUrlAsync(uploadResponse.UploadUrl, bytes, contentType);
                            if (uploadSuccess)
                            {
                                uploadedAttachments.Add(new TicketAttachment
                                {
                                    Name = file.Name,
                                    Size = $"{(fileSize / 1024.0 / 1024.0):F1} MB",
                                    Type = contentType,
                                    FileKey = uploadResponse.FileKey
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to upload attachment {attachment.Name}: {ex.Message}");
                    }
                }

                // Send the message with attachments
                var success = await _apiService.SendTicketMessageWithAttachmentsAsync(_ticketId, replyText, uploadedAttachments);

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

        private string GetContentType(string fileExtension)
        {
            return fileExtension.ToLower() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            };
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

        private string CapitalizeFirst(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return char.ToUpper(text[0]) + text.Substring(1).Replace("-", " ");
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

        private async System.Threading.Tasks.Task ShowSuccessAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Success",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
