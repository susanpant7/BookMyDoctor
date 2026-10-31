using BookMyDoctor.Features.Hospital;

namespace BookMyDoctor.Persistence;

public interface IHospitalRepo
{
    HospitalInfo GetInfo();
    IReadOnlyList<HospitalHours> GetHours();
    IReadOnlyList<ServicePrice> GetServicePrices();

    // Replaces the contact details, opening hours and extra-test prices in one transaction.
    void Save(HospitalDetails details);
}
