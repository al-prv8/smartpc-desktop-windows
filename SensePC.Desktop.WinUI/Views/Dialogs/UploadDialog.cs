using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SensePC.Desktop.WinUI.Models;
using SensePC.Desktop.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;

namespace SensePC.Desktop.WinUI.Views.Dialogs
{
    /// <summary>
    /// Dialog for uploading files to Sense Cloud with progress indicators
    /// </summary>
    public sealed class UploadDialog : ContentDialog
    {
        private readonly SensePCApiService _apiService;
        private readonly string? _currentFolder;
        private readonly ObservableCollection<UploadFileItem> _selectedFiles = new();
        
        // UI elements
        private ListView _filesList = null!;
        private Border _dropZone = null!;
        private TextBlock _dropZoneText = null!;
        private StackPanel _uploadingPanel = null!;
        private ProgressBar _overallProgress = null!;
        private TextBlock _statusText = null!;
        private TextBlock _errorText = null!;
        private Button _addFilesButton = null!;

        private bool _isUploading;
        
        public bool FilesUploaded { get; private set; }

        public UploadDialog(XamlRoot xamlRoot, string? currentFolder = null)
        {
            this.XamlRoot = xamlRoot;
            _currentFolder = currentFolder;
            _apiService = new SensePCApiService(new SecureStorage());

            Title = "Upload Files";
            PrimaryButtonText = "Upload";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;
            IsPrimaryButtonEnabled = false;

            BuildUI();

            PrimaryButtonClick += PrimaryButton_Click;
        }

        private void BuildUI()
        {
            var mainStack = new StackPanel { Spacing = 16, MinWidth = 480, MinHeight = 300 };

            // Header description
            var destinationText = string.IsNullOrEmpty(_currentFolder) ? "root folder" : _currentFolder;
            mainStack.Children.Add(new TextBlock
            {
                Text = $"Select files to upload to: {destinationText}",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8,
                FontSize = 14
            });

            // Drop zone
            _dropZone = new Border
            {
                Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(24),
                MinHeight = 120,
                AllowDrop = true
            };
            
            var dropZoneStack = new StackPanel 
            { 
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 12
            };
            
            dropZoneStack.Children.Add(new FontIcon 
            { 
                Glyph = "\uE898", 
                FontSize = 32,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
            });
            
            _dropZoneText = new TextBlock
            {
                Text = "Drag and drop files here",
                HorizontalAlignment = HorizontalAlignment.Center,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"]
            };
            dropZoneStack.Children.Add(_dropZoneText);
            
            dropZoneStack.Children.Add(new TextBlock
            {
                Text = "or",
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
            });
            
            _addFilesButton = new Button
            {
                Content = "Browse Files",
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(24, 8, 24, 8)
            };
            _addFilesButton.Click += AddFiles_Click;
            dropZoneStack.Children.Add(_addFilesButton);
            
            _dropZone.Child = dropZoneStack;
            
            // Wire up drag-drop events
            _dropZone.DragOver += DropZone_DragOver;
            _dropZone.Drop += DropZone_Drop;
            
            mainStack.Children.Add(_dropZone);

            // Selected files list
            var filesHeader = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            filesHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filesHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var filesLabel = new TextBlock
            {
                Text = "Selected Files",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
            };
            Grid.SetColumn(filesLabel, 0);
            filesHeader.Children.Add(filesLabel);
            
            var clearButton = new Button
            {
                Content = "Clear All",
                Padding = new Thickness(8, 4, 8, 4)
            };
            clearButton.Click += (s, e) => { _selectedFiles.Clear(); UpdateButtonState(); };
            Grid.SetColumn(clearButton, 1);
            filesHeader.Children.Add(clearButton);
            
            mainStack.Children.Add(filesHeader);

            // Simple file list display
            _filesList = new ListView
            {
                ItemsSource = _selectedFiles,
                SelectionMode = ListViewSelectionMode.None,
                MaxHeight = 150
            };
            mainStack.Children.Add(_filesList);

            // Upload progress section
            _uploadingPanel = new StackPanel { Spacing = 8, Visibility = Visibility.Collapsed };
            
            _statusText = new TextBlock
            {
                Text = "Uploading...",
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"]
            };
            _uploadingPanel.Children.Add(_statusText);
            
            _overallProgress = new ProgressBar
            {
                Value = 0,
                Maximum = 100,
                Height = 8
            };
            _uploadingPanel.Children.Add(_overallProgress);
            
            mainStack.Children.Add(_uploadingPanel);

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

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Drop to upload";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
            
            _dropZone.BorderThickness = new Thickness(3);
        }

        private async void DropZone_Drop(object sender, DragEventArgs e)
        {
            _dropZone.BorderThickness = new Thickness(2);
            
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                foreach (var item in items)
                {
                    if (item is StorageFile file)
                    {
                        var props = await file.GetBasicPropertiesAsync();
                        _selectedFiles.Add(new UploadFileItem
                        {
                            FileName = file.Name,
                            FilePath = file.Path,
                            FileSize = (long)props.Size,
                            ContentType = file.ContentType ?? "application/octet-stream",
                            StorageFile = file
                        });
                    }
                }
                UpdateButtonState();
            }
        }

        private async void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            
            // Initialize picker with window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    var props = await file.GetBasicPropertiesAsync();
                    _selectedFiles.Add(new UploadFileItem
                    {
                        FileName = file.Name,
                        FilePath = file.Path,
                        FileSize = (long)props.Size,
                        ContentType = file.ContentType ?? "application/octet-stream",
                        StorageFile = file
                    });
                }
                UpdateButtonState();
            }
        }

        private void UpdateButtonState()
        {
            IsPrimaryButtonEnabled = _selectedFiles.Count > 0 && !_isUploading;
        }

        private async void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (_selectedFiles.Count == 0)
            {
                args.Cancel = true;
                return;
            }

            var deferral = args.GetDeferral();
            _isUploading = true;

            try
            {
                _uploadingPanel.Visibility = Visibility.Visible;
                _errorText.Visibility = Visibility.Collapsed;
                _dropZone.IsHitTestVisible = false;
                _addFilesButton.IsEnabled = false;
                IsPrimaryButtonEnabled = false;

                int completed = 0;
                int total = _selectedFiles.Count;
                var errors = new List<string>();

                foreach (var fileItem in _selectedFiles.ToList())
                {
                    _statusText.Text = $"Uploading {fileItem.FileName} ({completed + 1}/{total})...";

                    try
                    {
                        // Step 1: Get presigned URL
                        var uploadResponse = await _apiService.GetUploadUrlAsync(
                            fileItem.FileName,
                            fileItem.ContentType,
                            fileItem.FileSize,
                            _currentFolder
                        );

                        if (uploadResponse == null)
                        {
                            errors.Add($"{fileItem.FileName}: No response from server");
                            continue;
                        }

                        if (!string.IsNullOrEmpty(uploadResponse.ErrorMessage))
                        {
                            errors.Add($"{fileItem.FileName}: {uploadResponse.ErrorMessage}");
                            continue;
                        }

                        if (string.IsNullOrEmpty(uploadResponse.UploadUrl))
                        {
                            errors.Add($"{fileItem.FileName}: No upload URL in response");
                            continue;
                        }

                        // Step 2: Read file bytes
                        byte[] fileBytes;
                        if (fileItem.StorageFile != null)
                        {
                            using var stream = await fileItem.StorageFile.OpenStreamForReadAsync();
                            using var ms = new MemoryStream();
                            await stream.CopyToAsync(ms);
                            fileBytes = ms.ToArray();
                        }
                        else
                        {
                            fileBytes = await File.ReadAllBytesAsync(fileItem.FilePath);
                        }

                        // Step 3: Upload to presigned URL
                        var uploadSuccess = await _apiService.UploadToPresignedUrlAsync(
                            uploadResponse.UploadUrl,
                            fileBytes,
                            fileItem.ContentType
                        );

                        if (!uploadSuccess)
                        {
                            errors.Add($"{fileItem.FileName}: Upload failed");
                            continue;
                        }

                        // Step 4: Confirm upload
                        if (!string.IsNullOrEmpty(uploadResponse.Key))
                        {
                            var confirmSuccess = await _apiService.ConfirmUploadAsync(
                                uploadResponse.FinalFileName ?? fileItem.FileName,
                                fileItem.ContentType,
                                fileItem.FileSize,
                                uploadResponse.Key,
                                _currentFolder
                            );

                            if (confirmSuccess)
                            {
                                completed++;
                            }
                            else
                            {
                                errors.Add($"{fileItem.FileName}: Confirmation failed");
                            }
                        }
                        else
                        {
                            // No key returned, but upload succeeded - count as success
                            completed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{fileItem.FileName}: {ex.Message}");
                    }

                    _overallProgress.Value = ((double)(completed + errors.Count) / total) * 100;
                }

                if (errors.Count > 0 && completed < total)
                {
                    args.Cancel = true;
                    _errorText.Text = $"Some files failed:\n{string.Join("\n", errors.Take(3))}";
                    _errorText.Visibility = Visibility.Visible;
                }
                else
                {
                    FilesUploaded = completed > 0;
                    // Dialog will close
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
                _isUploading = false;
                _uploadingPanel.Visibility = Visibility.Collapsed;
                _dropZone.IsHitTestVisible = true;
                _addFilesButton.IsEnabled = true;
                UpdateButtonState();
                deferral.Complete();
            }
        }
    }

    /// <summary>
    /// Represents a file selected for upload
    /// </summary>
    public class UploadFileItem
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = "application/octet-stream";
        public StorageFile? StorageFile { get; set; }

        public override string ToString()
        {
            return $"{FileName} ({FormatFileSize(FileSize)})";
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }
}
