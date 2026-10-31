using BookMyDoctor.Features.Auth;
using BookMyDoctor.Features.Common;
using BookMyDoctor.Features.Knowledge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookMyDoctor.Features.Doctors;

// Signed-in users only; changing doctors additionally needs the Admin role.
[ApiController]
[Authorize]
[Route("api/doctors")]
public sealed class DoctorsController : ControllerBase
{
    private readonly IDoctorService _doctorService;
    private readonly IKnowledgeService _knowledgeService;
    private readonly ILogger<DoctorsController> _logger;

    public DoctorsController(IDoctorService doctorService, IKnowledgeService knowledgeService, ILogger<DoctorsController> logger)
    {
        _doctorService = doctorService;
        _knowledgeService = knowledgeService;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<Doctor>> GetAll([FromQuery] Guid? specialtyId) =>
        Ok(_doctorService.GetAll(specialtyId));

    [HttpGet("{id:guid}")]
    public ActionResult<Doctor> GetById(Guid id)
    {
        var doctor = _doctorService.GetById(id);
        return doctor is null ? NotFound() : Ok(doctor);
    }

    [HttpGet("{id:guid}/schedule")]
    public ActionResult<IReadOnlyList<ScheduleBlock>> GetSchedule(Guid id) => Ok(_doctorService.GetSchedule(id));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<Doctor>> Create(DoctorRequest request, CancellationToken cancellationToken)
    {
        var doctor = _doctorService.Create(request);
        await SyncKnowledgeAsync(cancellationToken);
        return Ok(doctor);
    }

    [HttpPost("load-defaults")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<LoadDefaultsResult>> LoadDefaults(CancellationToken cancellationToken)
    {
        var result = _doctorService.LoadDefaults();
        await SyncKnowledgeAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<Doctor>> Update(Guid id, DoctorRequest request, CancellationToken cancellationToken)
    {
        var doctor = _doctorService.Update(id, request);
        await SyncKnowledgeAsync(cancellationToken);
        return Ok(doctor);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _doctorService.Delete(id);
        await SyncKnowledgeAsync(cancellationToken);
        return NoContent();
    }

    // Updates the doctor's section in doctor-settings.md, and specialty-settings.md, which lists each specialty's doctors.
    // The doctor is saved either way. If Ollama is down, the files catch up via "Sync settings" on the Knowledge page
    // or at the next start.
    private async Task SyncKnowledgeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _knowledgeService.SyncDoctorSettingsAsync(cancellationToken);
            await _knowledgeService.SyncSpecialtySettingsAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("doctor-settings.md and specialty-settings.md were not updated ({Error}). Is Ollama running?", ex.Message);
        }
    }
}
