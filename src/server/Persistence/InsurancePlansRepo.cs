using BookMyDoctor.Features.Hospital;
using Microsoft.EntityFrameworkCore;

namespace BookMyDoctor.Persistence;

public sealed class InsurancePlansRepo : IInsurancePlansRepo
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public InsurancePlansRepo(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public IReadOnlyList<InsurancePlan> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.InsurancePlans
            .AsNoTracking()
            .OrderBy(plan => plan.SortOrder)
            .ToList()
            .Select(plan => new InsurancePlan(plan.Id, plan.Name, plan.Restrictions))
            .ToList();
    }
}
