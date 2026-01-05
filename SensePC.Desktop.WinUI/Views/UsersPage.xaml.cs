using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using SensePC.Desktop.WinUI.Services;
using SensePC.Desktop.WinUI.Views.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SensePC.Desktop.WinUI.Views
{
    public sealed partial class UsersPage : Page
    {
        private readonly SensePCApiService _apiService;
        private List<ApiUser> _allUsers = new();
        private Dictionary<string, List<PCAssignment>> _assignments = new();
        private string _searchQuery = "";

        public UsersPage()
        {
            this.InitializeComponent();
            _apiService = new SensePCApiService(new SecureStorage());
            Loaded += UsersPage_Loaded;
        }

        private async void UsersPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            LoadingRing.IsActive = true;
            UsersListView.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Collapsed;

            try
            {
                var usersTask = _apiService.GetUsersAsync();
                var assignmentsTask = _apiService.GetAssignmentsAsync();
                await Task.WhenAll(usersTask, assignmentsTask);

                _allUsers = usersTask.Result;
                _assignments = assignmentsTask.Result;
                ApplyFilter();
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }

        private void ApplyFilter()
        {
            var filtered = string.IsNullOrEmpty(_searchQuery)
                ? _allUsers
                : _allUsers.Where(u =>
                    u.DisplayName.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)
                ).ToList();

            UsersListView.Items.Clear();

            if (filtered.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                UsersListView.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyState.Visibility = Visibility.Collapsed;
                UsersListView.Visibility = Visibility.Visible;

                foreach (var user in filtered)
                {
                    UsersListView.Items.Add(CreateUserRow(user));
                }
            }

            TotalUsersText.Text = $"Total users: {_allUsers.Count}";
        }

        private Grid CreateUserRow(ApiUser user)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            // Name
            var nameText = new TextBlock
            {
                Text = user.DisplayName,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"]
            };
            Grid.SetColumn(nameText, 0);
            grid.Children.Add(nameText);

            // Email
            var emailText = new TextBlock
            {
                Text = user.Email,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            Grid.SetColumn(emailText, 1);
            grid.Children.Add(emailText);

            // Role Badge
            var roleBadge = CreateInfoBadge(user.Role, 
                user.Role?.ToLower() == "admin" ? InfoBarSeverity.Error : InfoBarSeverity.Success);
            Grid.SetColumn(roleBadge, 2);
            grid.Children.Add(roleBadge);

            // Status Badge
            var statusBadge = CreateInfoBadge(user.Status ?? "—",
                user.Status?.ToLower() == "active" ? InfoBarSeverity.Success :
                user.Status?.ToLower() == "pending" ? InfoBarSeverity.Warning : InfoBarSeverity.Informational);
            Grid.SetColumn(statusBadge, 3);
            grid.Children.Add(statusBadge);

            // Assigned PCs
            var pcsPanel = CreateAssignedPCsPanel(user.Id);
            Grid.SetColumn(pcsPanel, 4);
            grid.Children.Add(pcsPanel);

            // Actions
            var actionsButton = CreateActionsButton(user);
            Grid.SetColumn(actionsButton, 5);
            grid.Children.Add(actionsButton);

            return grid;
        }

        private Border CreateInfoBadge(string text, InfoBarSeverity severity)
        {
            var color = severity switch
            {
                InfoBarSeverity.Success => Windows.UI.Color.FromArgb(255, 16, 185, 129),
                InfoBarSeverity.Warning => Windows.UI.Color.FromArgb(255, 245, 158, 11),
                InfoBarSeverity.Error => Windows.UI.Color.FromArgb(255, 239, 68, 68),
                _ => Windows.UI.Color.FromArgb(255, 107, 114, 128)
            };

            return new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, color.R, color.G, color.B)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(color)
                }
            };
        }

        private StackPanel CreateAssignedPCsPanel(string userId)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (_assignments.TryGetValue(userId, out var pcs) && pcs.Count > 0)
            {
                var countText = pcs.Count == 1 ? "1 PC" : $"{pcs.Count} PCs";
                panel.Children.Add(new TextBlock
                {
                    Text = countText,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
                });
            }
            else
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "None",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
                });
            }

            return panel;
        }

        private Button CreateActionsButton(ApiUser user)
        {
            var button = new Button
            {
                Content = new FontIcon { Glyph = "\uE712", FontSize = 14 },
                Style = (Style)Application.Current.Resources["SubtleButtonStyle"],
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var flyout = new MenuFlyout();

            if (user.Role?.ToLower() == "member")
            {
                flyout.Items.Add(new MenuFlyoutItem
                {
                    Text = "Manage PCs",
                    Icon = new FontIcon { Glyph = "\uE7F4" }
                });
                ((MenuFlyoutItem)flyout.Items.Last()).Click += async (s, e) => await ShowManagePCDialogAsync(user);
            }

            if (user.Status?.ToLower() == "pending")
            {
                flyout.Items.Add(new MenuFlyoutItem
                {
                    Text = "Resend Invite",
                    Icon = new FontIcon { Glyph = "\uE715" }
                });
                ((MenuFlyoutItem)flyout.Items.Last()).Click += async (s, e) => await ResendInviteAsync(user);
            }

            if (flyout.Items.Count > 0)
                flyout.Items.Add(new MenuFlyoutSeparator());

            var deleteItem = new MenuFlyoutItem
            {
                Text = "Delete",
                Icon = new FontIcon { Glyph = "\uE74D" }
            };
            deleteItem.Click += async (s, e) => await ShowDeleteDialogAsync(user);
            flyout.Items.Add(deleteItem);

            button.Flyout = flyout;
            return button;
        }

        private async Task ShowManagePCDialogAsync(ApiUser user)
        {
            var allPCs = await _apiService.FetchPCsAsync();
            var userPCs = _assignments.TryGetValue(user.Id, out var pcs) ? pcs : new List<PCAssignment>();

            var listView = new ListView { SelectionMode = ListViewSelectionMode.None, MaxHeight = 300 };
            foreach (var pc in allPCs)
            {
                var isAssigned = userPCs.Any(a => a.InstanceId == pc.InstanceId);
                var panel = new Grid();
                panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                panel.Children.Add(new TextBlock { Text = pc.SystemName ?? pc.InstanceId, VerticalAlignment = VerticalAlignment.Center });

                var btn = new Button
                {
                    Content = isAssigned ? "Remove" : "Assign",
                    Style = (Style)Application.Current.Resources[isAssigned ? "DefaultButtonStyle" : "AccentButtonStyle"]
                };
                btn.Click += async (s, e) =>
                {
                    if (isAssigned)
                        await _apiService.UnassignPCAsync(pc.InstanceId, user.Id);
                    else
                        await _apiService.AssignPCAsync(pc.InstanceId, user.Id, pc.SystemName ?? "");
                    await LoadDataAsync();
                };
                Grid.SetColumn(btn, 1);
                panel.Children.Add(btn);
                listView.Items.Add(panel);
            }

            var dialog = new ContentDialog
            {
                Title = $"Manage PCs - {user.DisplayName}",
                Content = allPCs.Count > 0 ? listView : new TextBlock { Text = "No PCs available" },
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async Task ResendInviteAsync(ApiUser user)
        {
            await _apiService.InviteUserAsync(user.DisplayName, user.Email, user.Role);
            var dialog = new ContentDialog
            {
                Title = "Invite Sent",
                Content = $"Invitation resent to {user.Email}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async Task ShowDeleteDialogAsync(ApiUser user)
        {
            var confirmBox = new TextBox { PlaceholderText = "Type 'confirm' to delete" };
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(new TextBlock { Text = $"Are you sure you want to delete {user.Email}?", TextWrapping = TextWrapping.Wrap });
            content.Children.Add(confirmBox);

            var dialog = new ContentDialog
            {
                Title = "Delete User",
                Content = content,
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                IsPrimaryButtonEnabled = false,
                XamlRoot = this.XamlRoot
            };

            confirmBox.TextChanged += (s, e) => dialog.IsPrimaryButtonEnabled = confirmBox.Text.ToLower() == "confirm";

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await _apiService.DeleteUserAsync(user.Id, user.Email);
                await LoadDataAsync();
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            _searchQuery = sender.Text;
            ApplyFilter();
        }

        private async void InviteUserButton_Click(object sender, RoutedEventArgs e)
        {
            var nameBox = new TextBox { PlaceholderText = "Full name" };
            var emailBox = new TextBox { PlaceholderText = "Email address" };
            var roleBox = new ComboBox { ItemsSource = new[] { "Admin", "Member" }, SelectedIndex = 1, HorizontalAlignment = HorizontalAlignment.Stretch };

            var content = new StackPanel { Spacing = 12, MinWidth = 300 };
            content.Children.Add(new TextBlock { Text = "Name" });
            content.Children.Add(nameBox);
            content.Children.Add(new TextBlock { Text = "Email" });
            content.Children.Add(emailBox);
            content.Children.Add(new TextBlock { Text = "Role" });
            content.Children.Add(roleBox);

            var dialog = new ContentDialog
            {
                Title = "Invite User",
                Content = content,
                PrimaryButtonText = "Send Invite",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var role = roleBox.SelectedItem?.ToString()?.ToLower() ?? "member";
                await _apiService.InviteUserAsync(nameBox.Text, emailBox.Text, role);
                await LoadDataAsync();
            }
        }
    }
}
