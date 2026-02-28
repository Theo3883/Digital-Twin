using System.Security.Claims;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTwin.WebAPI.Controllers;

/// <summary>
/// Dashboard summary endpoint for the Doctor Portal.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Doctor")]
public class DashboardController : ControllerBase
{
    private readonly IDoctorPortalApplicationService _service;

    public DashboardController(IDoctorPortalApplicationService service)
    {
        _service = service;
    }

    private string DoctorEmail =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? throw new UnauthorizedAccessException("Email claim missing.");

    /// <summary>GET /api/dashboard — high-level summary for the logged-in doctor.</summary>
    [HttpGet]
    public async Task<ActionResult<DoctorDashboardDto>> Get()
    {
        var dashboard = await _service.GetDashboardAsync(DoctorEmail);
        return Ok(dashboard);
    }
}
