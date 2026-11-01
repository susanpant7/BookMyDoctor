using MyHospitalAI.Features.Settings;
using MyHospitalAI.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyHospitalAI.Persistence;

public interface IAiModelsRepo
{
    IReadOnlyList<AiModel> GetAll(string kind);

    AiModel? GetById(Guid id);

    // True when another model of this kind (not `exceptId`) already has this name.
    bool NameExists(string kind, string name, Guid? exceptId = null);

    void Add(AiModel model);

    // False when no model has the id.
    bool Update(AiModel model);

    bool Delete(Guid id);

    // Deletes every model and saves these instead.
    void ReplaceAll(IReadOnlyList<AiModel> models);
}

public sealed class AiModelsRepo : IAiModelsRepo
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public AiModelsRepo(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public IReadOnlyList<AiModel> GetAll(string kind)
    {
        using var db = _factory.CreateDbContext();
        return db.AiModels
            .AsNoTracking()
            .Where(model => model.Kind == kind)
            .OrderBy(model => model.SortOrder)
            .ThenBy(model => model.Name)
            .Select(model => new AiModel(model.Id, model.Kind, model.Name, model.BaseUrl, model.Model, model.ApiKeyName, model.QueryPrefix, model.DocumentPrefix, model.Notes))
            .ToList();
    }

    public AiModel? GetById(Guid id)
    {
        using var db = _factory.CreateDbContext();
        return db.AiModels
            .AsNoTracking()
            .Where(model => model.Id == id)
            .Select(model => new AiModel(model.Id, model.Kind, model.Name, model.BaseUrl, model.Model, model.ApiKeyName, model.QueryPrefix, model.DocumentPrefix, model.Notes))
            .FirstOrDefault();
    }

    public bool NameExists(string kind, string name, Guid? exceptId = null)
    {
        using var db = _factory.CreateDbContext();
        return db.AiModels.Any(model => model.Kind == kind && model.Name == name && model.Id != exceptId);
    }

    public void Add(AiModel model)
    {
        using var db = _factory.CreateDbContext();
        var last = db.AiModels.Where(entity => entity.Kind == model.Kind).Max(entity => (int?)entity.SortOrder) ?? -1;
        db.AiModels.Add(ToEntity(model, last + 1));
        db.SaveChanges();
    }

    public bool Update(AiModel model)
    {
        using var db = _factory.CreateDbContext();
        return db.AiModels.Where(entity => entity.Id == model.Id).ExecuteUpdate(setters => setters
            .SetProperty(entity => entity.Name, model.Name)
            .SetProperty(entity => entity.BaseUrl, model.BaseUrl)
            .SetProperty(entity => entity.Model, model.Model)
            .SetProperty(entity => entity.ApiKeyName, model.ApiKeyName)
            .SetProperty(entity => entity.QueryPrefix, model.QueryPrefix)
            .SetProperty(entity => entity.DocumentPrefix, model.DocumentPrefix)
            .SetProperty(entity => entity.Notes, model.Notes)) > 0;
    }

    public bool Delete(Guid id)
    {
        using var db = _factory.CreateDbContext();
        return db.AiModels.Where(model => model.Id == id).ExecuteDelete() > 0;
    }

    public void ReplaceAll(IReadOnlyList<AiModel> models)
    {
        using var db = _factory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        db.AiModels.ExecuteDelete();
        db.AiModels.AddRange(models.Select((model, i) => ToEntity(model, i)));
        db.SaveChanges();
        transaction.Commit();
    }

    private static AiModelEntity ToEntity(AiModel model, int sortOrder) => new()
    {
        Id = model.Id, Kind = model.Kind, Name = model.Name, BaseUrl = model.BaseUrl, Model = model.Model, ApiKeyName = model.ApiKeyName,
        QueryPrefix = model.QueryPrefix, DocumentPrefix = model.DocumentPrefix, Notes = model.Notes, SortOrder = sortOrder
    };
}
