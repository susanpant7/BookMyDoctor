using BookMyDoctor.Features.Hospital;

namespace BookMyDoctor.Persistence;

public interface IInsurancePlansRepo
{
    IReadOnlyList<InsurancePlan> GetAll();
}
