using System.Security.Claims;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Exceptions;
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

    /// <summary>GET /api/patients/{id}/medications — list all medications for an assigned patient.</summary>
    [HttpGet("{id:guid}/medications")]
    public async Task<ActionResult<IEnumerable<MedicationDto>>> GetMedications(Guid id)
    {
        var medications = await _service.GetPatientMedicationsAsync(DoctorEmail, id);
        return Ok(medications);
    }

    /// <summary>GET /api/patients/{id}/medications/interactions — interactions for ACTIVE meds.</summary>
    [HttpGet("{id:guid}/medications/interactions")]
    public async Task<ActionResult<IEnumerable<MedicationInteractionDto>>> GetMedicationInteractions(Guid id)
    {
        var interactions = await _service.GetPatientMedicationInteractionsAsync(DoctorEmail, id);
        return Ok(interactions);
    }

    /// <summary>GET /api/patients/{id}/medical-history — structured OCR history entries.</summary>
    [HttpGet("{id:guid}/medical-history")]
    public async Task<ActionResult<IEnumerable<MedicalHistoryEntryDto>>> GetMedicalHistory(
        Guid id,
        [FromQuery] int limit = 50)
    {
        var history = await _service.GetPatientMedicalHistoryAsync(DoctorEmail, id, limit);
        return Ok(history);
    }

    /// <summary>POST /api/patients/{id}/medications — prescribe a medication for an assigned patient.</summary>
    [HttpPost("{id:guid}/medications")]
    public async Task<ActionResult<MedicationDto>> AddMedication(Guid id, [FromBody] AddMedicationDto dto)
    {
        try
        {
            var result = await _service.AddPatientMedicationAsync(DoctorEmail, id, dto);
            if (result is null) return NotFound(new { error = "Patient not found or not assigned to this doctor." });
            return Ok(result);
        }
        catch (MedicationInteractionBlockedException ex)
        {
            return Conflict(new
            {
                error = ex.Message,
                code = "HIGH_RISK_INTERACTION_BLOCKED"
            });
        }
    }

    /// <summary>DELETE /api/patients/{id}/medications/{medId} — soft-delete a medication.</summary>
    [HttpDelete("{id:guid}/medications/{medId:guid}")]
    public async Task<IActionResult> DeleteMedication(Guid id, Guid medId)
    {
        var removed = await _service.DeletePatientMedicationAsync(DoctorEmail, id, medId);
        if (!removed) return NotFound();
        return NoContent();
    }

    /// <summary>PATCH /api/patients/{id}/medications/{medId}/discontinue — end a medication with reason.</summary>
    [HttpPatch("{id:guid}/medications/{medId:guid}/discontinue")]
    public async Task<IActionResult> DiscontinueMedication(Guid id, Guid medId, [FromBody] DiscontinueMedicationRequest? request)
    {
        var reason = request?.Reason?.Trim() ?? "";
        if (string.IsNullOrEmpty(reason))
            return BadRequest(new { error = "Reason is required." });
        var ok = await _service.DiscontinuePatientMedicationAsync(DoctorEmail, id, medId, reason);
        if (!ok) return NotFound();
        return NoContent();
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
