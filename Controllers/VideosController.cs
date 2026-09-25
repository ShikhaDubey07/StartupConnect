using Microsoft.AspNetCore.Mvc;
using StartupConnect.Services;

namespace StartupConnect.Controllers;

public class VideosController : Controller
{
    private readonly IVideoService _videoService;

    public VideosController(IVideoService videoService)
    {
        _videoService = videoService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? category, string? search, string? sort)
    {
        var model = await _videoService.GetVideosAsync(category, search, sort);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> GetVideo(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "Video ID is required" });

        var video = await _videoService.GetVideoByIdAsync(id);
        if (video == null)
            return NotFound(new { message = "Video not found" });

        return Json(video);
    }
}
