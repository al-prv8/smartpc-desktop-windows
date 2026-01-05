using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SensePC.Desktop.WinUI.Views
{
    public sealed partial class TutorialsPage : Page
    {
        private string _searchQuery = "";
        private string _selectedCategory = "All";
        private string _selectedDifficulty = "All";
        
        private readonly ObservableCollection<Tutorial> _filteredTutorials = new();
        
        // Static tutorial data matching the website
        private readonly List<Tutorial> _tutorials = new()
        {
            new Tutorial
            {
                Id = 1,
                Title = "Introduce Sense PC",
                Duration = "0:56",
                Description = "Build a powerful cloud PC in minutes and access it from any browser, on any device, from anywhere—no hardware needed.",
                VideoUrl = "https://d2dlj0hxnln4ry.cloudfront.net/SENSEPC%201.mp4",
                ThumbnailUrl = "https://smartpc.cloud/assets/images/gettingStartedWithSensePc.png",
                Category = "Start here",
                Difficulty = "Beginner",
                UploadDate = new DateTime(2025, 12, 25)
            },
            new Tutorial
            {
                Id = 2,
                Title = "How Sense PC Works",
                Duration = "1:30",
                Description = "See how SensePC delivers fast performance with built-in security—plus user management, billing, support ticketing, and in-app tutorials after login.",
                VideoUrl = "https://d2dlj0hxnln4ry.cloudfront.net/SENSEPC%203.mp4",
                ThumbnailUrl = "https://smartpc.cloud/assets/images/optimizing.jpg",
                Category = "Start here",
                Difficulty = "Beginner",
                UploadDate = new DateTime(2025, 12, 25)
            },
            new Tutorial
            {
                Id = 3,
                Title = "Introduce Sense Cloud",
                Duration = "0:50",
                Description = "Securely store, organize, preview, and share files with smart cloud storage that stays synced across all your devices.",
                VideoUrl = "https://d2dlj0hxnln4ry.cloudfront.net/SENSEPC%202.mp4",
                ThumbnailUrl = "https://smartpc.cloud/assets/images/storageManagement.png",
                Category = "Start here",
                Difficulty = "Beginner",
                UploadDate = new DateTime(2025, 12, 25)
            }
        };

        public TutorialsPage()
        {
            this.InitializeComponent();
            TutorialsGridView.ItemsSource = _filteredTutorials;
            InitializeFilters();
            Loaded += (s, e) => ApplyFilters();
        }

        private void InitializeFilters()
        {
            // Populate category filter
            var categories = new List<string> { "All" };
            categories.AddRange(_tutorials.Select(t => t.Category).Distinct());
            CategoryFilter.ItemsSource = categories;
            CategoryFilter.SelectedIndex = 0;

            // Populate difficulty filter
            var difficulties = new List<string> { "All" };
            difficulties.AddRange(_tutorials.Select(t => t.Difficulty).Distinct());
            DifficultyFilter.ItemsSource = difficulties;
            DifficultyFilter.SelectedIndex = 0;
        }

        private void ApplyFilters()
        {
            var filtered = _tutorials.Where(t =>
            {
                var matchesSearch = string.IsNullOrEmpty(_searchQuery) ||
                    t.Title.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase);
                
                var matchesCategory = _selectedCategory == "All" || t.Category == _selectedCategory;
                var matchesDifficulty = _selectedDifficulty == "All" || t.Difficulty == _selectedDifficulty;

                return matchesSearch && matchesCategory && matchesDifficulty;
            }).ToList();

            _filteredTutorials.Clear();
            foreach (var tutorial in filtered)
            {
                _filteredTutorials.Add(tutorial);
            }

            EmptyState.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TutorialsGridView.Visibility = filtered.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void TutorialsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Tutorial tutorial)
            {
                await ShowVideoDialogAsync(tutorial);
            }
        }

        private async System.Threading.Tasks.Task ShowVideoDialogAsync(Tutorial tutorial)
        {
            // MediaPlayerElement with Uniform stretch to maintain aspect ratio
            var mediaPlayer = new MediaPlayerElement
            {
                Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(tutorial.VideoUrl)),
                AutoPlay = true,
                AreTransportControlsEnabled = true,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch
            };

            // Container that fills available space
            var videoContainer = new Border
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black),
                CornerRadius = new CornerRadius(8),
                Child = mediaPlayer,
                MinHeight = 400,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch
            };

            var content = new StackPanel 
            { 
                Spacing = 16, 
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch 
            };
            content.Children.Add(new TextBlock
            {
                Text = tutorial.Description,
                TextWrapping = TextWrapping.Wrap,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            content.Children.Add(videoContainer);

            var dialog = new ContentDialog
            {
                Title = tutorial.Title,
                Content = content,
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot,
                FullSizeDesired = true  // This makes the dialog expand to fit content
            };

            await dialog.ShowAsync();

            // Cleanup
            mediaPlayer.MediaPlayer?.Dispose();
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            _searchQuery = sender.Text;
            ApplyFilters();
        }

        private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryFilter.SelectedItem != null)
            {
                _selectedCategory = CategoryFilter.SelectedItem.ToString() ?? "All";
                ApplyFilters();
            }
        }

        private void DifficultyFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DifficultyFilter.SelectedItem != null)
            {
                _selectedDifficulty = DifficultyFilter.SelectedItem.ToString() ?? "All";
                ApplyFilters();
            }
        }
    }

    /// <summary>
    /// Tutorial data model
    /// </summary>
    public class Tutorial
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Duration { get; set; } = "";
        public string Description { get; set; } = "";
        public string VideoUrl { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public string Category { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public DateTime UploadDate { get; set; }
    }
}
