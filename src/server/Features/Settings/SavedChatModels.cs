using MyHospitalAI.Features.Common;
using MyHospitalAI.Persistence;

namespace MyHospitalAI.Features.Settings;

// A chat model the admin saved to switch to later. "Use" on the Settings page copies BaseUrl and Model into the
// "Chat API address" and "Chat model" settings; the key for an online service still comes from AI_API_KEY in .env.
public sealed record SavedChatModel(Guid Id, string Name, string BaseUrl, string Model, string Notes);

public sealed record SaveChatModelRequest(string Name, string BaseUrl, string Model, string? Notes);

public sealed class SavedChatModelsService
{
    private readonly IChatModelsRepo _repo;

    public SavedChatModelsService(IChatModelsRepo repo)
    {
        _repo = repo;
    }

    public IReadOnlyList<SavedChatModel> GetAll() => _repo.GetAll();

    public SavedChatModel Add(SaveChatModelRequest request)
    {
        var model = Validated(Guid.NewGuid(), request);
        _repo.Add(model);
        return model;
    }

    public SavedChatModel Update(Guid id, SaveChatModelRequest request)
    {
        var model = Validated(id, request);
        return _repo.Update(model) ? model : throw new KeyNotFoundException("Chat model not found.");
    }

    public void Delete(Guid id)
    {
        if (!_repo.Delete(id))
        {
            throw new KeyNotFoundException("Chat model not found.");
        }
    }

    private SavedChatModel Validated(Guid id, SaveChatModelRequest request)
    {
        var name = request.Name.Trim();
        var baseUrl = request.BaseUrl.Trim().TrimEnd('/');
        var model = request.Model.Trim();
        var notes = request.Notes?.Trim() ?? "";

        if (name.Length is 0 or > 100)
        {
            throw new ArgumentException("Name is required and can be up to 100 characters.");
        }
        if (!(Uri.TryCreate(baseUrl, UriKind.Absolute, out var url) && url.Scheme is "http" or "https") || baseUrl.Length > 200)
        {
            throw new ArgumentException("Chat API address must be a web address that starts with http:// or https://.");
        }
        if (model.Length is 0 or > 200)
        {
            throw new ArgumentException("Model is required and can be up to 200 characters.");
        }
        if (notes.Length > 500)
        {
            throw new ArgumentException("Notes can be up to 500 characters.");
        }
        if (_repo.NameExists(name, id))
        {
            throw new ConflictException($"A chat model named \"{name}\" is already saved.");
        }
        return new SavedChatModel(id, name, baseUrl, model, notes);
    }
}
