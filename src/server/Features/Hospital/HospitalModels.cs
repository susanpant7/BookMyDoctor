namespace BookMyDoctor.Features.Hospital;

public sealed record HospitalInfo(
    string Name,
    string Address,
    string Phone,
    IReadOnlyList<string> Facilities,
    string Parking,
    string PublicTransport);

// Outpatient opening hours for one weekday; days without a row are closed.
public sealed record HospitalHours(
    DayOfWeek Day,
    TimeOnly Opens,
    TimeOnly Closes);

// Extra tests and procedures charged on top of the consultation fee.
public sealed record ServicePrice(
    Guid Id,
    string Name,
    decimal Price);

// Everything the Settings page edits in one go. Rows with an empty Id are new.
public sealed record HospitalDetails(
    HospitalInfo Info,
    IReadOnlyList<HospitalHours> Hours,
    IReadOnlyList<ServicePrice> ServicePrices);
