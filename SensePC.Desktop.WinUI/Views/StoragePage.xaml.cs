using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SensePC.Desktop.WinUI.Models;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace SensePC.Desktop.WinUI.Views
{
    public sealed partial class StoragePage : Page
    {
        private readonly SensePCApiService _apiService;
        private readonly List<StorageCategory> _categories;
        
        private StorageCategory _selectedCategory = null!;
        private string? _currentFolder;
        private readonly Stack<string> _folderPath = new();
        private int _currentPage = 1;
        private int _totalPages = 1;
        private string _sortBy = "date";
        private string _searchQuery = "";
        private bool _isLoading;
        private bool _isGridView = false;
        
        // Activity tracking
        private static readonly List<ActivityItem> _recentActivity = new();
        private const int MaxActivityItems = 10;

        public StoragePage()
        {
            this.InitializeComponent();
            _apiService = new SensePCApiService(new SecureStorage());
            _categories = StorageCategory.GetCategories();
            _selectedCategory = _categories.First();

            InitializeCategories();
            Loaded += StoragePage_Loaded;
        }

        private void StoragePage_Loaded(object sender, RoutedEventArgs e)
        {
            _ = LoadStorageDataAsync();
        }

        private void InitializeCategories()
        {
            CategoriesPanel.Children.Clear();
            
            foreach (var category in _categories)
            {
                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(12, 10, 12, 10),
                    CornerRadius = new CornerRadius(6),
                    Tag = category
                };

                var stack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10
                };

                stack.Children.Add(new FontIcon
                {
                    Glyph = category.Icon,
                    FontSize = 16
                });

                stack.Children.Add(new TextBlock
                {
                    Text = category.Name,
                    VerticalAlignment = VerticalAlignment.Center
                });

                button.Content = stack;
                button.Click += Category_Click;

                // Highlight selected category
                if (category.Name == _selectedCategory.Name)
                {
                    button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                }

                CategoriesPanel.Children.Add(button);
            }
        }

        private void UpdateCategorySelection()
        {
            foreach (var child in CategoriesPanel.Children)
            {
                if (child is Button button && button.Tag is StorageCategory cat)
                {
                    if (cat.Name == _selectedCategory.Name)
                    {
                        button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                    }
                    else
                    {
                        button.Style = null;
                    }
                }
            }
        }

        private async Task LoadStorageDataAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                ShowLoadingState();

                // Load usage stats
                var usage = await _apiService.GetStorageUsageAsync();
                if (usage != null)
                {
                    StorageUsageText.Text = $"{usage.UsedFormatted} / {usage.MaxFormatted}";
                    StorageProgressBar.Value = usage.UsagePercentage;
                }
                else
                {
                    StorageUsageText.Text = "—";
                }

                // Load files
                await LoadFilesAsync();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task LoadFilesAsync()
        {
            try
            {
                var request = new StorageListRequest
                {
                    Page = _currentPage,
                    Limit = 20,
                    SortBy = _sortBy,
                    SortOrder = _sortBy == "name" ? "asc" : "desc",
                    Folder = _currentFolder,
                    Search = string.IsNullOrWhiteSpace(_searchQuery) ? null : _searchQuery
                };

                // Apply category filters
                if (_selectedCategory?.Name == "Starred")
                {
                    request.Starred = true;
                }
                else if (_selectedCategory?.Name == "Shared")
                {
                    request.Shared = true;
                }
                else if (_selectedCategory != null && !string.IsNullOrEmpty(_selectedCategory.FilterType))
                {
                    request.Type = _selectedCategory.FilterType;
                }

                if (_apiService == null)
                {
                    return;
                }

                var result = await _apiService.ListFilesAsync(request);

                if (result?.Files?.Any() == true)
                {
                    FilesListView.ItemsSource = result.Files;
                    FilesGridView.ItemsSource = result.Files;
                    ShowFilesState();

                    // Update pagination
                    if (result.Pagination != null)
                    {
                        _totalPages = result.Pagination.TotalPages;
                        UpdatePagination();
                    }
                }
                else
                {
                    ShowEmptyState();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadFiles error: {ex.Message}");
                ShowEmptyState();
            }
        }

        private void ShowLoadingState()
        {
            LoadingState.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
            FilesListView.Visibility = Visibility.Collapsed;
            FilesGridView.Visibility = Visibility.Collapsed;
            PaginationPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowEmptyState()
        {
            LoadingState.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
            FilesListView.Visibility = Visibility.Collapsed;
            FilesGridView.Visibility = Visibility.Collapsed;
            PaginationPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowFilesState()
        {
            LoadingState.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Collapsed;
            
            // Show the appropriate view based on mode
            FilesListView.Visibility = _isGridView ? Visibility.Collapsed : Visibility.Visible;
            FilesGridView.Visibility = _isGridView ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdatePagination()
        {
            if (_totalPages > 1)
            {
                PaginationPanel.Visibility = Visibility.Visible;
                PageInfoText.Text = $"Page {_currentPage} of {_totalPages}";
                PrevPageButton.IsEnabled = _currentPage > 1;
                NextPageButton.IsEnabled = _currentPage < _totalPages;
            }
            else
            {
                PaginationPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void Category_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is StorageCategory category)
            {
                _selectedCategory = category;
                _currentFolder = null;
                _folderPath.Clear();
                _currentPage = 1;
                
                CurrentFolderText.Text = category.Name;
                BackButton.Visibility = Visibility.Collapsed;
                
                UpdateCategorySelection();
                _ = LoadFilesAsync();
            }
        }

        private void ViewToggle_Click(object sender, RoutedEventArgs e)
        {
            _isGridView = ViewToggleButton.IsChecked == true;
            
            // Update icon: Grid icon when in list mode (showing what it will switch to), List icon when in grid mode
            ViewToggleIcon.Glyph = _isGridView ? "\uE8FD" : "\uF0E2"; // List icon vs Grid icon
            
            // Toggle visibility
            ShowFilesState();
            
            // Sync the data source between views
            if (_isGridView)
            {
                FilesGridView.ItemsSource = FilesListView.ItemsSource;
            }
        }

        private void FilesGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedCount = FilesGridView.SelectedItems.Count;
            if (selectedCount > 0)
            {
                BulkDownloadButton.Visibility = Visibility.Visible;
                BulkDownloadText.Text = $"Download ({selectedCount})";
                BulkShareButton.Visibility = Visibility.Visible;
                BulkShareText.Text = $"Share ({selectedCount})";
                BulkDeleteButton.Visibility = Visibility.Visible;
                BulkDeleteText.Text = $"Delete ({selectedCount})";
            }
            else
            {
                BulkDownloadButton.Visibility = Visibility.Collapsed;
                BulkShareButton.Visibility = Visibility.Collapsed;
                BulkDeleteButton.Visibility = Visibility.Collapsed;
            }
        }

        private void FileItem_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StorageItem item && item.IsFolder)
            {
                NavigateToFolder(item);
            }
        }

        private void NavigateToFolder(StorageItem folder)
        {
            if (!string.IsNullOrEmpty(_currentFolder))
            {
                _folderPath.Push(_currentFolder);
            }
            
            _currentFolder = folder.Id;
            _currentPage = 1;
            
            CurrentFolderText.Text = folder.FileName;
            BackButton.Visibility = Visibility.Visible;
            
            _ = LoadFilesAsync();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_folderPath.Count > 0)
            {
                _currentFolder = _folderPath.Pop();
                CurrentFolderText.Text = _folderPath.Count > 0 ? "..." : _selectedCategory.Name;
            }
            else
            {
                _currentFolder = null;
                CurrentFolderText.Text = _selectedCategory.Name;
            }

            BackButton.Visibility = _currentFolder != null ? Visibility.Visible : Visibility.Collapsed;
            _currentPage = 1;
            _ = LoadFilesAsync();
        }

        private async void Upload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Dialogs.UploadDialog(this.XamlRoot, _currentFolder);
                var result = await dialog.ShowAsync();
                
                if (dialog.FilesUploaded)
                {
                    // Refresh the file list
                    await LoadFilesAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Upload error: {ex.Message}");
            }
        }

        private async void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            var input = new TextBox
            {
                PlaceholderText = "Folder name",
                Width = 300
            };

            var dialog = new ContentDialog
            {
                Title = "Create New Folder",
                Content = input,
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var folderName = input.Text?.Trim();
                if (!string.IsNullOrEmpty(folderName))
                {
                    var success = await _apiService.CreateFolderAsync(folderName, _currentFolder);
                    if (success)
                    {
                        await LoadFilesAsync();
                    }
                }
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                _searchQuery = sender.Text;
                _currentPage = 1;
                
                // Debounce search
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await Task.Delay(300);
                    if (_searchQuery == sender.Text)
                    {
                        await LoadFilesAsync();
                    }
                });
            }
        }

        private void Sort_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (SortComboBox.SelectedItem is ComboBoxItem item && item.Tag is string sortBy)
            {
                _sortBy = sortBy;
                _currentPage = 1;
                _ = LoadFilesAsync();
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadStorageDataAsync();
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                _ = LoadFilesAsync();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                _ = LoadFilesAsync();
            }
        }

        private void FileRow_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Optional: Add hover effect
        }

        private void FileRow_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // Optional: Remove hover effect
        }

        private void FileMenu_Click(object sender, RoutedEventArgs e)
        {
            // Menu flyout handles this
        }

        private async void DuplicateCleanup_Click(object sender, RoutedEventArgs e)
        {
            DuplicateCleanupBtn.IsEnabled = false;
            
            try
            {
                // Show scanning progress
                var scanDialog = new ContentDialog
                {
                    Title = "Scanning for Duplicates",
                    Content = new StackPanel
                    {
                        Spacing = 16,
                        Children =
                        {
                            new ProgressRing { IsActive = true, Width = 40, Height = 40, HorizontalAlignment = HorizontalAlignment.Center },
                            new TextBlock { Text = "Scanning your storage for duplicate files...", HorizontalAlignment = HorizontalAlignment.Center }
                        }
                    },
                    XamlRoot = this.XamlRoot
                };
                
                // Show dialog then start scan
                _ = scanDialog.ShowAsync();
                var result = await _apiService.DedupScanAsync();
                scanDialog.Hide();
                
                if (result == null || result.Groups.Count == 0)
                {
                    // No duplicates found
                    await ShowNoDuplicatesDialog();
                    return;
                }
                
                // Show duplicates found
                await ShowDuplicatesDialog(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DuplicateCleanup error: {ex.Message}");
                await ShowErrorDialog("Scan Failed", "Failed to scan for duplicates. Please try again.");
            }
            finally
            {
                DuplicateCleanupBtn.IsEnabled = true;
            }
        }

        private async Task ShowNoDuplicatesDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "No Duplicates Found",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE8FB", FontSize = 48, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green), HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock { Text = "Great news! All your files are unique.", HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock { Text = "Nothing to clean up right now.", Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], HorizontalAlignment = HorizontalAlignment.Center }
                    }
                },
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async Task ShowDuplicatesDialog(DedupScanResponse result)
        {
            var selectedGroups = new HashSet<int>();
            
            var content = new StackPanel { Spacing = 16, MaxWidth = 600 };
            
            // Stats header
            if (result.Stats != null)
            {
                var statsPanel = new Border
                {
                    Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16),
                    Child = new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock { Text = $"Found {result.Stats.DuplicateFiles} duplicate files", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                            new TextBlock 
                            { 
                                Text = $"Potential savings: {FormatFileSize(result.Stats.PotentialSavings)}", 
                                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green)
                            }
                        }
                    }
                };
                content.Children.Add(statsPanel);
            }
            
            // Duplicate groups list
            var groupsScroll = new ScrollViewer { MaxHeight = 300, Padding = new Thickness(0, 8, 0, 0) };
            var groupsList = new StackPanel { Spacing = 8 };
            
            for (int i = 0; i < result.Groups.Count && i < 20; i++)
            {
                var group = result.Groups[i];
                var groupIndex = i;
                
                var checkbox = new CheckBox 
                { 
                    IsChecked = true, 
                    Content = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = group.GroupKey?.FileName ?? "Unknown", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                            new TextBlock 
                            { 
                                Text = $"{group.Count} copies • {FormatFileSize(group.TotalBytes)} total", 
                                FontSize = 12,
                                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                            }
                        }
                    }
                };
                
                selectedGroups.Add(groupIndex);
                checkbox.Checked += (s, e) => selectedGroups.Add(groupIndex);
                checkbox.Unchecked += (s, e) => selectedGroups.Remove(groupIndex);
                
                groupsList.Children.Add(checkbox);
            }
            
            if (result.Groups.Count > 20)
            {
                groupsList.Children.Add(new TextBlock 
                { 
                    Text = $"... and {result.Groups.Count - 20} more groups",
                    FontStyle = Windows.UI.Text.FontStyle.Italic,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
            }
            
            groupsScroll.Content = groupsList;
            content.Children.Add(groupsScroll);
            
            // Info text
            content.Children.Add(new TextBlock
            {
                Text = "Selected duplicates will be removed. The original file in each group will be kept.",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            
            var dialog = new ContentDialog
            {
                Title = "Duplicate Files Found",
                Content = content,
                PrimaryButtonText = "Merge & Remove Duplicates",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };
            
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && selectedGroups.Count > 0)
            {
                await MergeDuplicates(result, selectedGroups);
            }
        }

        private async Task MergeDuplicates(DedupScanResponse scanResult, HashSet<int> selectedIndices)
        {
            var groups = selectedIndices
                .Where(i => i < scanResult.Groups.Count)
                .Select(i => scanResult.Groups[i])
                .Select(g => new DedupMergeGroup
                {
                    PrimaryId = g.Primary?.Id,
                    Duplicates = g.Duplicates.Select(d => d.Id ?? "").Where(id => !string.IsNullOrEmpty(id)).ToList()
                })
                .Where(g => !string.IsNullOrEmpty(g.PrimaryId) && g.Duplicates.Count > 0)
                .ToList();
            
            if (groups.Count == 0)
            {
                await ShowErrorDialog("No Selection", "No valid duplicate groups selected.");
                return;
            }
            
            var mergeResult = await _apiService.DedupMergeAsync(groups);
            
            if (mergeResult != null && mergeResult.Success)
            {
                await ShowSuccessDialog(
                    "Duplicates Removed",
                    $"Removed {mergeResult.RemovedFiles} files and freed {FormatFileSize(mergeResult.FreedBytes)}."
                );
                AddActivity("Duplicate Cleanup", $"Removed {mergeResult.RemovedFiles} duplicate files", "\uE8C8");
                await LoadFilesAsync();
            }
            else
            {
                await ShowErrorDialog("Merge Failed", "Failed to remove duplicates. Please try again.");
            }
        }

        private async Task ShowSuccessDialog(string title, string message)
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

        private async Task ShowErrorDialog(string title, string message)
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

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        private async void StoragePlans_Click(object sender, RoutedEventArgs e)
        {
            var contentStack = new StackPanel { Spacing = 16 };
            
            // Current plan
            contentStack.Children.Add(new TextBlock 
            { 
                Text = "Current Plan: Free (1 TB)",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
            });
            
            // Available plans
            var plansPanel = new StackPanel { Spacing = 12 };
            
            var plans = new[]
            {
                ("Pro", "5 TB", "$9.99/mo"),
                ("Business", "10 TB", "$19.99/mo"),
                ("Enterprise", "Unlimited", "Contact Sales")
            };
            
            foreach (var (name, storage, price) in plans)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                var infoStack = new StackPanel();
                infoStack.Children.Add(new TextBlock { Text = name, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
                infoStack.Children.Add(new TextBlock { Text = storage, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
                grid.Children.Add(infoStack);
                
                var priceText = new TextBlock 
                { 
                    Text = price, 
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(priceText, 1);
                grid.Children.Add(priceText);
                
                var planCard = new Border
                {
                    Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16),
                    Child = grid
                };
                
                plansPanel.Children.Add(planCard);
            }
            
            contentStack.Children.Add(plansPanel);
            
            var dialog = new ContentDialog
            {
                Title = "Storage Plans",
                Content = contentStack,
                PrimaryButtonText = "Learn More",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };
            
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                // Open plans page in browser
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://sensepc.com/storage/plans"));
            }
        }

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is StorageItem item)
            {
                if (item.IsFolder)
                {
                    // Download folder as ZIP
                    var downloadUrl = await _apiService.GetFolderDownloadUrlAsync(item.FileName, _currentFolder);
                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        await Windows.System.Launcher.LaunchUriAsync(new Uri(downloadUrl));
                    }
                    else
                    {
                        var dialog = new ContentDialog
                        {
                            Title = "Download Failed",
                            Content = "Could not generate folder download URL. Please try again.",
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await dialog.ShowAsync();
                    }
                }
                else
                {
                    // Download single file
                    var downloadUrl = await _apiService.GetDownloadUrlAsync(item.FileName, _currentFolder, item.Id);
                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        await Windows.System.Launcher.LaunchUriAsync(new Uri(downloadUrl));
                    }
                }
            }
        }

        private async void Star_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is StorageItem item)
            {
                await _apiService.ToggleStarAsync(item.Id, !item.Starred);
                await LoadFilesAsync();
            }
        }

        private void CopyName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is StorageItem item)
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(item.FileName);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                
                // Show a brief tooltip or notification would be nice, but for now just copy silently
            }
        }

        private async void Preview_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is StorageItem item)
            {
                // Check if it's a folder
                if (item.IsFolder)
                {
                    var folderDialog = new ContentDialog
                    {
                        Title = "Cannot Preview Folder",
                        Content = "Folders cannot be previewed. Double-click to open the folder instead.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await folderDialog.ShowAsync();
                    return;
                }

                // Get download URL and show preview
                var downloadUrl = await _apiService.GetDownloadUrlAsync(item.FileName, _currentFolder, item.Id);
                
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "Preview Failed",
                        Content = "Could not generate preview URL. Please try again.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    return;
                }

                // Create preview content based on file type
                object previewContent;
                var fileExt = System.IO.Path.GetExtension(item.FileName).ToLower();
                
                // Image extensions
                var imageExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".ico" };
                // Video extensions
                var videoExts = new[] { ".mp4", ".webm", ".mov", ".avi", ".wmv" };
                // Audio extensions
                var audioExts = new[] { ".mp3", ".wav", ".ogg", ".m4a", ".flac" };
                // PDF
                var isPdf = fileExt == ".pdf";
                // Office documents
                var officeExts = new[] { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx" };
                
                if (imageExts.Contains(fileExt))
                {
                    // Image preview with zoom capability
                    var image = new Microsoft.UI.Xaml.Controls.Image
                    {
                        Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(downloadUrl)),
                        MaxWidth = 700,
                        MaxHeight = 500,
                        Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform
                    };
                    
                    var imageContainer = new Border
                    {
                        Background = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(16),
                        Child = image
                    };
                    
                    previewContent = new ScrollViewer
                    {
                        Content = imageContainer,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        MaxHeight = 550
                    };
                }
                else if (videoExts.Contains(fileExt))
                {
                    // Video preview with media player
                    var mediaPlayer = new MediaPlayerElement
                    {
                        Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(downloadUrl)),
                        AutoPlay = false,
                        AreTransportControlsEnabled = true,
                        MaxWidth = 700,
                        MaxHeight = 400
                    };
                    mediaPlayer.TransportControls.IsCompact = false;
                    mediaPlayer.TransportControls.IsZoomButtonVisible = true;
                    
                    var videoContainer = new Border
                    {
                        Background = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        CornerRadius = new CornerRadius(8),
                        Child = mediaPlayer
                    };
                    
                    previewContent = videoContainer;
                }
                else if (audioExts.Contains(fileExt))
                {
                    // Audio preview with media player
                    var mediaPlayer = new MediaPlayerElement
                    {
                        Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(downloadUrl)),
                        AutoPlay = false,
                        AreTransportControlsEnabled = true,
                        MaxWidth = 500,
                        Height = 80
                    };
                    mediaPlayer.TransportControls.IsCompact = true;
                    
                    previewContent = new StackPanel
                    {
                        Spacing = 16,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children =
                        {
                            new FontIcon
                            {
                                Glyph = "\uE8D6", // Music icon
                                FontSize = 64,
                                Foreground = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                            },
                            new TextBlock 
                            { 
                                Text = item.FileName, 
                                HorizontalAlignment = HorizontalAlignment.Center, 
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold 
                            },
                            mediaPlayer
                        }
                    };
                }
                else if (isPdf)
                {
                    // PDF - open in browser/system viewer (WinUI doesn't have native PDF viewer)
                    previewContent = new StackPanel
                    {
                        Spacing = 16,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children =
                        {
                            new FontIcon
                            {
                                Glyph = "\uEA90", // PDF icon
                                FontSize = 64,
                                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red)
                            },
                            new TextBlock 
                            { 
                                Text = item.FileName, 
                                HorizontalAlignment = HorizontalAlignment.Center, 
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold 
                            },
                            new TextBlock 
                            { 
                                Text = $"Size: {item.DisplaySize}", 
                                HorizontalAlignment = HorizontalAlignment.Center, 
                                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] 
                            },
                            new Button
                            {
                                Content = new StackPanel
                                {
                                    Orientation = Orientation.Horizontal,
                                    Spacing = 8,
                                    Children =
                                    {
                                        new FontIcon { Glyph = "\uE8A7", FontSize = 14 },
                                        new TextBlock { Text = "Open in PDF Viewer" }
                                    }
                                },
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Tag = downloadUrl
                            }
                        }
                    };
                    
                    // Wire up button click to open PDF in browser
                    if (((StackPanel)previewContent).Children.LastOrDefault() is Button pdfButton)
                    {
                        pdfButton.Click += async (s, args) =>
                        {
                            if (((Button)s).Tag is string url)
                            {
                                await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
                            }
                        };
                    }
                }
                else if (officeExts.Contains(fileExt))
                {
                    // Office documents - offer to open in Office Online
                    var officeViewerUrl = $"https://view.officeapps.live.com/op/embed.aspx?src={Uri.EscapeDataString(downloadUrl)}";
                    
                    previewContent = new StackPanel
                    {
                        Spacing = 16,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children =
                        {
                            new FontIcon
                            {
                                Glyph = "\uE8A5", // Document icon
                                FontSize = 64,
                                Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
                            },
                            new TextBlock 
                            { 
                                Text = item.FileName, 
                                HorizontalAlignment = HorizontalAlignment.Center, 
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold 
                            },
                            new TextBlock 
                            { 
                                Text = $"Size: {item.DisplaySize}", 
                                HorizontalAlignment = HorizontalAlignment.Center, 
                                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] 
                            },
                            new Button
                            {
                                Content = new StackPanel
                                {
                                    Orientation = Orientation.Horizontal,
                                    Spacing = 8,
                                    Children =
                                    {
                                        new FontIcon { Glyph = "\uE8A7", FontSize = 14 },
                                        new TextBlock { Text = "Open in Office Online" }
                                    }
                                },
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Tag = officeViewerUrl
                            }
                        }
                    };
                    
                    // Wire up button click
                    if (((StackPanel)previewContent).Children.LastOrDefault() is Button officeButton)
                    {
                        officeButton.Click += async (s, args) =>
                        {
                            if (((Button)s).Tag is string url)
                            {
                                await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
                            }
                        };
                    }
                }
                else
                {
                    // For other file types, show file info and offer to download
                    previewContent = new StackPanel
                    {
                        Spacing = 16,
                        Children =
                        {
                            new FontIcon
                            {
                                Glyph = item.FileTypeIcon ?? "\uE7C3",
                                FontSize = 64,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Foreground = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                            },
                            new TextBlock { Text = item.FileName, HorizontalAlignment = HorizontalAlignment.Center, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                            new TextBlock { Text = $"Size: {item.DisplaySize}", HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] },
                            new TextBlock { Text = $"Modified: {item.DisplayDate}", HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] },
                            new TextBlock { Text = "This file type cannot be previewed directly. Click Download to view.", TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) }
                        }
                    };
                }

                var previewDialog = new ContentDialog
                {
                    Title = $"Preview: {item.FileName}",
                    Content = previewContent,
                    PrimaryButtonText = "Download",
                    CloseButtonText = "Close",
                    XamlRoot = this.XamlRoot
                };

                if (await previewDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    // Download the file
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(downloadUrl));
                }
            }
        }

        private async void StopSharing_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is StorageItem item)
            {
                // First, get the share ID for this file
                var shareId = await _apiService.GetShareIdForFileAsync(item.Id);
                
                if (string.IsNullOrEmpty(shareId))
                {
                    var noShareDialog = new ContentDialog
                    {
                        Title = "Not Shared",
                        Content = $"\"{item.FileName}\" is not currently shared.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await noShareDialog.ShowAsync();
                    return;
                }

                // Confirm stop sharing
                var confirmDialog = new ContentDialog
                {
                    Title = "Stop Sharing",
                    Content = $"Are you sure you want to stop sharing \"{item.FileName}\"? Anyone with the share link will no longer be able to access this file.",
                    PrimaryButtonText = "Stop Sharing",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                if (await confirmDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    var success = await _apiService.CancelShareAsync(shareId);
                    
                    if (success)
                    {
                        var successDialog = new ContentDialog
                        {
                            Title = "Sharing Stopped",
                            Content = $"\"{item.FileName}\" is no longer shared.",
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await successDialog.ShowAsync();
                        await LoadFilesAsync();
                    }
                    else
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "Error",
                            Content = "Failed to stop sharing. Please try again.",
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                    }
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is StorageItem item)
            {
                var dialog = new ContentDialog
                {
                    Title = "Delete File",
                    Content = $"Are you sure you want to delete \"{item.FileName}\"?",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    await _apiService.DeleteStorageFileAsync(item.FileName, _currentFolder, item.Id);
                    await LoadFilesAsync();
                }
            }
        }

        private async void Share_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is StorageItem item)
            {
                var dialog = new Dialogs.ShareDialog(item, this.XamlRoot, _currentFolder);
                await dialog.ShowAsync();
                
                if (dialog.FileShared)
                {
                    await LoadFilesAsync();
                }
            }
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is StorageItem item)
            {
                var input = new TextBox
                {
                    Text = item.FileName,
                    PlaceholderText = "Enter new name",
                    Width = 300
                };
                input.SelectAll();

                var dialog = new ContentDialog
                {
                    Title = "Rename",
                    Content = input,
                    PrimaryButtonText = "Rename",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    var newName = input.Text?.Trim();
                    if (!string.IsNullOrEmpty(newName) && newName != item.FileName)
                    {
                        var success = await _apiService.RenameFileAsync(item.FileName, newName, _currentFolder, item.Id);
                        if (success)
                        {
                            await LoadFilesAsync();
                        }
                        else
                        {
                            var errorDialog = new ContentDialog
                            {
                                Title = "Rename Failed",
                                Content = "Could not rename the file. Please try again.",
                                CloseButtonText = "OK",
                                XamlRoot = this.XamlRoot
                            };
                            await errorDialog.ShowAsync();
                        }
                    }
                }
            }
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is StorageItem item)
            {
                var input = new TextBox
                {
                    Text = "",
                    PlaceholderText = "Enter destination folder (leave empty for root)",
                    Width = 300
                };

                var dialog = new ContentDialog
                {
                    Title = $"Copy \"{item.FileName}\"",
                    Content = new StackPanel
                    {
                        Spacing = 12,
                        Children =
                        {
                            new TextBlock { Text = "Enter the destination folder path:", TextWrapping = TextWrapping.Wrap },
                            input
                        }
                    },
                    PrimaryButtonText = "Copy",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    var destFolder = input.Text?.Trim();
                    var success = await _apiService.CopyFileAsync(item.FileName, _currentFolder ?? "", destFolder ?? "", item.Id);
                    if (success)
                    {
                        await LoadFilesAsync();
                    }
                    else
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "Copy Failed",
                            Content = "Could not copy the file. Please try again.",
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                    }
                }
            }
        }

        private async void Move_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is StorageItem item)
            {
                var input = new TextBox
                {
                    Text = "",
                    PlaceholderText = "Enter destination folder (leave empty for root)",
                    Width = 300
                };

                var dialog = new ContentDialog
                {
                    Title = $"Move \"{item.FileName}\"",
                    Content = new StackPanel
                    {
                        Spacing = 12,
                        Children =
                        {
                            new TextBlock { Text = "Enter the destination folder path:", TextWrapping = TextWrapping.Wrap },
                            input
                        }
                    },
                    PrimaryButtonText = "Move",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    var destFolder = input.Text?.Trim();
                    var success = await _apiService.MoveFileAsync(item.FileName, _currentFolder ?? "", destFolder ?? "", item.Id);
                    if (success)
                    {
                        await LoadFilesAsync();
                    }
                    else
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "Move Failed",
                            Content = "Could not move the file. Please try again.",
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                    }
                }
            }
        }

        #region Drag-Drop Support

        private void ContentArea_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Drop to upload";
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.IsGlyphVisible = true;
                
                // Visual feedback - highlight the content area
                ContentAreaGrid.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorSelectedBrush"];
            }
        }

        private void ContentArea_DragLeave(object sender, DragEventArgs e)
        {
            // Remove visual feedback
            ContentAreaGrid.Background = null;
        }

        private async void ContentArea_Drop(object sender, DragEventArgs e)
        {
            // Remove visual feedback
            ContentAreaGrid.Background = null;
            
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var files = items.OfType<Windows.Storage.StorageFile>().ToList();
                
                if (files.Count > 0)
                {
                    await UploadDroppedFilesAsync(files);
                }
            }
        }

        private async Task UploadDroppedFilesAsync(List<Windows.Storage.StorageFile> files)
        {
            // Show uploading indicator
            var statusDialog = new ContentDialog
            {
                Title = "Uploading Files",
                Content = new StackPanel
                {
                    Spacing = 16,
                    Children =
                    {
                        new ProgressRing { IsActive = true, Width = 40, Height = 40 },
                        new TextBlock { Text = $"Uploading {files.Count} file(s)...", HorizontalAlignment = HorizontalAlignment.Center }
                    }
                },
                XamlRoot = this.XamlRoot
            };

            _ = statusDialog.ShowAsync();

            int completed = 0;
            foreach (var file in files)
            {
                try
                {
                    var props = await file.GetBasicPropertiesAsync();
                    var contentType = file.ContentType ?? "application/octet-stream";
                    
                    // Get upload URL
                    var uploadResponse = await _apiService.GetUploadUrlAsync(
                        file.Name,
                        contentType,
                        (long)props.Size,
                        _currentFolder
                    );

                    if (uploadResponse != null && !string.IsNullOrEmpty(uploadResponse.UploadUrl))
                    {
                        // Read file bytes
                        using var stream = await file.OpenStreamForReadAsync();
                        using var ms = new System.IO.MemoryStream();
                        await stream.CopyToAsync(ms);
                        var bytes = ms.ToArray();

                        // Upload to presigned URL
                        var uploadSuccess = await _apiService.UploadToPresignedUrlAsync(
                            uploadResponse.UploadUrl,
                            bytes,
                            contentType
                        );

                        // Confirm upload
                        if (uploadSuccess && !string.IsNullOrEmpty(uploadResponse.Key))
                        {
                            await _apiService.ConfirmUploadAsync(
                                uploadResponse.FinalFileName ?? file.Name,
                                contentType,
                                (long)props.Size,
                                uploadResponse.Key,
                                _currentFolder
                            );
                            completed++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Drop upload error for {file.Name}: {ex.Message}");
                }
            }

            statusDialog.Hide();

            // Refresh file list
            await LoadFilesAsync();
        }

        #endregion

        #region Bulk Operations

        private void FilesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedCount = FilesListView.SelectedItems.Count;
            if (selectedCount > 0)
            {
                BulkDownloadButton.Visibility = Visibility.Visible;
                BulkDownloadText.Text = $"Download ({selectedCount})";
                BulkShareButton.Visibility = Visibility.Visible;
                BulkShareText.Text = $"Share ({selectedCount})";
                BulkDeleteButton.Visibility = Visibility.Visible;
                BulkDeleteText.Text = $"Delete ({selectedCount})";
            }
            else
            {
                BulkDownloadButton.Visibility = Visibility.Collapsed;
                BulkShareButton.Visibility = Visibility.Collapsed;
                BulkDeleteButton.Visibility = Visibility.Collapsed;
                BulkDeleteText.Text = "Delete";
            }
        }

        private async void BulkDownload_Click(object sender, RoutedEventArgs e)
        {
            // Get selected items from whichever view is active
            var selectedItems = _isGridView 
                ? FilesGridView.SelectedItems.Cast<StorageItem>().ToList()
                : FilesListView.SelectedItems.Cast<StorageItem>().ToList();
            
            if (selectedItems.Count == 0) return;

            // Filter out folders - only download files
            var filesToDownload = selectedItems.Where(f => !f.IsFolder).ToList();
            
            if (filesToDownload.Count == 0)
            {
                var noFilesDialog = new ContentDialog
                {
                    Title = "No Files Selected",
                    Content = "Only files can be downloaded. Folders are not supported for bulk download.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await noFilesDialog.ShowAsync();
                return;
            }

            if (filesToDownload.Count != selectedItems.Count)
            {
                // Some folders were filtered out
                var warningDialog = new ContentDialog
                {
                    Title = "Folders Skipped",
                    Content = $"{selectedItems.Count - filesToDownload.Count} folder(s) will be skipped. Only {filesToDownload.Count} file(s) will be downloaded.",
                    PrimaryButtonText = "Continue",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.XamlRoot
                };
                
                if (await warningDialog.ShowAsync() != ContentDialogResult.Primary)
                    return;
            }

            var progressText = new TextBlock { Text = $"Downloading 0/{filesToDownload.Count}...", HorizontalAlignment = HorizontalAlignment.Center };
            var progressDialog = new ContentDialog
            {
                Title = "Downloading Files",
                Content = new StackPanel
                {
                    Spacing = 16,
                    Children =
                    {
                        new ProgressRing { IsActive = true, Width = 40, Height = 40 },
                        progressText
                    }
                },
                XamlRoot = this.XamlRoot
            };

            _ = progressDialog.ShowAsync();

            int downloaded = 0;
            foreach (var file in filesToDownload)
            {
                try
                {
                    var downloadUrl = await _apiService.GetDownloadUrlAsync(file.FileName, _currentFolder, file.Id);
                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        await Windows.System.Launcher.LaunchUriAsync(new Uri(downloadUrl));
                        downloaded++;
                        progressText.Text = $"Downloading {downloaded}/{filesToDownload.Count}...";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Download error: {ex.Message}");
                }
                
                // Small delay between downloads to prevent overwhelming the browser
                await Task.Delay(500);
            }

            progressDialog.Hide();

            // Show completion message
            var completionDialog = new ContentDialog
            {
                Title = "Download Complete",
                Content = $"Successfully started download for {downloaded} of {filesToDownload.Count} file(s).",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await completionDialog.ShowAsync();
        }

        private async void BulkShare_Click(object sender, RoutedEventArgs e)
        {
            // Get selected items from whichever view is active
            var selectedItems = _isGridView 
                ? FilesGridView.SelectedItems.Cast<StorageItem>().ToList()
                : FilesListView.SelectedItems.Cast<StorageItem>().ToList();
            
            if (selectedItems.Count == 0) return;

            // Filter out folders - only share files
            var filesToShare = selectedItems.Where(f => !f.IsFolder).ToList();
            
            if (filesToShare.Count == 0)
            {
                var noFilesDialog = new ContentDialog
                {
                    Title = "No Files Selected",
                    Content = "Only files can be shared. Folders are not supported for bulk share.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await noFilesDialog.ShowAsync();
                return;
            }

            if (filesToShare.Count != selectedItems.Count)
            {
                // Some folders were filtered out
                var warningDialog = new ContentDialog
                {
                    Title = "Folders Skipped",
                    Content = $"{selectedItems.Count - filesToShare.Count} folder(s) will be skipped. Only {filesToShare.Count} file(s) will be shared.",
                    PrimaryButtonText = "Continue",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.XamlRoot
                };
                
                if (await warningDialog.ShowAsync() != ContentDialogResult.Primary)
                    return;
            }

            // Show progress dialog
            var progressText = new TextBlock { Text = $"Generating share links...", HorizontalAlignment = HorizontalAlignment.Center };
            var progressDialog = new ContentDialog
            {
                Title = "Sharing Files",
                Content = new StackPanel
                {
                    Spacing = 16,
                    Children =
                    {
                        new ProgressRing { IsActive = true, Width = 40, Height = 40 },
                        progressText
                    }
                },
                XamlRoot = this.XamlRoot
            };

            _ = progressDialog.ShowAsync();

            var shareLinks = new List<string>();
            int shared = 0;
            
            foreach (var file in filesToShare)
            {
                try
                {
                    progressText.Text = $"Sharing {shared + 1}/{filesToShare.Count}...";
                    var shareUrl = await _apiService.GenerateShareLinkAsync(file.FileName, _currentFolder, file.Id);
                    if (!string.IsNullOrEmpty(shareUrl))
                    {
                        shareLinks.Add($"{file.FileName}: {shareUrl}");
                        shared++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Share error: {ex.Message}");
                }
            }

            progressDialog.Hide();

            if (shareLinks.Count > 0)
            {
                // Copy all share links to clipboard
                var allLinks = string.Join("\n", shareLinks);
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(allLinks);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

                // Show completion message with links
                var linksTextBlock = new TextBlock
                {
                    Text = allLinks,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                    MaxWidth = 400
                };

                var completionDialog = new ContentDialog
                {
                    Title = "Share Links Generated",
                    Content = new StackPanel
                    {
                        Spacing = 12,
                        Children =
                        {
                            new TextBlock { Text = $"Successfully generated {shared} share link(s). Links copied to clipboard!" },
                            new ScrollViewer { Content = linksTextBlock, MaxHeight = 200 }
                        }
                    },
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await completionDialog.ShowAsync();
            }
            else
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Share Failed",
                    Content = "Failed to generate share links. Please try again.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        private async void BulkDelete_Click(object sender, RoutedEventArgs e)
        {
            // Get selected items from whichever view is active
            var selectedItems = _isGridView 
                ? FilesGridView.SelectedItems.Cast<StorageItem>().ToList()
                : FilesListView.SelectedItems.Cast<StorageItem>().ToList();
            
            if (selectedItems.Count == 0) return;

            var dialog = new ContentDialog
            {
                Title = "Delete Files",
                Content = $"Are you sure you want to delete {selectedItems.Count} selected item(s)? This action cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var progressDialog = new ContentDialog
                {
                    Title = "Deleting Files",
                    Content = new StackPanel
                    {
                        Spacing = 16,
                        Children =
                        {
                            new ProgressRing { IsActive = true, Width = 40, Height = 40 },
                            new TextBlock { Text = $"Deleting {selectedItems.Count} file(s)...", HorizontalAlignment = HorizontalAlignment.Center }
                        }
                    },
                    XamlRoot = this.XamlRoot
                };

                _ = progressDialog.ShowAsync();

                int deleted = 0;
                foreach (var item in selectedItems)
                {
                    var success = await _apiService.DeleteStorageFileAsync(item.FileName, _currentFolder, item.Id);
                    if (success) deleted++;
                }

                progressDialog.Hide();

                // Refresh file list
                await LoadFilesAsync();
                
                // Add activity
                AddActivity("Deleted", $"{deleted} file(s)", "&#xE74D;");
            }
        }

        #endregion

        #region Activity Tracking

        private void AddActivity(string action, string target, string iconGlyph)
        {
            var activity = new ActivityItem
            {
                Action = action,
                Target = target,
                IconGlyph = iconGlyph,
                Timestamp = DateTime.Now
            };

            _recentActivity.Insert(0, activity);
            
            // Keep only the most recent items
            while (_recentActivity.Count > MaxActivityItems)
            {
                _recentActivity.RemoveAt(_recentActivity.Count - 1);
            }

            UpdateActivityUI();
        }

        private void RefreshActivity_Click(object sender, RoutedEventArgs e)
        {
            UpdateActivityUI();
        }

        private void UpdateActivityUI()
        {
            ActivityListPanel.Children.Clear();

            if (_recentActivity.Count == 0)
            {
                ActivityEmptyText.Visibility = Visibility.Visible;
                return;
            }

            ActivityEmptyText.Visibility = Visibility.Collapsed;

            foreach (var activity in _recentActivity.Take(10))
            {
                var activityItem = new Border
                {
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(4),
                    Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                    Child = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = new GridLength(24) },
                            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                        },
                        Children =
                        {
                            new FontIcon
                            {
                                Glyph = activity.IconGlyph,
                                FontSize = 12,
                                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                                VerticalAlignment = VerticalAlignment.Center
                            },
                            CreateActivityContent(activity)
                        }
                    }
                };

                ActivityListPanel.Children.Add(activityItem);
            }
        }

        private static StackPanel CreateActivityContent(ActivityItem activity)
        {
            var panel = new StackPanel { Spacing = 2 };
            Grid.SetColumn(panel, 1);
            
            panel.Children.Add(new TextBlock
            {
                Text = $"{activity.Action}: {activity.Target}",
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 160
            });
            
            panel.Children.Add(new TextBlock
            {
                Text = activity.Timestamp.ToString("h:mm tt"),
                FontSize = 10,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
            });

            return panel;
        }

        #endregion

        #region Cloud Storage Connections

        private async void ConnectDropbox_Click(object sender, RoutedEventArgs e)
        {
            await ShowComingSoonDialog("Dropbox", "Sync your SensePC Cloud with Dropbox for seamless file access across all your devices.");
        }

        private async void ConnectGoogleDrive_Click(object sender, RoutedEventArgs e)
        {
            await ShowComingSoonDialog("Google Drive", "Connect your Google Drive to access and sync files directly from SensePC Cloud.");
        }

        private async void ConnectiCloud_Click(object sender, RoutedEventArgs e)
        {
            await ShowComingSoonDialog("iCloud Drive", "Integrate with iCloud Drive to keep your files synchronized across your Apple ecosystem.");
        }

        private async Task ShowComingSoonDialog(string serviceName, string description)
        {
            var featuresPanel = new StackPanel { Spacing = 8 };
            
            // Feature list with checkmarks
            var features = new[]
            {
                "Automatic sync of your files",
                "Two-way synchronization",
                "Conflict resolution",
                "Selective folder sync",
                "Background sync while working"
            };
            
            foreach (var feature in features)
            {
                var featureRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                featureRow.Children.Add(new FontIcon 
                { 
                    Glyph = "\uE73E", 
                    FontSize = 12, 
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green) 
                });
                featureRow.Children.Add(new TextBlock { Text = feature, FontSize = 13 });
                featuresPanel.Children.Add(featureRow);
            }
            
            var dialog = new ContentDialog
            {
                Title = $"{serviceName} Integration",
                Content = new StackPanel
                {
                    Spacing = 16,
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = description, 
                            TextWrapping = TextWrapping.Wrap 
                        },
                        featuresPanel,
                        new Border
                        {
                            Background = new SolidColorBrush(Microsoft.UI.Colors.DarkSlateBlue),
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(16),
                            Child = new StackPanel
                            {
                                Spacing = 8,
                                Children =
                                {
                                    new StackPanel
                                    {
                                        Orientation = Orientation.Horizontal,
                                        Spacing = 8,
                                        Children =
                                        {
                                            new FontIcon { Glyph = "\uE946", FontSize = 20, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) },
                                            new TextBlock 
                                            { 
                                                Text = "Coming Soon!", 
                                                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                                                FontSize = 16,
                                                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
                                            }
                                        }
                                    },
                                    new TextBlock
                                    {
                                        Text = "We're actively developing cloud storage integrations. This feature will be available in an upcoming release.",
                                        TextWrapping = TextWrapping.Wrap,
                                        FontSize = 12,
                                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray)
                                    }
                                }
                            }
                        }
                    }
                },
                PrimaryButtonText = "Notify Me",
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ShowSuccessDialog("Notification Set", $"We'll notify you when {serviceName} integration becomes available!");
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a recent activity item
    /// </summary>
    public class ActivityItem
    {
        public string Action { get; set; } = "";
        public string Target { get; set; } = "";
        public string IconGlyph { get; set; } = "\xE7C3";
        public DateTime Timestamp { get; set; }
    }
}
