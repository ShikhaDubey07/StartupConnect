namespace StartupConnect.ViewModels;

public class VideoItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string Speaker { get; set; } = string.Empty;
    public string SpeakerRole { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string YouTubeId { get; set; } = string.Empty;
    public string YouTubeUrl => $"https://www.youtube.com/watch?v={YouTubeId}";
    public string Views { get; set; } = string.Empty;
    public string Level { get; set; } = "Beginner"; // Beginner, Intermediate, Masterclass
    public bool IsFeatured { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> KeyTakeaways { get; set; } = new();
    public List<VideoItemViewModel> RelatedVideos { get; set; } = new();
    public string PublishedDate { get; set; } = string.Empty;
}

public class VideosIndexViewModel
{
    public List<VideoItemViewModel> Videos { get; set; } = new();
    public VideoItemViewModel? FeaturedVideo { get; set; }
    public string? CurrentCategory { get; set; }
    public string? SearchQuery { get; set; }
    public string? SortBy { get; set; }
    public Dictionary<string, int> CategoryCounts { get; set; } = new();
    public int TotalVideos { get; set; }
}
