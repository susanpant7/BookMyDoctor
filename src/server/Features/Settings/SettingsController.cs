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
    private readonly SavedChatModelsService _chatModels;

    public SettingsController(
        ISettingsService settingsService, DataCleanup dataCleanup, IConfiguration configuration, SavedChatModelsService chatModels)
    {
        _settingsService = settingsService;
        _dataCleanup = dataCleanup;
        _configuration = configuration;
        _chatModels = chatModels;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<Setting>> GetAll() => Ok(_settingsService.GetAll());

    // Notes on the services that can run the chat and embedding models, and whether the API keys are set.
    [HttpGet("ai-providers")]
    public ActionResult<AiProvidersInfo> GetAiProviders() =>
        Ok(new AiProvidersInfo(
            AiProviders.ApiKeyName, HasKey(AiProviders.ApiKeyName), AiProviders.All,
            AiProviders.EmbeddingApiKeyName, HasKey(AiProviders.EmbeddingApiKeyName), AiProviders.Embedding));

    private bool HasKey(string name) => !string.IsNullOrWhiteSpace(_configuration[name]);

    // Chat models the admin saved to switch to later.
    [HttpGet("chat-models")]
    public ActionResult<IReadOnlyList<SavedChatModel>> GetChatModels() => Ok(_chatModels.GetAll());

    [HttpPost("chat-models")]
    public ActionResult<SavedChatModel> AddChatModel(SaveChatModelRequest request) => Ok(_chatModels.Add(request));

    [HttpPut("chat-models/{id:guid}")]
    public ActionResult<SavedChatModel> UpdateChatModel(Guid id, SaveChatModelRequest request) => Ok(_chatModels.Update(id, request));

    [HttpDelete("chat-models/{id:guid}")]
    public IActionResult DeleteChatModel(Guid id)
    {
        _chatModels.Delete(id);
        return NoContent();
    }

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
