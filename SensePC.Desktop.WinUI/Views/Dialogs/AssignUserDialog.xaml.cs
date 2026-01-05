using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using SensePC.Desktop.WinUI.Models;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace SensePC.Desktop.WinUI.Views.Dialogs
{
    /// <summary>
    /// Dialog for assigning/unassigning users to a PC - built programmatically
    /// </summary>
    public sealed class AssignUserDialog : ContentDialog
    {
        private readonly PCInstance _pc;
        private readonly SensePCApiService _apiService;
        
        private ListView _teamMembersListView;
        private StackPanel _loadingUsersPanel;
        private StackPanel _savingPanel;
        private TextBlock _emptyStateText;
        private TextBlock _errorText;
        
        private List<TeamMember> _teamMembers = new();
        private HashSet<string> _initiallyAssignedIds = new();

        public bool AssignmentChanged { get; private set; }

        public AssignUserDialog(PCInstance pc, XamlRoot xamlRoot)
        {
            this.XamlRoot = xamlRoot;
            _pc = pc;
            _apiService = new SensePCApiService(new SecureStorage());

            Title = "Assign Users";
            PrimaryButtonText = "Save";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
            Opened += AssignUserDialog_Opened;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 400 };

            // PC name header
            var pcInfoBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 95, 111, 255)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };
            var pcInfoStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            pcInfoStack.Children.Add(new FontIcon { Glyph = "\uE7F4", FontSize = 20 });
            var pcTextStack = new StackPanel();
            pcTextStack.Children.Add(new TextBlock { Text = "Assigning users to:", Opacity = 0.7, FontSize = 12 });
            pcTextStack.Children.Add(new TextBlock { Text = _pc.SystemName, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            pcInfoStack.Children.Add(pcTextStack);
            pcInfoBox.Child = pcInfoStack;
            mainStack.Children.Add(pcInfoBox);

            // Instructions
            mainStack.Children.Add(new TextBlock
            {
                Text = "Select team members who should have access to this PC:",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8
            });

            // Loading users panel
            _loadingUsersPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20)
            };
            _loadingUsersPanel.Children.Add(new ProgressRing { IsActive = true, Width = 24, Height = 24 });
            _loadingUsersPanel.Children.Add(new TextBlock { Text = "Loading team members...", VerticalAlignment = VerticalAlignment.Center });
            mainStack.Children.Add(_loadingUsersPanel);

            // Empty state text
            _emptyStateText = new TextBlock
            {
                Text = "No team members found. Add team members from the web dashboard first.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.6,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            mainStack.Children.Add(_emptyStateText);

            // Team members list
            _teamMembersListView = new ListView
            {
                SelectionMode = ListViewSelectionMode.Multiple,
                Height = 250,
                Visibility = Visibility.Collapsed
            };
            mainStack.Children.Add(_teamMembersListView);

            // Saving panel
            _savingPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _savingPanel.Children.Add(new ProgressRing { IsActive = true, Width = 16, Height = 16 });
            _savingPanel.Children.Add(new TextBlock { Text = "Saving assignments...", VerticalAlignment = VerticalAlignment.Center });
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

        private async void AssignUserDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
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

                _teamMembersListView.Visibility = Visibility.Visible;

                foreach (var member in _teamMembers)
                {
                    var item = new ListViewItem
                    {
                        Tag = member.Id,
                        Content = CreateMemberItem(member)
                    };

                    _teamMembersListView.Items.Add(item);

                    // Pre-select if already assigned
                    if (member.AssignedPCs != null && member.AssignedPCs.Contains(_pc.InstanceId))
                    {
                        _initiallyAssignedIds.Add(member.Id);
                        _teamMembersListView.SelectedItems.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                _loadingUsersPanel.Visibility = Visibility.Collapsed;
                _errorText.Text = $"Failed to load team members: {ex.Message}";
                _errorText.Visibility = Visibility.Visible;
            }
        }

        private Grid CreateMemberItem(TeamMember member)
        {
            var grid = new Grid { Padding = new Thickness(4) };

            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };

            // Avatar circle
            var avatarBorder = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Color.FromArgb(255, 95, 111, 255))
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
            stack.Children.Add(avatarBorder);

            // Name and email
            var infoStack = new StackPanel();
            infoStack.Children.Add(new TextBlock
            {
                Text = member.Name ?? member.Email,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
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
            stack.Children.Add(infoStack);

            grid.Children.Add(stack);
            return grid;
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return name.Substring(0, Math.Min(2, name.Length)).ToUpper();
        }

        private async void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var deferral = args.GetDeferral();

            try
            {
                _savingPanel.Visibility = Visibility.Visible;
                _errorText.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = false;
                _teamMembersListView.IsEnabled = false;

                // Get currently selected member IDs
                var selectedIds = new HashSet<string>();
                foreach (var item in _teamMembersListView.SelectedItems)
                {
                    if (item is ListViewItem lvItem && lvItem.Tag is string memberId)
                    {
                        selectedIds.Add(memberId);
                    }
                }

                // Determine who to assign (newly selected)
                var toAssign = selectedIds.Except(_initiallyAssignedIds);
                // Determine who to unassign (previously selected but not now)
                var toUnassign = _initiallyAssignedIds.Except(selectedIds);

                bool hasError = false;

                // Assign new users
                foreach (var memberId in toAssign)
                {
                    var member = _teamMembers.FirstOrDefault(m => m.Id == memberId);
                    var success = await _apiService.AssignPCAsync(_pc.InstanceId, memberId, _pc.SystemName);
                    if (!success) hasError = true;
                }

                // Unassign removed users
                foreach (var memberId in toUnassign)
                {
                    var success = await _apiService.UnassignPCAsync(_pc.InstanceId, memberId);
                    if (!success) hasError = true;
                }

                if (hasError)
                {
                    args.Cancel = true;
                    _errorText.Text = "Some assignments failed. Please try again.";
                    _errorText.Visibility = Visibility.Visible;
                }
                else
                {
                    AssignmentChanged = toAssign.Any() || toUnassign.Any();
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
                _teamMembersListView.IsEnabled = true;
                deferral.Complete();
            }
        }
    }
}
