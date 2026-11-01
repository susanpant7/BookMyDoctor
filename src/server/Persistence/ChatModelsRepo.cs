using MyHospitalAI.Features.Settings;
using MyHospitalAI.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyHospitalAI.Persistence;

public interface IChatModelsRepo
{
    IReadOnlyList<SavedChatModel> GetAll();

    // True when another saved chat model (not `exceptId`) already has this name.
    bool NameExists(string name, Guid? exceptId = null);

    void Add(SavedChatModel model);

    // False when no saved chat model has the id.
    bool Update(SavedChatModel model);

    bool Delete(Guid id);
}

public sealed class ChatModelsRepo : IChatModelsRepo
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ChatModelsRepo(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public IReadOnlyList<SavedChatModel> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.ChatModels
            .AsNoTracking()
            .OrderBy(model => model.Name)
            .Select(model => new SavedChatModel(model.Id, model.Name, model.BaseUrl, model.Model, model.Notes))
            .ToList();
    }

    public bool NameExists(string name, Guid? exceptId = null)
    {
        using var db = _factory.CreateDbContext();
        return db.ChatModels.Any(model => model.Name == name && model.Id != exceptId);
    }

    public void Add(SavedChatModel model)
    {
        using var db = _factory.CreateDbContext();
        db.ChatModels.Add(new ChatModelEntity { Id = model.Id, Name = model.Name, BaseUrl = model.BaseUrl, Model = model.Model, Notes = model.Notes });
        db.SaveChanges();
    }

    public bool Update(SavedChatModel model)
    {
        using var db = _factory.CreateDbContext();
        return db.ChatModels.Where(entity => entity.Id == model.Id).ExecuteUpdate(setters => setters
            .SetProperty(entity => entity.Name, model.Name)
            .SetProperty(entity => entity.BaseUrl, model.BaseUrl)
            .SetProperty(entity => entity.Model, model.Model)
            .SetProperty(entity => entity.Notes, model.Notes)) > 0;
    }

    public bool Delete(Guid id)
    {
        using var db = _factory.CreateDbContext();
        return db.ChatModels.Where(model => model.Id == id).ExecuteDelete() > 0;
    }
}
