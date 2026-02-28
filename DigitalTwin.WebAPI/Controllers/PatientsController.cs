using System.Security.Claims;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTwin.WebAPI.Controllers;

/// <summary>
/// Doctor-scoped patient endpoints. All queries are filtered through
/// <see cref="IDoctorPortalApplicationService"/> which enforces assignment checks.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Doctor")]
public class PatientsController : ControllerBase
{
    private readonly IDoctorPortalApplicationService _service;

    public PatientsController(IDoctorPortalApplicationService service)
    {
        _service = service;
    }

    private string DoctorEmail =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? throw new UnauthorizedAccessException("Email claim missing.");

    /// <summary>GET /api/patients — list the doctor's assigned patients.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DoctorPatientSummaryDto>>> GetMyPatients()
    {
        var patients = await _service.GetMyPatientsAsync(DoctorEmail);
        return Ok(patients);
    }

    /// <summary>GET /api/patients/{id} — single patient detail (only if assigned).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DoctorPatientDetailDto>> GetById(Guid id)
    {
        var detail = await _service.GetPatientDetailAsync(DoctorEmail, id);
        if (detail is null) return NotFound();
        return Ok(detail);
    }

    /// <summary>GET /api/patients/{id}/vitals?from=&amp;to=&amp;type=</summary>
    [HttpGet("{id:guid}/vitals")]
    public async Task<ActionResult<IEnumerable<VitalSignDto>>> GetVitals(
        Guid id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? type)
    {
        var vitals = await _service.GetPatientVitalsAsync(DoctorEmail, id, type, from, to);
        return Ok(vitals);
    }

    /// <summary>GET /api/patients/{id}/sleep?from=&amp;to=</summary>
    [HttpGet("{id:guid}/sleep")]
    public async Task<ActionResult<IEnumerable<SleepSessionDto>>> GetSleep(
        Guid id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var sessions = await _service.GetPatientSleepAsync(DoctorEmail, id, from, to);
        return Ok(sessions);
    }

    /// <summary>POST /api/patients/assign — assign a patient by email.</summary>
    [HttpPost("assign")]
    public async Task<ActionResult<DoctorPatientSummaryDto>> Assign([FromBody] AssignPatientDto dto)
    {
        var result = await _service.AssignPatientAsync(DoctorEmail, dto);
        if (result is null)
            return BadRequest(new { error = "Patient not found or already assigned." });
        return Ok(result);
    }

    /// <summary>DELETE /api/patients/{id}/unassign — remove assignment.</summary>
    [HttpDelete("{id:guid}/unassign")]
    public async Task<IActionResult> Unassign(Guid id)
    {
        var removed = await _service.UnassignPatientAsync(DoctorEmail, id);
        if (!removed) return NotFound();
        return NoContent();
    }
}
