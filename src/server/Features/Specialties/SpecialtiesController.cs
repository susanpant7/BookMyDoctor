using BookMyDoctor.Features.Auth;
using BookMyDoctor.Features.Common;
using BookMyDoctor.Features.Knowledge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookMyDoctor.Features.Specialties;

[ApiController]
[Route("api/specialties")]
public sealed class SpecialtiesController : ControllerBase
{
    private readonly ISpecialtyService _specialtyService;
    private readonly IKnowledgeService _knowledgeService;
    private readonly ILogger<SpecialtiesController> _logger;

    public SpecialtiesController(ISpecialtyService specialtyService, IKnowledgeService knowledgeService, ILogger<SpecialtiesController> logger)
    {
        _specialtyService = specialtyService;
        _knowledgeService = knowledgeService;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<Specialty>> GetAll() => Ok(_specialtyService.GetAll());

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<Specialty>> Create(SpecialtyRequest request, CancellationToken cancellationToken)
    {
        var specialty = _specialtyService.Create(request);
        await SyncKnowledgeAsync(includeDoctors: false, cancellationToken);
        return Ok(specialty);
    }

    [HttpPost("load-defaults")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<LoadDefaultsResult>> LoadDefaults(CancellationToken cancellationToken)
    {
        var result = _specialtyService.LoadDefaults();
        await SyncKnowledgeAsync(includeDoctors: false, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<Specialty>> Update(Guid id, SpecialtyRequest request, CancellationToken cancellationToken)
    {
        var specialty = _specialtyService.Update(id, request);
        // Each doctor's section in doctor-settings.md names their specialty, so a rename has to reach it too.
        await SyncKnowledgeAsync(includeDoctors: true, cancellationToken);
        return Ok(specialty);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _specialtyService.Delete(id);
        await SyncKnowledgeAsync(includeDoctors: false, cancellationToken);
        return NoContent();
    }

    // Keeps specialty-settings.md (and doctor-settings.md when asked) matching the specialties. The change is saved
    // either way; if Ollama is down, "Sync settings" on the Knowledge page catches up.
    private async Task SyncKnowledgeAsync(bool includeDoctors, CancellationToken cancellationToken)
    {
        try
        {
            await _knowledgeService.SyncSpecialtySettingsAsync(cancellationToken);
            if (includeDoctors)
            {
                await _knowledgeService.SyncDoctorSettingsAsync(cancellationToken);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("The specialty knowledge was not updated ({Error}). Is Ollama running?", ex.Message);
        }
    }
}
