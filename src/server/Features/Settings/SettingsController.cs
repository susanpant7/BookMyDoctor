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
    private readonly AiModelsService _models;
    private readonly AiModelSwitcher _switcher;

    public SettingsController(
        ISettingsService settingsService, DataCleanup dataCleanup, IConfiguration configuration, AiModelsService models,
        AiModelSwitcher switcher)
    {
        _settingsService = settingsService;
        _dataCleanup = dataCleanup;
        _configuration = configuration;
        _models = models;
        _switcher = switcher;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<Setting>> GetAll() => Ok(_settingsService.GetAll());

    // Which key variables the models in use read and whether they are set, and which of the key names on the
    // Settings page are set in the .env file. Only names are sent, never a key.
    [HttpGet("ai-keys")]
    public ActionResult<AiKeysInfo> GetAiKeys()
    {
        var settings = _settingsService.Get();
        var chatKey = AiProviders.KeyNameInUse(_configuration, false, settings.ChatApiKeyName, settings.ChatBaseUrl);
        var embeddingKey = AiProviders.KeyNameInUse(_configuration, true, settings.EmbeddingApiKeyName, settings.EmbeddingBaseUrl);
        var names = AiProviders.ChatModels.Concat(AiProviders.EmbeddingModels).Select(service => service.ApiKeyName)
            .Concat(_models.GetAll(AiModelKinds.Chat).Concat(_models.GetAll(AiModelKinds.Embedding)).Select(model => model.ApiKeyName))
            .Append(AiProviders.ApiKeyName)
            .Append(AiProviders.EmbeddingApiKeyName)
            .Where(name => AiProviders.ReadKey(_configuration, name) is not null)
            .Distinct()
            .Order()
            .ToList();
        return Ok(new AiKeysInfo(chatKey, HasKey(chatKey), embeddingKey, HasKey(embeddingKey), names));
    }

    private bool HasKey(string name) => AiProviders.ReadKey(_configuration, name) is not null;

    // The chat or embedding models to switch to on the Settings page (kind is "chat" or "embedding").
    [HttpGet("models")]
    public ActionResult<IReadOnlyList<AiModel>> GetModels([FromQuery] string kind) => Ok(_models.GetAll(kind));

    [HttpPost("models")]
    public ActionResult<AiModel> AddModel(SaveAiModelRequest request) => Ok(_models.Add(request));

    [HttpPut("models/{id:guid}")]
    public ActionResult<AiModel> UpdateModel(Guid id, SaveAiModelRequest request) => Ok(_models.Update(id, request));

    [HttpDelete("models/{id:guid}")]
    public IActionResult DeleteModel(Guid id)
    {
        _models.Delete(id);
        return NoContent();
    }

    // The known services of a kind, shown below the saved models.
    [HttpGet("models/services")]
    public ActionResult<IReadOnlyList<AiModelDefault>> GetServices([FromQuery] string kind) => Ok(AiModelSwitcher.Services(kind));

    // The models the service at a known address offers for our key.
    [HttpGet("models/available")]
    public Task<IActionResult> GetAvailable(
        [FromQuery] string kind, [FromQuery] string baseUrl, [FromQuery] string? keyName, CancellationToken cancellationToken) =>
        FromService(async () => Ok(await _switcher.ListAsync(kind, baseUrl, keyName, cancellationToken)));

    // Sends one tiny request to a model, and saves nothing: how long it took to answer, when it works.
    [HttpPost("models/test")]
    public Task<IActionResult> TestModel(ModelChoice choice, CancellationToken cancellationToken) =>
        FromService(async () => Ok(await _switcher.TestAsync(choice, cancellationToken)));

    // Switches the chat model after it answered a test, or the embedding model after all the knowledge was embedded
    // with it. When that fails, nothing is changed.
    [HttpPost("models/use")]
    public Task<IActionResult> UseModel(ModelChoice choice, CancellationToken cancellationToken) =>
        FromService(async () => Ok(await _switcher.UseAsync(choice, cancellationToken)));

    // A failure of the service (a wrong key, an unknown model, a busy service) is a 502 with its reason.
    private async Task<IActionResult> FromService(Func<Task<IActionResult>> call)
    {
        try
        {
            return await call();
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    [HttpPut("{key}")]
    public ActionResult<Setting> Update(string key, UpdateSettingRequest request) =>
        Ok(_settingsService.Update(key, request.Value));

    // The default settings. The admin's saved models are kept.
    [HttpPost("restore-defaults")]
    public ActionResult<IReadOnlyList<Setting>> RestoreDefaults()
    {
        _models.RemoveServiceCopies();
        return Ok(_settingsService.RestoreDefaults());
    }

    // Deletes all data and starts again from the defaults a first start creates.
    [HttpPost("clean-up")]
    public async Task<IActionResult> CleanUp(CancellationToken cancellationToken)
    {
        await _dataCleanup.CleanUpAsync(cancellationToken);
        return NoContent();
    }
}
