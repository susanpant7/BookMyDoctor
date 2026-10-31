using BookMyDoctor.Features.Common;

namespace BookMyDoctor.Features.Specialties;

public interface ISpecialtyService
{
    IReadOnlyList<Specialty> GetAll();
    Specialty? GetById(Guid id);

    // These throw ArgumentException for invalid input, KeyNotFoundException for an unknown id and
    // ConflictException when the name is taken or (for Delete) the specialty still has doctors.
    Specialty Create(SpecialtyRequest request);
    Specialty Update(Guid id, SpecialtyRequest request);
    void Delete(Guid id);

    // Adds the default specialties that do not exist yet (by name).
    LoadDefaultsResult LoadDefaults();
}
