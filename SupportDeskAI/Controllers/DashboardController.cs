using Microsoft.AspNetCore.Mvc;
using SupportDeskAI.DTOs;
using SupportDeskAI.Interfaces;

namespace SupportDeskAI.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    // FR-08: GET /api/dashboard/summary
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummary(CancellationToken ct)
    {
        return Ok(await _dashboardService.GetSummaryAsync(ct));
    }
}
