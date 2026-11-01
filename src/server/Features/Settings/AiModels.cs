using MyHospitalAI.Features.Chats;
using MyHospitalAI.Features.Common;
using MyHospitalAI.Persistence;

namespace MyHospitalAI.Features.Settings;

// A chat or embedding model saved on the Settings page. "Use" copies BaseUrl, Model and ApiKeyName (and for an
// embedding model the prefixes) into the settings the assistant reads. ApiKeyName is the name of the .env variable
// with the service's key (empty for Ollama, or to use the key of the service at the same address).
public sealed record AiModel(
    Guid Id, string Kind, string Name, string BaseUrl, string Model, string ApiKeyName, string QueryPrefix, string DocumentPrefix,
    string Notes);

public sealed record SaveAiModelRequest(
    string Kind, string Name, string BaseUrl, string Model, string? ApiKeyName, string? QueryPrefix, string? DocumentPrefix,
    string? Notes);

// The variables the chat and embedding models in use really read, whether they are set, and which of the key names
// on the Settings page (services, saved models) are set in the .env file. The keys themselves are never sent.
public sealed record AiKeysInfo(
    string ChatKeyNameInUse, bool ChatKeyInUseSet, string EmbeddingKeyNameInUse, bool EmbeddingKeyInUseSet,
    IReadOnlyList<string> SetKeyNames);

public static class AiModelKinds
{
    public const string Chat = "chat";
    public const string Embedding = "embedding";
}

public sealed class AiModelsService
{
    private readonly IAiModelsRepo _repo;

    public AiModelsService(IAiModelsRepo repo)
    {
        _repo = repo;
    }

    public IReadOnlyList<AiModel> GetAll(string kind) => _repo.GetAll(CheckedKind(kind));

    public AiModel Add(SaveAiModelRequest request)
    {
        var model = Validated(Guid.NewGuid(), request);
        _repo.Add(model);
        return model;
    }

    public AiModel Update(Guid id, SaveAiModelRequest request)
    {
        var model = Validated(id, request);
        return _repo.Update(model) ? model : throw new KeyNotFoundException("Model not found.");
    }

    public void Delete(Guid id)
    {
        if (!_repo.Delete(id))
        {
            throw new KeyNotFoundException("Model not found.");
        }
    }

    // The services are listed on the Settings page from AiProviders, so the saved models are only the admin's own.
    // Deletes the saved copies of the services that an earlier version put in the list; others are kept.
    public void RemoveServiceCopies()
    {
        foreach (var kind in new[] { AiModelKinds.Chat, AiModelKinds.Embedding })
        {
            var services = AiModelSwitcher.Services(kind);
            foreach (var saved in _repo.GetAll(kind).Where(saved => services.Any(service =>
                service.Name == saved.Name && service.BaseUrl == saved.BaseUrl && service.Model == saved.Model
                && service.QueryPrefix == saved.QueryPrefix && service.DocumentPrefix == saved.DocumentPrefix)))
            {
                _repo.Delete(saved.Id);
            }
        }
    }

    public static string CheckedKind(string kind) =>
        kind is AiModelKinds.Chat or AiModelKinds.Embedding ? kind : throw new ArgumentException("Kind must be chat or embedding.");

    // Trimmed of a trailing slash. Throws ArgumentException when it is not an http(s) address.
    public static string CheckedBaseUrl(string? baseUrl)
    {
        baseUrl = baseUrl?.Trim().TrimEnd('/') ?? "";
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var url) && url.Scheme is "http" or "https" && baseUrl.Length <= 200
            ? baseUrl
            : throw new ArgumentException("API address must be a web address that starts with http:// or https://.");
    }

    private AiModel Validated(Guid id, SaveAiModelRequest request)
    {
        var kind = CheckedKind(request.Kind);
        if (_repo.GetById(id) is { } existing && existing.Kind != kind)
        {
            throw new ArgumentException("A model's kind can't be changed.");
        }

        var name = request.Name.Trim();
        var baseUrl = CheckedBaseUrl(request.BaseUrl);
        var model = request.Model.Trim();
        var notes = request.Notes?.Trim() ?? "";
        var apiKeyName = request.ApiKeyName?.Trim() ?? "";
        // Only embedding models use prefixes.
        var queryPrefix = kind == AiModelKinds.Embedding ? request.QueryPrefix?.Trim() ?? "" : "";
        var documentPrefix = kind == AiModelKinds.Embedding ? request.DocumentPrefix?.Trim() ?? "" : "";

        if (name.Length is 0 or > 100)
        {
            throw new ArgumentException("Name is required and can be up to 100 characters.");
        }
        if (model.Length is 0 or > 200)
        {
            throw new ArgumentException("Model is required and can be up to 200 characters.");
        }
        if (apiKeyName.Length > 0 && !AiProviders.IsValidKeyName(apiKeyName))
        {
            throw new ArgumentException("API key name must be empty or a variable name that ends in API_KEY, e.g. GEMINI_API_KEY.");
        }
        if (queryPrefix.Length > 100 || documentPrefix.Length > 100)
        {
            throw new ArgumentException("The prefixes can be up to 100 characters.");
        }
        if (notes.Length > 500)
        {
            throw new ArgumentException("Notes can be up to 500 characters.");
        }
        if (_repo.NameExists(kind, name, id))
        {
            throw new ConflictException($"A {kind} model named \"{name}\" already exists.");
        }
        return new AiModel(id, kind, name, baseUrl, model, apiKeyName, queryPrefix, documentPrefix, notes);
    }
}
