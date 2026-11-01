using MyHospitalAI.Features.Auth;
using MyHospitalAI.Features.Chats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyHospitalAI.Features.Settings;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    private readonly DataCleanup _dataCleanup;
    private readonly IConfiguration _configuration;

    public SettingsController(ISettingsService settingsService, DataCleanup dataCleanup, IConfiguration configuration)
    {
        _settingsService = settingsService;
        _dataCleanup = dataCleanup;
        _configuration = configuration;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<Setting>> GetAll() => Ok(_settingsService.GetAll());

    // The providers the "AI provider" setting can choose, and which of them have a key.
    [HttpGet("ai-providers")]
    public ActionResult<IReadOnlyList<AiProviderInfo>> GetAiProviders() =>
        Ok(AiProviders.All
            .Select(provider => new AiProviderInfo(
                provider.Id, provider.Name, provider.ApiKeyName,
                provider.IsLocal || !string.IsNullOrWhiteSpace(_configuration[provider.ApiKeyName!]),
                provider.DefaultModel, provider.FreeTier, provider.SignUpUrl))
            .ToList());

    [HttpPut("{key}")]
    public ActionResult<Setting> Update(string key, UpdateSettingRequest request) =>
        Ok(_settingsService.Update(key, request.Value));

    [HttpPost("restore-defaults")]
    public ActionResult<IReadOnlyList<Setting>> RestoreDefaults() => Ok(_settingsService.RestoreDefaults());

    // Deletes all data and starts again from the defaults a first start creates.
    [HttpPost("clean-up")]
    public async Task<IActionResult> CleanUp(CancellationToken cancellationToken)
    {
        await _dataCleanup.CleanUpAsync(cancellationToken);
        return NoContent();
    }
}
