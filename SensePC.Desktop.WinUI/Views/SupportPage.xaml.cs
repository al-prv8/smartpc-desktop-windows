using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SensePC.Desktop.WinUI.Views
{
    public sealed partial class SupportPage : Page
    {
        private readonly SensePCApiService _apiService;
        private List<TicketViewModel> _allTickets = new();
        private List<FAQCategory> _faqCategories = new();
        private bool _sortAscending = false;

        public SupportPage()
        {
            this.InitializeComponent();
            _apiService = new SensePCApiService(new SecureStorage());
            this.Loaded += SupportPage_Loaded;
        }

        private async void SupportPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadFAQData();
                await LoadTicketsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SupportPage_Loaded error: {ex.Message}");
            }
        }

        #region Tickets Tab

        private async System.Threading.Tasks.Task LoadTicketsAsync()
        {
            try
            {
                TicketsLoadingRing.IsActive = true;
                NoTicketsPanel.Visibility = Visibility.Collapsed;

                var tickets = await _apiService.GetTicketsAsync();
                
                if (tickets != null && tickets.Count > 0)
                {
                    _allTickets = tickets.Select(t => new TicketViewModel
                    {
                        TicketId = t.TicketId ?? "",
                        Subject = t.Subject ?? "",
                        Status = t.Status ?? "Open",
                        CreatedAt = t.CreatedAt ?? "",
                        CreatedAtDisplay = FormatDate(t.CreatedAt)
                    }).ToList();
                    
                    ApplyTicketFilters();
                }
                else
                {
                    TicketsListView.ItemsSource = null;
                    NoTicketsPanel.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadTicketsAsync error: {ex.Message}");
                NoTicketsPanel.Visibility = Visibility.Visible;
            }
            finally
            {
                TicketsLoadingRing.IsActive = false;
            }
        }

        private void ApplyTicketFilters()
        {
            if (_allTickets.Count == 0)
            {
                TicketsListView.ItemsSource = null;
                NoTicketsPanel.Visibility = Visibility.Visible;
                return;
            }

            var filtered = _allTickets.AsEnumerable();
            
            var searchText = TicketSearchBox?.Text?.ToLower() ?? "";
            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(t => 
                    t.TicketId.ToLower().Contains(searchText) || 
                    t.Subject.ToLower().Contains(searchText));
            }
            
            if (TicketStatusFilter?.SelectedItem is ComboBoxItem statusItem)
            {
                var status = statusItem.Content?.ToString();
                if (!string.IsNullOrEmpty(status) && status != "Any")
                {
                    filtered = filtered.Where(t => t.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
                }
            }
            
            filtered = _sortAscending 
                ? filtered.OrderBy(t => t.CreatedAt)
                : filtered.OrderByDescending(t => t.CreatedAt);
            
            var resultList = filtered.ToList();
            TicketsListView.ItemsSource = resultList;
            NoTicketsPanel.Visibility = resultList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TicketSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            ApplyTicketFilters();
        }

        private void TicketStatusFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_allTickets.Count > 0) ApplyTicketFilters();
        }

        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            _sortAscending = !_sortAscending;
            SortIcon.Glyph = _sortAscending ? "\uE74B" : "\uE74A";
            ApplyTicketFilters();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadTicketsAsync();
        }

        private void Ticket_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TicketViewModel ticket)
            {
                System.Diagnostics.Debug.WriteLine($"Navigating to ticket: {ticket.TicketId}");
                Frame.Navigate(typeof(TicketConversationPage), ticket.TicketId);
            }
        }

        private async System.Threading.Tasks.Task ShowTicketDetailsAsync(string ticketId)
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                
                var ticket = await _apiService.GetTicketByIdAsync(ticketId);
                var messages = await _apiService.GetTicketMessagesAsync(ticketId);
                
                LoadingOverlay.Visibility = Visibility.Collapsed;
                
                if (ticket == null)
                {
                    await ShowDialogAsync("Error", "Unable to load ticket details.");
                    return;
                }
                
                var isClosed = ticket.Status?.ToLower() == "closed";
                
                // Create main container
                var mainContent = new StackPanel { Spacing = 16, MinWidth = 550 };
                
                // Header Card with ticket info
                var headerCard = new Border
                {
                    Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16)
                };
                
                var headerStack = new StackPanel { Spacing = 8 };
                headerStack.Children.Add(new TextBlock 
                { 
                    Text = ticket.Subject ?? "No Subject", 
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, 
                    FontSize = 18, 
                    TextWrapping = TextWrapping.Wrap 
                });
                
                // Status badges row
                var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                
                // Status badge
                var statusBadge = new Border
                {
                    Background = isClosed 
                        ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(30, 150, 150, 150))
                        : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(30, 16, 185, 129)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 4, 8, 4)
                };
                statusBadge.Child = new TextBlock 
                { 
                    Text = ticket.Status ?? "Open", 
                    FontSize = 12,
                    Foreground = isClosed 
                        ? new SolidColorBrush(Microsoft.UI.Colors.Gray)
                        : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129))
                };
                statusRow.Children.Add(statusBadge);
                
                statusRow.Children.Add(new TextBlock { Text = $"Category: {ticket.Category}", FontSize = 12, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
                statusRow.Children.Add(new TextBlock { Text = $"Priority: {ticket.Priority}", FontSize = 12, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
                headerStack.Children.Add(statusRow);
                
                headerStack.Children.Add(new TextBlock { Text = $"Created: {FormatDate(ticket.CreatedAt)}", FontSize = 12, Opacity = 0.6 });
                
                // Description
                if (!string.IsNullOrEmpty(ticket.Description))
                {
                    headerStack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Microsoft.UI.Colors.Gray), Opacity = 0.2, Margin = new Thickness(0, 8, 0, 8) });
                    headerStack.Children.Add(new TextBlock { Text = ticket.Description, TextWrapping = TextWrapping.Wrap, Opacity = 0.9 });
                }
                
                headerCard.Child = headerStack;
                mainContent.Children.Add(headerCard);
                
                // Conversation section
                if (messages != null && messages.Count > 0)
                {
                    mainContent.Children.Add(new TextBlock 
                    { 
                        Text = $"Conversation ({messages.Count} messages)", 
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Margin = new Thickness(0, 8, 0, 0)
                    });
                    
                    foreach (var msg in messages)
                    {
                        var isAgent = msg.SenderType?.ToLower() == "agent";
                        
                        var msgCard = new Border
                        {
                            Background = isAgent 
                                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(15, 16, 185, 129))
                                : (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
                            BorderBrush = isAgent
                                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(50, 16, 185, 129))
                                : (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                            BorderThickness = new Thickness(0, 0, 0, 3),
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(16),
                            Margin = new Thickness(0, 4, 0, 4)
                        };
                        
                        var msgStack = new StackPanel { Spacing = 8 };
                        
                        // Sender header
                        var senderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                        var senderBadge = new Border
                        {
                            Background = isAgent 
                                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 16, 185, 129))
                                : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 59, 130, 246)),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2)
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
                        msgStack.Children.Add(senderRow);
                        
                        // Message content
                        msgStack.Children.Add(new TextBlock { Text = msg.Content, TextWrapping = TextWrapping.Wrap });
                        
                        msgCard.Child = msgStack;
                        mainContent.Children.Add(msgCard);
                    }
                }
                else
                {
                    mainContent.Children.Add(new TextBlock 
                    { 
                        Text = "No messages yet", 
                        Opacity = 0.6, 
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 16, 0, 16)
                    });
                }
                
                // Reply section (only if not closed)
                TextBox replyBox = null;
                if (!isClosed)
                {
                    var replyCard = new Border
                    {
                        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(16),
                        Margin = new Thickness(0, 8, 0, 0)
                    };
                    
                    var replyStack = new StackPanel { Spacing = 12 };
                    replyStack.Children.Add(new TextBlock { Text = "Reply to Support", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                    
                    replyBox = new TextBox
                    {
                        PlaceholderText = "Type your reply here...",
                        TextWrapping = TextWrapping.Wrap,
                        AcceptsReturn = true,
                        MinHeight = 80
                    };
                    replyStack.Children.Add(replyBox);
                    
                    replyCard.Child = replyStack;
                    mainContent.Children.Add(replyCard);
                }
                else
                {
                    var closedNotice = new Border
                    {
                        Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(20, 150, 150, 150)),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(16),
                        Margin = new Thickness(0, 8, 0, 0)
                    };
                    closedNotice.Child = new TextBlock 
                    { 
                        Text = "This ticket is closed. If you need further assistance, please open a new ticket.",
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.7
                    };
                    mainContent.Children.Add(closedNotice);
                }
                
                var dialog = new ContentDialog
                {
                    Title = $"Ticket #{ticketId}",
                    CloseButtonText = "Close",
                    XamlRoot = this.XamlRoot,
                    Content = new ScrollViewer { Content = mainContent, MaxHeight = 600 }
                };
                
                // Add Send Reply button if not closed
                if (!isClosed && replyBox != null)
                {
                    dialog.PrimaryButtonText = "Send Reply";
                    dialog.PrimaryButtonClick += async (s, args) =>
                    {
                        var deferral = args.GetDeferral();
                        try
                        {
                            var replyText = replyBox.Text?.Trim();
                            if (!string.IsNullOrEmpty(replyText))
                            {
                                var success = await _apiService.SendTicketMessageAsync(ticketId, replyText);
                                if (success)
                                {
                                    await LoadTicketsAsync();
                                }
                            }
                        }
                        finally
                        {
                            deferral.Complete();
                        }
                    };
                }
                
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                await ShowDialogAsync("Error", $"Error loading ticket: {ex.Message}");
            }
        }

        #endregion

        #region New Ticket Tab

        private List<AttachmentFile> _attachments = new();

        private async void AttachmentArea_Click(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            await PickAttachmentFilesAsync();
        }

        private async void AttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            await PickAttachmentFilesAsync();
        }

        private async System.Threading.Tasks.Task PickAttachmentFilesAsync()
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
                AttachmentsListControl.ItemsSource = null;
                AttachmentsListControl.ItemsSource = _attachments.ToList();
            }
        }

        private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AttachmentFile file)
            {
                _attachments.Remove(file);
                AttachmentsListControl.ItemsSource = null;
                AttachmentsListControl.ItemsSource = _attachments.ToList();
            }
        }


        private async void SubmitTicket_Click(object sender, RoutedEventArgs e)
        {
            var subject = SubjectBox.Text?.Trim();
            var description = DescriptionBox.Text?.Trim();
            
            SubjectError.Visibility = Visibility.Collapsed;
            DescriptionError.Visibility = Visibility.Collapsed;
            
            if (string.IsNullOrEmpty(subject))
            {
                SubjectError.Text = "Subject is required";
                SubjectError.Visibility = Visibility.Visible;
                return;
            }
            
            if (string.IsNullOrEmpty(description))
            {
                DescriptionError.Text = "Description is required";
                DescriptionError.Visibility = Visibility.Visible;
                return;
            }
            
            var category = (CategoryCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Technical";
            var priority = (PriorityCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Medium";

            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                
                var success = await _apiService.CreateTicketAsync(subject, category, priority, description);
                
                if (success)
                {
                    SubjectBox.Text = "";
                    DescriptionBox.Text = "";
                    
                    await ShowDialogAsync("Success", "Ticket submitted successfully! Our support team will respond soon.");
                    
                    SupportPivot.SelectedIndex = 0;
                    await LoadTicketsAsync();
                }
                else
                {
                    await ShowDialogAsync("Error", "Failed to submit ticket. Please try again.");
                }
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Error", $"Error: {ex.Message}");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region FAQ Tab

        private void LoadFAQData()
        {
            _faqCategories = new List<FAQCategory>
            {
                new FAQCategory
                {
                    Title = "What is SensePC?",
                    Items = new List<FAQItem>
                    {
                        new FAQItem { Question = "What is a SensePC?", Answer = "SensePC is your personal computer in the cloud—high-performance virtual desktops that live in secure data centers and stream to your devices. Create, start, stop, or resize in minutes and work from anywhere." },
                        new FAQItem { Question = "How is SensePC different from other cloud computing services?", Answer = "SensePC focuses on responsiveness, security, and simplicity. We combine GPU-ready streaming, adaptive bitrate, and enterprise-grade controls so it feels like a local PC—without the hardware hassle." },
                        new FAQItem { Question = "Who is SensePC for?", Answer = "Creators, developers, students, gamers (via native app), small teams, and enterprises who need secure, high-performance desktops accessible from anywhere." }
                    }
                },
                new FAQCategory
                {
                    Title = "Getting Started",
                    Items = new List<FAQItem>
                    {
                        new FAQItem { Question = "How do I sign up?", Answer = "Create an account (no plan required), verify your email, and launch your first SensePC from the dashboard. You can pick or upgrade a plan later." },
                        new FAQItem { Question = "How fast can I be up and running?", Answer = "Typically within minutes. Provisioning, OS setup, and secure access are automated." },
                        new FAQItem { Question = "Do I need special hardware?", Answer = "No. Any modern device with a browser works. For gaming/low-latency use, install our native app." },
                        new FAQItem { Question = "How do I verify my email and secure my account?", Answer = "During sign-up we send a one-time passcode (OTP) to your email. Enter that OTP to verify your account. After your first login, enable MFA in Settings → Security." }
                    }
                },
                new FAQCategory
                {
                    Title = "Performance & Internet",
                    Items = new List<FAQItem>
                    {
                        new FAQItem { Question = "What internet speed do I need?", Answer = "We recommend 15 Mbps down / 5 Mbps up. Adaptive streaming helps on slower links; lower latency improves responsiveness." },
                        new FAQItem { Question = "Can I use SensePC on Wi-Fi?", Answer = "Yes. For the best experience, use 5 GHz Wi-Fi or wired Ethernet." },
                        new FAQItem { Question = "Does SensePC support gaming?", Answer = "Yes—via the native app for the best frame rates and latency. The web client is great for productivity apps." }
                    }
                },
                new FAQCategory
                {
                    Title = "Apps, Licenses & Data",
                    Items = new List<FAQItem>
                    {
                        new FAQItem { Question = "Can I install my own software?", Answer = "Yes. Treat it like a regular PC. Install IDEs, creative tools, enterprise apps—whatever your workflow needs." },
                        new FAQItem { Question = "Can I bring my own OS licenses?", Answer = "Yes. BYOL is supported for Windows and Linux. Ensure your license terms permit cloud use." },
                        new FAQItem { Question = "Where do files live?", Answer = "Your data lives on encrypted SSD volumes attached to your SensePC and in SenseStorage, our cloud drive (similar to iCloud)." }
                    }
                },
                new FAQCategory
                {
                    Title = "Creating & Managing PCs",
                    Items = new List<FAQItem>
                    {
                        new FAQItem { Question = "How do I create my first SensePC?", Answer = "Click Build PC, choose a configuration (OS, CPU/GPU/RAM and storage), pick a region, name it, and launch. It appears in your dashboard with status and actions." },
                        new FAQItem { Question = "What OS options are available?", Answer = "Windows and Linux today, with SenseOS options planned. You can install additional tooling as needed." },
                        new FAQItem { Question = "Can I resize my SensePC later?", Answer = "Only the Hourly plan supports PC resize. Daily/Monthly plans do not support resize; wait until the cycle ends." },
                        new FAQItem { Question = "Can I schedule my PC?", Answer = "Yes. Set auto start/stop schedules and configure idle auto-stop to save cost when inactive." }
                    }
                },
                new FAQCategory
                {
                    Title = "SenseStorage",
                    Items = new List<FAQItem>
                    {
                        new FAQItem { Question = "How is SenseStorage billed?", Answer = "Intelligent tier-based billing: you're charged on the maximum usage observed in the month. No fixed plans to select." },
                        new FAQItem { Question = "Can I use SenseStorage without adding a card?", Answer = "New users can leverage promotional balance (when available) to store files without adding a card. Once promo funds are used, you'll need to recharge." },
                        new FAQItem { Question = "Can I preview media?", Answer = "Yes. SenseStorage supports in-browser previews for images, music, and videos where supported by your browser." }
                    }
                },
                new FAQCategory
                {
                    Title = "Plans & Billing",
                    Items = new List<FAQItem>
                    {
                        new FAQItem { Question = "Do I need to pick a plan to sign up?", Answer = "No. Sign up first. SensePC offers Hourly (default), Daily, and Monthly plans you can choose later." },
                        new FAQItem { Question = "How do the plans differ?", Answer = "Hourly: Billed per hour while running. Daily: Prepaid once per day. Monthly: Prepaid every 30 days." },
                        new FAQItem { Question = "Can I upgrade/downgrade plans?", Answer = "You can upgrade Hourly → Daily/Monthly or Daily → Monthly. Plan changes take effect after the current billing cycle." },
                        new FAQItem { Question = "How do I recharge my Wallet?", Answer = "Add a credit/debit card and top up. Enable Auto-recharge for convenience. Payments are processed securely by Stripe." }
                    }
                },
                new FAQCategory
                {
                    Title = "Security & Compliance",
                    Items = new List<FAQItem>
                    {
                        new FAQItem { Question = "How are sessions secured?", Answer = "Short-lived session tokens, TLS encryption, and per-user auth guard every connection. No static passwords required." },
                        new FAQItem { Question = "What about compliance?", Answer = "We align with industry standards and can support SOC 2/GDPR/HIPAA-oriented controls. Request our security overview for details." },
                        new FAQItem { Question = "Do you provide audit logs?", Answer = "Admin plans include immutable audit logs for key actions (create/stop/resize, billing events, policy changes)." }
                    }
                },
                new FAQCategory
                {
                    Title = "Admin & Team Controls",
                    Items = new List<FAQItem>
                    {
                        new FAQItem { Question = "What roles are available?", Answer = "Owner: Full access. Admin: Full account access except removing Owner. Member: Access only to PCs assigned to them." },
                        new FAQItem { Question = "Who can invite or remove users?", Answer = "Owner and Admins can invite/remove users. Members cannot." },
                        new FAQItem { Question = "Who can assign PCs to Members?", Answer = "Owner/Admin can assign or deassign PCs to Members. PC must be Stopped to change assignment." }
                    }
                },
                new FAQCategory
                {
                    Title = "Troubleshooting",
                    Items = new List<FAQItem>
                    {
                        new FAQItem { Question = "My stream feels laggy—what can I try?", Answer = "Switch to the native app, close bandwidth-heavy tabs, use wired Ethernet or 5 GHz Wi-Fi, pick a nearer region, or lower resolution/bitrate in client settings." },
                        new FAQItem { Question = "I can't connect—what now?", Answer = "Check internet reachability, confirm your instance is Running, allow pop-ups for the SensePC site, verify you're using the latest client." },
                        new FAQItem { Question = "A game/app won't start.", Answer = "Ensure GPU drivers are current, install dependencies, and run via the native app. Some anti-cheat systems may not be supported." },
                        new FAQItem { Question = "I don't see CPU/Memory usage.", Answer = "Open the Computer Metrics panel in the dashboard for live CPU and memory metrics. If empty, refresh or ensure the instance is running." }
                    }
                }
            };
            
            FAQCategoriesControl.ItemsSource = _faqCategories;
        }

        private void FAQSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            var query = sender.Text?.ToLower() ?? "";
            
            if (string.IsNullOrEmpty(query))
            {
                FAQCategoriesControl.ItemsSource = _faqCategories;
                return;
            }
            
            var filtered = _faqCategories
                .Select(c => new FAQCategory
                {
                    Title = c.Title,
                    Items = c.Items.Where(i => 
                        i.Question.ToLower().Contains(query) || 
                        i.Answer.ToLower().Contains(query)).ToList()
                })
                .Where(c => c.Items.Count > 0)
                .ToList();
            
            FAQCategoriesControl.ItemsSource = filtered;
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            SetAllExpandersState(true);
        }

        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            SetAllExpandersState(false);
        }

        private void SetAllExpandersState(bool expanded)
        {
            var expanders = FindVisualChildren<Expander>(FAQCategoriesControl);
            foreach (var expander in expanders)
            {
                expander.IsExpanded = expanded;
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) yield return t;
                
                foreach (var grandChild in FindVisualChildren<T>(child))
                {
                    yield return grandChild;
                }
            }
        }

        #endregion

        #region Helpers

        private string FormatDate(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) return "";
            if (DateTime.TryParse(dateStr, out var date))
            {
                return date.ToString("MMM dd, yyyy");
            }
            return dateStr;
        }

        private async System.Threading.Tasks.Task ShowDialogAsync(string title, string message)
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

        #endregion
    }

    #region View Models

    public class TicketViewModel
    {
        public string TicketId { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Status { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public string CreatedAtDisplay { get; set; } = "";
    }

    public class FAQCategory
    {
        public string Title { get; set; } = "";
        public List<FAQItem> Items { get; set; } = new();
    }

    public class FAQItem
    {
        public string Question { get; set; } = "";
        public string Answer { get; set; } = "";
    }

    public class AttachmentFile
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
    }

    #endregion
}
