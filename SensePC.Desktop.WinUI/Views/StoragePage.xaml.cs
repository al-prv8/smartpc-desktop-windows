using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
                if (_selectedCategory.Name == "Starred")
                {
                    request.Starred = true;
                }
                else if (_selectedCategory.Name == "Shared")
                {
                    request.Shared = true;
                }
                else if (!string.IsNullOrEmpty(_selectedCategory.FilterType))
                {
                    request.Type = _selectedCategory.FilterType;
                }

                var result = await _apiService.ListFilesAsync(request);

                if (result?.Files?.Any() == true)
                {
                    FilesListView.ItemsSource = result.Files;
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
            PaginationPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowEmptyState()
        {
            LoadingState.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
            FilesListView.Visibility = Visibility.Collapsed;
            PaginationPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowFilesState()
        {
            LoadingState.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Collapsed;
            FilesListView.Visibility = Visibility.Visible;
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

        private void ViewToggle_Click(object sender, RoutedEventArgs e)
        {
            // Note: Grid view toggle not implemented in current XAML layout
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
            if (sender is MenuFlyoutItem menuItem && 
                menuItem.Parent is MenuFlyout flyout &&
                flyout.Target is Button button &&
                button.Tag is StorageItem item)
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
            if (sender is MenuFlyoutItem menuItem && 
                menuItem.Parent is MenuFlyout flyout &&
                flyout.Target is Button button &&
                button.Tag is StorageItem item)
            {
                await _apiService.ToggleStarAsync(item.Id, !item.Starred);
                await LoadFilesAsync();
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && 
                menuItem.Parent is MenuFlyout flyout &&
                flyout.Target is Button button &&
                button.Tag is StorageItem item)
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
            if (sender is MenuFlyoutItem menuItem && 
                menuItem.Parent is MenuFlyout flyout &&
                flyout.Target is Button button &&
                button.Tag is StorageItem item)
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
            if (sender is MenuFlyoutItem menuItem && 
                menuItem.Parent is MenuFlyout flyout &&
                flyout.Target is Button button &&
                button.Tag is StorageItem item)
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
            if (sender is MenuFlyoutItem menuItem && 
                menuItem.Parent is MenuFlyout flyout &&
                flyout.Target is Button button &&
                button.Tag is StorageItem item)
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
            if (sender is MenuFlyoutItem menuItem && 
                menuItem.Parent is MenuFlyout flyout &&
                flyout.Target is Button button &&
                button.Tag is StorageItem item)
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
                BulkDeleteButton.Visibility = Visibility.Visible;
                BulkDeleteText.Text = $"Delete ({selectedCount})";
            }
            else
            {
                BulkDeleteButton.Visibility = Visibility.Collapsed;
                BulkDeleteText.Text = "Delete";
            }
        }

        private async void BulkDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = FilesListView.SelectedItems.Cast<StorageItem>().ToList();
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
            }
        }

        #endregion
    }
}
