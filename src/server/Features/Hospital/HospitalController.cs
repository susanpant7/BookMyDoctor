using BookMyDoctor.Features.Auth;
using BookMyDoctor.Features.Knowledge;
using BookMyDoctor.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookMyDoctor.Features.Hospital;

[ApiController]
[Route("api/hospital")]
public sealed class HospitalController : ControllerBase
{
    private readonly IHospitalRepo _hospitalRepo;
    private readonly IKnowledgeService _knowledgeService;
    private readonly ILogger<HospitalController> _logger;

    public HospitalController(IHospitalRepo hospitalRepo, IKnowledgeService knowledgeService, ILogger<HospitalController> logger)
    {
        _hospitalRepo = hospitalRepo;
        _knowledgeService = knowledgeService;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<HospitalDetails> Get() => Ok(Load());

    [HttpPut]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<HospitalDetails>> Update(HospitalDetails details, CancellationToken cancellationToken)
    {
        var info = details.Info;
        if (string.IsNullOrWhiteSpace(info.Name) || info.Name.Length > 100)
        {
            return BadRequest(new { message = "The hospital name is required and can be up to 100 characters." });
        }
        if (details.Hours.GroupBy(hours => hours.Day).Any(day => day.Count() > 1) || details.Hours.Any(hours => hours.Closes <= hours.Opens))
        {
            return BadRequest(new { message = "Each day can appear once, and must close after it opens." });
        }
        if (details.ServicePrices.Any(price => string.IsNullOrWhiteSpace(price.Name) || price.Price < 0))
        {
            return BadRequest(new { message = "Every test needs a name, and prices cannot be negative." });
        }

        _hospitalRepo.Save(details with
        {
            Info = info with { Facilities = [.. info.Facilities.Select(facility => facility.Trim()).Where(facility => facility.Length > 0)] }
        });
        await SyncKnowledgeAsync(cancellationToken);
        return Ok(Load());
    }

    // Replaces the hospital details with the starting ones (contact details, opening hours, test prices).
    [HttpPost("restore-defaults")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<HospitalDetails>> RestoreDefaults(CancellationToken cancellationToken)
    {
        _hospitalRepo.Save(DefaultData.Hospital);
        await SyncKnowledgeAsync(cancellationToken);
        return Ok(Load());
    }

    // The settings are saved either way. If Ollama is down, hospital-settings.md catches up via "Sync settings" on the
    // Knowledge page or at the next start.
    private async Task SyncKnowledgeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _knowledgeService.SyncHospitalSettingsAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("hospital-settings.md was not updated ({Error}). Is Ollama running?", ex.Message);
        }
    }

    private HospitalDetails Load() => new(_hospitalRepo.GetInfo(), _hospitalRepo.GetHours(), _hospitalRepo.GetServicePrices());
}
