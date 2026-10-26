using BookMyDoctor.Features.Hospital;
using static BookMyDoctor.Persistence.SeedDates;

namespace BookMyDoctor.Persistence;

public sealed class HospitalRepo : IHospitalRepo
{
    private static readonly HospitalInfo SeedInfo = new(
        "BookMyDoctor Hospital",
        "1200 Riverside Medical Plaza, Austin, TX 78741",
        "(512) 555-0140",
        ["ECG room", "X-ray room", "Blood test lab", "Children's play area", "Breastfeeding room"],
        "Free parking in the Riverside Medical Plaza garage for up to 2 hours.",
        "5-minute walk from the Riverside bus stop (routes 7 and 20).");

    private static readonly IReadOnlyList<HospitalHours> SeedHours =
    [
        .. Weekdays.Append(DayOfWeek.Saturday).Select(day => new HospitalHours(day, new TimeOnly(8, 0), new TimeOnly(19, 0)))
    ];

    private static readonly IReadOnlyList<ServicePrice> SeedServicePrices =
    [
        new(Guid.Parse("50000001-0000-0000-0000-000000000000"), "ECG (electrocardiogram)", 45),
        new(Guid.Parse("50000002-0000-0000-0000-000000000000"), "X-ray", 60),
        new(Guid.Parse("50000003-0000-0000-0000-000000000000"), "Basic blood panel", 35),
        new(Guid.Parse("50000004-0000-0000-0000-000000000000"), "Lipid profile (cholesterol)", 40),
        new(Guid.Parse("50000005-0000-0000-0000-000000000000"), "Blood sugar (HbA1c) test", 30),
        new(Guid.Parse("50000006-0000-0000-0000-000000000000"), "Allergy skin test", 120),
        new(Guid.Parse("50000007-0000-0000-0000-000000000000"), "Wart or skin tag removal", 75),
        new(Guid.Parse("50000008-0000-0000-0000-000000000000"), "Adult flu shot (September to March)", 25)
    ];

    public HospitalInfo GetInfo() => SeedInfo;

    public IReadOnlyList<HospitalHours> GetHours() => SeedHours;

    public IReadOnlyList<ServicePrice> GetServicePrices() => SeedServicePrices;
}
