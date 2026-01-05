using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
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
    /// Dialog for assigning/unassigning users to a PC - matches website functionality
    /// Shows users with individual Assign/Unassign buttons
    /// </summary>
    public sealed class AssignUserDialog : ContentDialog
    {
        private readonly PCInstance _pc;
        private readonly SensePCApiService _apiService;
        
        private StackPanel _loadingUsersPanel;
        private StackPanel _usersListPanel;
        private TextBlock _emptyStateText;
        private TextBlock _errorText;
        
        private List<TeamMember> _teamMembers = new();
        private HashSet<string> _assignedMemberIds = new();

        public bool AssignmentChanged { get; private set; }

        public AssignUserDialog(PCInstance pc, XamlRoot xamlRoot)
        {
            this.XamlRoot = xamlRoot;
            _pc = pc;
            _apiService = new SensePCApiService(new SecureStorage());

            Title = $"Assign Users to {_pc.SystemName}";
            CloseButtonText = "Close";
            DefaultButton = ContentDialogButton.Close;

            // No primary button - users assign/unassign individually like website

            BuildUI();

            Loaded += OnDialogLoaded;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 420 };

            // Description
            mainStack.Children.Add(new TextBlock
            {
                Text = "Select users to assign to this PC.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8,
                FontSize = 14
            });

            // Loading users panel
            _loadingUsersPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 30, 0, 30)
            };
            _loadingUsersPanel.Children.Add(new ProgressRing { IsActive = true, Width = 24, Height = 24 });
            _loadingUsersPanel.Children.Add(new TextBlock 
            { 
                Text = "Loading team members...", 
                VerticalAlignment = VerticalAlignment.Center 
            });
            mainStack.Children.Add(_loadingUsersPanel);

            // Empty state text
            _emptyStateText = new TextBlock
            {
                Text = "No team members found. Add team members from the web dashboard first.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.6,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20),
                Visibility = Visibility.Collapsed
            };
            mainStack.Children.Add(_emptyStateText);

            // Users list panel (will be populated dynamically)
            _usersListPanel = new StackPanel
            {
                Spacing = 8,
                Visibility = Visibility.Collapsed
            };
            mainStack.Children.Add(_usersListPanel);

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
            await LoadTeamMembersAsync();
        }

        private async Task LoadTeamMembersAsync()
        {
            try
            {
                _loadingUsersPanel.Visibility = Visibility.Visible;
                _errorText.Visibility = Visibility.Collapsed;

                _teamMembers = await _apiService.GetTeamMembersAsync();

                _loadingUsersPanel.Visibility = Visibility.Collapsed;

                if (_teamMembers == null || _teamMembers.Count == 0)
                {
                    _emptyStateText.Visibility = Visibility.Visible;
                    return;
                }

                // Determine which members are already assigned
                foreach (var member in _teamMembers)
                {
                    if (member.AssignedPCs != null && member.AssignedPCs.Contains(_pc.InstanceId))
                    {
                        _assignedMemberIds.Add(member.Id);
                    }
                }

                // Build user list with Assign/Unassign buttons
                BuildUsersList();

                _usersListPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                _loadingUsersPanel.Visibility = Visibility.Collapsed;
                _errorText.Text = $"Failed to load team members: {ex.Message}";
                _errorText.Visibility = Visibility.Visible;
            }
        }

        private void BuildUsersList()
        {
            _usersListPanel.Children.Clear();

            foreach (var member in _teamMembers)
            {
                var userRow = CreateUserRow(member);
                _usersListPanel.Children.Add(userRow);
            }
        }

        private Border CreateUserRow(TeamMember member)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Tag = member.Id
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Avatar
            var avatarBorder = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush(Color.FromArgb(255, 95, 111, 255)),
                Margin = new Thickness(0, 0, 12, 0)
            };
            var initials = GetInitials(member.Name ?? member.Email);
            avatarBorder.Child = new TextBlock
            {
                Text = initials,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White)
            };
            Grid.SetColumn(avatarBorder, 0);
            grid.Children.Add(avatarBorder);

            // Name and email
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock
            {
                Text = member.Name ?? member.Email,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 14
            });
            if (!string.IsNullOrEmpty(member.Name) && !string.IsNullOrEmpty(member.Email))
            {
                infoStack.Children.Add(new TextBlock
                {
                    Text = member.Email,
                    Opacity = 0.6,
                    FontSize = 12
                });
            }
            Grid.SetColumn(infoStack, 1);
            grid.Children.Add(infoStack);

            // Assign/Unassign button
            bool isAssigned = _assignedMemberIds.Contains(member.Id);
            var actionButton = new Button
            {
                Content = isAssigned ? "Unassign" : "Assign",
                Tag = member.Id,
                MinWidth = 80
            };
            
            if (isAssigned)
            {
                // Red-ish style for unassign
                actionButton.Background = new SolidColorBrush(Color.FromArgb(40, 255, 80, 80));
            }
            
            actionButton.Click += async (s, e) => await OnAssignButtonClick(member, actionButton);
            Grid.SetColumn(actionButton, 2);
            grid.Children.Add(actionButton);

            border.Child = grid;
            return border;
        }

        private async Task OnAssignButtonClick(TeamMember member, Button button)
        {
            button.IsEnabled = false;
            var originalContent = button.Content;
            button.Content = "...";

            try
            {
                bool isCurrentlyAssigned = _assignedMemberIds.Contains(member.Id);
                bool success;

                if (isCurrentlyAssigned)
                {
                    // Unassign
                    success = await _apiService.UnassignPCAsync(_pc.InstanceId, member.Id);
                    if (success)
                    {
                        _assignedMemberIds.Remove(member.Id);
                        button.Content = "Assign";
                        button.Background = null;
                        AssignmentChanged = true;
                    }
                }
                else
                {
                    // Assign
                    success = await _apiService.AssignPCAsync(_pc.InstanceId, member.Id, _pc.SystemName);
                    if (success)
                    {
                        _assignedMemberIds.Add(member.Id);
                        button.Content = "Unassign";
                        button.Background = new SolidColorBrush(Color.FromArgb(40, 255, 80, 80));
                        AssignmentChanged = true;
                    }
                }

                if (!success)
                {
                    button.Content = originalContent;
                    _errorText.Text = $"Failed to {(isCurrentlyAssigned ? "unassign" : "assign")} user.";
                    _errorText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                button.Content = originalContent;
                _errorText.Text = $"Error: {ex.Message}";
                _errorText.Visibility = Visibility.Visible;
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return name.Substring(0, Math.Min(2, name.Length)).ToUpper();
        }
    }
}
