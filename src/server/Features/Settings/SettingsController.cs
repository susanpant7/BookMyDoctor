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

    public SettingsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<Setting>> GetAll() => Ok(_settingsService.GetAll());

    [HttpPut("{key}")]
    public ActionResult<Setting> Update(string key, UpdateSettingRequest request) =>
        Ok(_settingsService.Update(key, request.Value));
}
