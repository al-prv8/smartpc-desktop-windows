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
                var picker = new FileOpenPicker();
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");

                // Get the window handle
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var files = await picker.PickMultipleFilesAsync();
                if (files?.Count > 0)
                {
                    var progressDialog = new ContentDialog
                    {
                        Title = "Uploading Files",
                        Content = new StackPanel
                        {
                            Children =
                            {
                                new ProgressRing { IsActive = true, Width = 48, Height = 48 },
                                new TextBlock { Text = $"Uploading {files.Count} file(s)...", Margin = new Thickness(0, 16, 0, 0) }
                            }
                        },
                        XamlRoot = this.XamlRoot
                    };

                    _ = progressDialog.ShowAsync();

                    foreach (var file in files)
                    {
                        try
                        {
                            var props = await file.GetBasicPropertiesAsync();
                            var uploadResponse = await _apiService.GetUploadUrlAsync(
                                file.Name,
                                file.ContentType ?? "application/octet-stream",
                                (long)props.Size,
                                _currentFolder
                            );

                            if (uploadResponse != null && !string.IsNullOrEmpty(uploadResponse.UploadUrl))
                            {
                                using var stream = await file.OpenStreamForReadAsync();
                                var bytes = new byte[stream.Length];
                                await stream.ReadAsync(bytes, 0, bytes.Length);

                                var uploadSuccess = await _apiService.UploadToPresignedUrlAsync(
                                    uploadResponse.UploadUrl,
                                    bytes,
                                    file.ContentType ?? "application/octet-stream"
                                );

                                if (uploadSuccess && !string.IsNullOrEmpty(uploadResponse.Key))
                                {
                                    await _apiService.ConfirmUploadAsync(
                                        uploadResponse.FinalFileName ?? file.Name,
                                        file.ContentType ?? "application/octet-stream",
                                        (long)props.Size,
                                        uploadResponse.Key,
                                        _currentFolder
                                    );
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Upload file error: {ex.Message}");
                        }
                    }

                    progressDialog.Hide();

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

        private void StoragePlans_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Show storage plans dialog
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

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && 
                menuItem.Parent is MenuFlyout flyout &&
                flyout.Target is Button button &&
                button.Tag is StorageItem item)
            {
                var downloadUrl = await _apiService.GetDownloadUrlAsync(item.FileName, _currentFolder, item.Id);
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    // Open download URL in browser or use Windows.System.Launcher
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(downloadUrl));
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
    }
}
