using StartupConnect.ViewModels;

namespace StartupConnect.Services;

public interface IVideoService
{
    Task<VideosIndexViewModel> GetVideosAsync(string? category, string? search, string? sort);
    Task<VideoItemViewModel?> GetVideoByIdAsync(string id);
    Task<List<VideoItemViewModel>> GetRelatedVideosAsync(string id, int count = 4);
}
