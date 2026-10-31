using BookMyDoctor.Features.Hospital;

namespace BookMyDoctor.Persistence;

public interface IHospitalRepo
{
    // False until the details are saved (on a fresh start, or after cleaning up the data).
    bool HasInfo();

    HospitalInfo GetInfo();
    IReadOnlyList<HospitalHours> GetHours();
    IReadOnlyList<ServicePrice> GetServicePrices();

    // Replaces the contact details, opening hours and extra-test prices in one transaction.
    void Save(HospitalDetails details);

    // Adds days the whole hospital is closed, skipping dates that already have one.
    void AddHolidays(IEnumerable<(DateOnly Date, string Name)> holidays);
}
