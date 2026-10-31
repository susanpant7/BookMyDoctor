using BookMyDoctor.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookMyDoctor.Features.Settings;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    private readonly DataCleanup _dataCleanup;

    public SettingsController(ISettingsService settingsService, DataCleanup dataCleanup)
    {
        _settingsService = settingsService;
        _dataCleanup = dataCleanup;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<Setting>> GetAll() => Ok(_settingsService.GetAll());

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
