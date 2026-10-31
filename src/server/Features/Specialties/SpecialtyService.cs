using BookMyDoctor.Features.Common;
using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Specialties;

public sealed class SpecialtyService : ISpecialtyService
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 500;

    private readonly ISpecialtiesRepo _specialtiesRepo;

    public SpecialtyService(ISpecialtiesRepo specialtiesRepo)
    {
        _specialtiesRepo = specialtiesRepo;
    }

    public IReadOnlyList<Specialty> GetAll() => _specialtiesRepo.GetAll();

    public Specialty? GetById(Guid id) => _specialtiesRepo.GetById(id);

    public Specialty Create(SpecialtyRequest request)
    {
        var (name, description) = Check(request, excludeId: null);
        var specialty = new Specialty(Guid.NewGuid(), name, description);
        _specialtiesRepo.Add(specialty);
        return specialty;
    }

    public Specialty Update(Guid id, SpecialtyRequest request)
    {
        var (name, description) = Check(request, excludeId: id);
        if (!_specialtiesRepo.Update(new Specialty(id, name, description)))
        {
            throw new KeyNotFoundException("Specialty not found.");
        }
        return _specialtiesRepo.GetById(id)!;
    }

    public void Delete(Guid id)
    {
        var specialty = _specialtiesRepo.GetById(id) ?? throw new KeyNotFoundException("Specialty not found.");
        if (specialty.DoctorCount > 0)
        {
            var doctors = specialty.DoctorCount == 1 ? "1 doctor" : $"{specialty.DoctorCount} doctors";
            throw new ConflictException($"This specialty has {doctors}, so it cannot be deleted. Move or delete its doctors first.");
        }
        _specialtiesRepo.Delete(id);
    }

    public LoadDefaultsResult LoadDefaults()
    {
        var added = 0;
        foreach (var specialty in DefaultData.Specialties)
        {
            if (_specialtiesRepo.GetByName(specialty.Name) is not null)
            {
                continue;
            }
            _specialtiesRepo.Add(specialty);
            added++;
        }
        return new LoadDefaultsResult(added, DefaultData.Specialties.Count - added);
    }

    private (string Name, string Description) Check(SpecialtyRequest request, Guid? excludeId)
    {
        var name = request.Name?.Trim() ?? "";
        var description = request.Description?.Trim() ?? "";
        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            throw new ArgumentException($"The name is required and can be up to {MaxNameLength} characters.");
        }
        if (description.Length > MaxDescriptionLength)
        {
            throw new ArgumentException($"The description can be up to {MaxDescriptionLength} characters.");
        }
        if (_specialtiesRepo.GetByName(name) is { } existing && existing.Id != excludeId)
        {
            throw new ConflictException("A specialty with this name already exists.");
        }
        return (name, description);
    }
}
