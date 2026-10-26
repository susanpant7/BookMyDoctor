using BookMyDoctor.Features.Hospital;

namespace BookMyDoctor.Persistence;

public sealed class InsurancePlansRepo : IInsurancePlansRepo
{
    private static readonly IReadOnlyList<InsurancePlan> Seed =
    [
        new(Guid.Parse("60000001-0000-0000-0000-000000000000"), "Blue Cross Blue Shield", null),
        new(Guid.Parse("60000002-0000-0000-0000-000000000000"), "Aetna", null),
        new(Guid.Parse("60000003-0000-0000-0000-000000000000"), "Cigna", null),
        new(Guid.Parse("60000004-0000-0000-0000-000000000000"), "UnitedHealthcare", null),
        new(Guid.Parse("60000005-0000-0000-0000-000000000000"), "Humana", null),
        new(Guid.Parse("60000006-0000-0000-0000-000000000000"), "Medicare", null),
        new(Guid.Parse("60000007-0000-0000-0000-000000000000"), "Medicaid", "Children only")
    ];

    public IReadOnlyList<InsurancePlan> GetAll() => Seed;
}
