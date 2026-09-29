using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StartupConnect.Services;
using StartupConnect.ViewModels;

namespace StartupConnect.Controllers;

[Authorize]
public class InvestorController : Controller
{
    private readonly IInvestorDashboardService _dashboard;

    public InvestorController(IInvestorDashboardService dashboard) => _dashboard = dashboard;

    /// <summary>Ranked deal flow for investors (with filters), their investment interests and saved ideas.</summary>
    [HttpGet]
    public async Task<IActionResult> Dashboard([FromQuery] InvestorDashboardFilter filter)
    {
        if (!ModelState.IsValid) filter = new InvestorDashboardFilter();
        if (filter.MinFund.HasValue && filter.MaxFund.HasValue && filter.MinFund > filter.MaxFund)
            (filter.MinFund, filter.MaxFund) = (filter.MaxFund, filter.MinFund);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var model = await _dashboard.BuildAsync(userId, filter);
        return View(model);
    }
}
