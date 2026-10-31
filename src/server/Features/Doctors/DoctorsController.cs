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
        await SyncProfilesAsync(cancellationToken);
        return Ok(doctor);
    }

    [HttpPost("load-defaults")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<LoadDefaultsResult>> LoadDefaults(CancellationToken cancellationToken)
    {
        var result = _doctorService.LoadDefaults();
        await SyncProfilesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<Doctor>> Update(Guid id, DoctorRequest request, CancellationToken cancellationToken)
    {
        var doctor = _doctorService.Update(id, request);
        await SyncProfilesAsync(cancellationToken);
        return Ok(doctor);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _doctorService.Delete(id);
        await SyncProfilesAsync(cancellationToken);
        return NoContent();
    }

    // The doctor is saved either way. If Ollama is down, the profile is picked up by "Sync doctor profiles" on the
    // Knowledge page or at the next start.
    private async Task SyncProfilesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _knowledgeService.SyncDoctorProfilesAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Doctor profiles were not re-indexed ({Error}). Is Ollama running?", ex.Message);
        }
    }
}
