using BookMyDoctor.Features.Hospital;

namespace BookMyDoctor.Persistence;

public interface IHospitalRepo
{
    HospitalInfo GetInfo();
    IReadOnlyList<HospitalHours> GetHours();
    IReadOnlyList<ServicePrice> GetServicePrices();
}
