using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SensePC.Desktop.WinUI.Models;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace SensePC.Desktop.WinUI.Views.Dialogs
{
    /// <summary>
    /// Dialog for sharing files with others via link or email
    /// </summary>
    public sealed class ShareDialog : ContentDialog
    {
        private readonly SensePCApiService _apiService;
        private readonly StorageItem _item;
        private readonly string? _currentFolder;
        
        // UI elements
        private TextBox _linkTextBox = null!;
        private TextBox _emailTextBox = null!;
        private ComboBox _expirationCombo = null!;
        private ToggleSwitch _passwordToggle = null!;
        private TextBox _passwordBox = null!;
        private StackPanel _loadingPanel = null!;
        private StackPanel _contentPanel = null!;
        private TextBlock _errorText = null!;
        private Button _copyLinkButton = null!;
        
        private string? _shareLink;

        public bool FileShared { get; private set; }

        public ShareDialog(StorageItem item, XamlRoot xamlRoot, string? currentFolder = null)
        {
            _item = item;
            _currentFolder = currentFolder;
            this.XamlRoot = xamlRoot;
            _apiService = new SensePCApiService(new SecureStorage());

            Title = $"Share \"{item.FileName}\"";
            PrimaryButtonText = "Share via Email";
            SecondaryButtonText = "Copy Link";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Secondary;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
            SecondaryButtonClick += SecondaryButton_Click;
            Loaded += OnDialogLoaded;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 420 };

            // Loading panel
            _loadingPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20)
            };
            _loadingPanel.Children.Add(new ProgressRing { IsActive = true, Width = 24, Height = 24 });
            _loadingPanel.Children.Add(new TextBlock 
            { 
                Text = "Generating share link...", 
                VerticalAlignment = VerticalAlignment.Center 
            });
            mainStack.Children.Add(_loadingPanel);

            // Content panel
            _contentPanel = new StackPanel { Spacing = 16, Visibility = Visibility.Collapsed };

            // Share link section
            var linkSection = new StackPanel { Spacing = 8 };
            linkSection.Children.Add(new TextBlock 
            { 
                Text = "Share Link",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
            });

            var linkGrid = new Grid();
            linkGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            linkGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _linkTextBox = new TextBox
            {
                IsReadOnly = true,
                PlaceholderText = "Share link will appear here"
            };
            Grid.SetColumn(_linkTextBox, 0);
            linkGrid.Children.Add(_linkTextBox);

            _copyLinkButton = new Button
            {
                Content = new FontIcon { Glyph = "\uE8C8", FontSize = 14 },
                Padding = new Thickness(12),
                Margin = new Thickness(8, 0, 0, 0)
            };
            ToolTipService.SetToolTip(_copyLinkButton, "Copy to clipboard");
            _copyLinkButton.Click += CopyLink_Click;
            Grid.SetColumn(_copyLinkButton, 1);
            linkGrid.Children.Add(_copyLinkButton);

            linkSection.Children.Add(linkGrid);
            _contentPanel.Children.Add(linkSection);

            // Expiration section
            var expirationSection = new StackPanel { Spacing = 8 };
            expirationSection.Children.Add(new TextBlock 
            { 
                Text = "Link Expiration",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
            });

            _expirationCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            _expirationCombo.Items.Add(new ComboBoxItem { Content = "Never", Tag = 0 });
            _expirationCombo.Items.Add(new ComboBoxItem { Content = "1 Hour", Tag = 1, IsSelected = true });
            _expirationCombo.Items.Add(new ComboBoxItem { Content = "24 Hours", Tag = 24 });
            _expirationCombo.Items.Add(new ComboBoxItem { Content = "7 Days", Tag = 168 });
            _expirationCombo.Items.Add(new ComboBoxItem { Content = "30 Days", Tag = 720 });
            expirationSection.Children.Add(_expirationCombo);
            _contentPanel.Children.Add(expirationSection);

            // Password protection section
            var passwordSection = new StackPanel { Spacing = 8 };
            _passwordToggle = new ToggleSwitch 
            { 
                Header = "Password Protection",
                OffContent = "Disabled",
                OnContent = "Enabled"
            };
            _passwordToggle.Toggled += (s, e) => 
            {
                _passwordBox.Visibility = _passwordToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            };
            passwordSection.Children.Add(_passwordToggle);

            _passwordBox = new TextBox
            {
                PlaceholderText = "Enter password for the share link",
                Visibility = Visibility.Collapsed
            };
            passwordSection.Children.Add(_passwordBox);
            _contentPanel.Children.Add(passwordSection);

            // Email section
            var emailSection = new StackPanel { Spacing = 8 };
            emailSection.Children.Add(new TextBlock 
            { 
                Text = "Share via Email",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
            });
            _emailTextBox = new TextBox
            {
                PlaceholderText = "Enter email addresses (comma-separated)"
            };
            emailSection.Children.Add(_emailTextBox);
            _contentPanel.Children.Add(emailSection);

            mainStack.Children.Add(_contentPanel);

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
            await GenerateShareLinkAsync();
        }

        private async Task GenerateShareLinkAsync()
        {
            try
            {
                _loadingPanel.Visibility = Visibility.Visible;
                _contentPanel.Visibility = Visibility.Collapsed;

                _shareLink = await _apiService.GenerateShareLinkAsync(_item.FileName, _currentFolder, _item.Id);
                
                if (!string.IsNullOrEmpty(_shareLink))
                {
                    _linkTextBox.Text = _shareLink;
                    _contentPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    _errorText.Text = "Could not generate share link. Please try again.";
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
                _loadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_shareLink))
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(_shareLink);
                Clipboard.SetContent(dataPackage);
                
                // Visual feedback
                if (_copyLinkButton.Content is FontIcon icon)
                {
                    icon.Glyph = "\uE73E"; // Checkmark
                }
            }
        }

        private async void SecondaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Copy Link button - just copy and close
            if (!string.IsNullOrEmpty(_shareLink))
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(_shareLink);
                Clipboard.SetContent(dataPackage);
                FileShared = true;
            }
        }

        private async void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Share via Email
            var emails = _emailTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(emails))
            {
                args.Cancel = true;
                _errorText.Text = "Please enter at least one email address.";
                _errorText.Visibility = Visibility.Visible;
                return;
            }

            var deferral = args.GetDeferral();

            try
            {
                var expirationHours = 24;
                if (_expirationCombo.SelectedItem is ComboBoxItem item && item.Tag is int hours)
                {
                    expirationHours = hours;
                }

                var password = _passwordToggle.IsOn ? _passwordBox.Text : null;
                
                var success = await _apiService.ShareFileViaEmailAsync(
                    _item.FileName,
                    emails.Split(',', StringSplitOptions.RemoveEmptyEntries),
                    _currentFolder,
                    _item.Id,
                    expirationHours,
                    password
                );

                if (success)
                {
                    FileShared = true;
                }
                else
                {
                    args.Cancel = true;
                    _errorText.Text = "Failed to send share invitations. Please try again.";
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
                deferral.Complete();
            }
        }
    }
}
