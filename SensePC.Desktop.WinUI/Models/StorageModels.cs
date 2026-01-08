using System.Text.Json.Serialization;

namespace SensePC.Desktop.WinUI.Models
{
    /// <summary>
    /// Represents a file or folder in Sense Cloud storage
    /// </summary>
    public class StorageItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = "";

        [JsonPropertyName("fileType")]
        public string FileType { get; set; } = "";

        [JsonPropertyName("createdAt")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("size")]
        public string? Size { get; set; }

        [JsonPropertyName("starred")]
        public bool Starred { get; set; }

        [JsonPropertyName("shared")]
        public bool Shared { get; set; }

        [JsonPropertyName("sharedWith")]
        public string? SharedWith { get; set; }

        [JsonPropertyName("previewUrl")]
        public string? PreviewUrl { get; set; }

        // Helper properties for UI
        public bool IsFolder => FileType?.ToLower() == "folder";
        public string DisplaySize => Size ?? "—";
        public string DisplayDate => string.IsNullOrEmpty(CreatedAt) ? "—" : FormatDate(CreatedAt);

        private static string FormatDate(string dateStr)
        {
            if (DateTime.TryParse(dateStr, out var date))
            {
                return date.ToString("MMM dd, yyyy");
            }
            return dateStr;
        }

        // Get icon for file type
        public string FileTypeIcon
        {
            get
            {
                if (IsFolder) return "\uE8B7"; // Folder icon
                return FileType?.ToLower() switch
                {
                    "image" or "png" or "jpg" or "jpeg" or "gif" or "webp" => "\uE8B9", // Image
                    "video" or "mp4" or "mov" or "avi" => "\uE8B2", // Video
                    "audio" or "mp3" or "wav" or "ogg" => "\uE8D6", // Audio
                    "document" or "pdf" or "doc" or "docx" or "txt" => "\uE8A5", // Document
                    _ => "\uE7C3" // Generic file
                };
            }
        }
    }

    /// <summary>
    /// Storage usage statistics
    /// </summary>
    public class StorageUsage
    {
        [JsonPropertyName("totalBytes")]
        public long TotalBytes { get; set; }

        [JsonPropertyName("usedBytes")]
        public long UsedBytes { get; set; }

        // Helper properties for UI
        public long MaxBytes => 1024L * 1024L * 1024L * 1024L; // 1 TB
        
        public double UsagePercentage => MaxBytes > 0 ? (double)TotalBytes / MaxBytes * 100 : 0;
        
        public string UsedFormatted => FormatBytes(TotalBytes);
        
        public string MaxFormatted => FormatBytes(MaxBytes);

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// Storage list API response with pagination
    /// </summary>
    public class StorageListResponse
    {
        [JsonPropertyName("files")]
        public List<StorageItem> Files { get; set; } = new();

        [JsonPropertyName("pagination")]
        public StoragePagination? Pagination { get; set; }
    }

    /// <summary>
    /// Pagination info for storage list
    /// </summary>
    public class StoragePagination
    {
        [JsonPropertyName("page")]
        public int Page { get; set; } = 1;

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 10;

        [JsonPropertyName("pages")]
        public int TotalPages { get; set; } = 1;

        [JsonPropertyName("total")]
        public int TotalItems { get; set; } = 0;
    }

    /// <summary>
    /// Upload URL response from API
    /// </summary>
    public class UploadUrlResponse
    {
        [JsonPropertyName("uploadUrl")]
        public string UploadUrl { get; set; } = "";

        [JsonPropertyName("finalFileName")]
        public string? FinalFileName { get; set; }

        [JsonPropertyName("key")]
        public string? Key { get; set; }
        
        // For error handling
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Download URL response from API
    /// </summary>
    public class DownloadUrlResponse
    {
        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; } = "";
    }

    /// <summary>
    /// Storage category for sidebar navigation
    /// </summary>
    public class StorageCategory
    {
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public string? FilterType { get; set; }
        public bool IsSpecial { get; set; } // For starred, shared, duplicates

        public static List<StorageCategory> GetCategories()
        {
            return new List<StorageCategory>
            {
                new() { Name = "All Files", Icon = "\uE896", FilterType = null },
                new() { Name = "Folders", Icon = "\uE8B7", FilterType = "folder" },
                new() { Name = "Documents", Icon = "\uE8A5", FilterType = "document" },
                new() { Name = "Images", Icon = "\uE8B9", FilterType = "image" },
                new() { Name = "Videos", Icon = "\uE8B2", FilterType = "video" },
                new() { Name = "Audio", Icon = "\uE8D6", FilterType = "audio" },
                new() { Name = "Recent", Icon = "\uE823", FilterType = "recent" },
                new() { Name = "Starred", Icon = "\uE734", FilterType = null, IsSpecial = true },
                new() { Name = "Shared", Icon = "\uE72D", FilterType = null, IsSpecial = true },
                new() { Name = "Duplicates", Icon = "\uE8C8", FilterType = "duplicates", IsSpecial = true },
            };
        }
    }

    /// <summary>
    /// Request parameters for listing files
    /// </summary>
    public class StorageListRequest
    {
        public string? Type { get; set; }
        public string? Search { get; set; }
        public bool? Starred { get; set; }
        public bool? Shared { get; set; }
        public string? Modified { get; set; }
        public string? Folder { get; set; }
        public string SortBy { get; set; } = "date";
        public string SortOrder { get; set; } = "desc";
        public int Limit { get; set; } = 10;
        public int Page { get; set; } = 1;
    }

    /// <summary>
    /// Share link API response
    /// </summary>
    public class ShareLinkResponse
    {
        [JsonPropertyName("shareUrl")]
        public string ShareUrl { get; set; } = "";

        [JsonPropertyName("expiresAt")]
        public string? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Shares query response
    /// </summary>
    public class SharesResponse
    {
        [JsonPropertyName("items")]
        public List<ShareInfo>? Items { get; set; }
    }

    /// <summary>
    /// Individual share info
    /// </summary>
    public class ShareInfo
    {
        [JsonPropertyName("shareId")]
        public string ShareId { get; set; } = "";
    }
}
