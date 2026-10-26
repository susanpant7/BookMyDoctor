using System.Text.Json;
using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Features.Chats.Tools;
using BookMyDoctor.Features.Doctors;
using BookMyDoctor.Features.Specialties;
using BookMyDoctor.Persistence;
using Xunit;

namespace BookMyDoctor.Tests;

public sealed class ReadToolsTests
{
    // Monday 19 Oct 2026, 06:00 in Texas.
    private static readonly DateTimeOffset Now = HospitalTime.At(new DateOnly(2026, 10, 19), new TimeOnly(6, 0));
    private static readonly ToolContext Hari = new(SeedIds.HariPatientId);

    private readonly FixedTime _time = new(Now);
    private readonly DoctorService _doctors = new(new DoctorsRepo());
    private readonly AppointmentsRepo _appointmentsRepo = new();
    private readonly AvailabilityService _availability;

    public ReadToolsTests()
    {
        _availability = new AvailabilityService(
            new DoctorsRepo(), new DoctorSchedulesRepo(DoctorSchedulesRepo.SeedSchedules, []), _appointmentsRepo, _time);
    }

    [Fact]
    public async Task Slots_ListsTimesForDoctorNamedBySpecialty()
    {
        var result = await Slots("""{"doctor":"general practitioner","date":"2026-10-19"}""");

        Assert.StartsWith("Doctor General Practice has free times on Monday 19 October 2026 (2026-10-19): 09:00, 09:15", result);
        Assert.Contains("18:45 (each 15 minutes)", result);
    }

    [Fact]
    public async Task Slots_SuggestsNextFreeDayWhenDoctorDoesNotWork()
    {
        // Doctor Dermatology works Tuesday, Thursday and Saturday.
        var result = await Slots("""{"doctor":"dermatologist","date":"2026-10-19"}""");

        Assert.Contains("no free times on Monday 19 October 2026", result);
        Assert.Contains("next free day is Tuesday 20 October 2026 (2026-10-20): 10:00, 10:20", result);
    }

    [Fact]
    public async Task Slots_FiltersByTimeOfDay()
    {
        var result = await Slots("""{"doctor":"Doctor General Practice","date":"2026-10-19","time_of_day":"afternoon"}""");

        Assert.Contains("free times in the afternoon on Monday 19 October 2026 (2026-10-19): 12:00, 12:15", result);
        Assert.Contains("16:45 (each 15 minutes)", result);
        Assert.DoesNotContain("11:45", result);
        Assert.DoesNotContain("17:00", result);
    }

    [Fact]
    public async Task Slots_RejectsPastDatesAndUnknownDoctors()
    {
        Assert.Contains("is in the past", await Slots("""{"doctor":"Doctor Cardiology","date":"2026-10-01"}"""));

        var error = await Assert.ThrowsAsync<ArgumentException>(() => Slots("""{"doctor":"Doctor House"}"""));
        Assert.Contains("Doctor Dermatology (Dermatology)", error.Message);
    }

    [Fact]
    public async Task MyAppointments_OnlyShowsTheSignedInPatientsBookings()
    {
        var booking = new AppointmentService(_appointmentsRepo, _availability, _time);
        booking.Book(SeedIds.HariPatientId, new BookAppointmentRequest(SeedIds.GeneralPracticeDoctorId, At(10, 0), "Headache"));
        booking.Book(SeedIds.MadanPatientId, new BookAppointmentRequest(SeedIds.GeneralPracticeDoctorId, At(11, 0), "Back pain"));
        var tool = new GetMyAppointmentsTool(booking, _doctors, _time);

        var result = await tool.RunAsync(Args("{}"), Hari, CancellationToken.None);

        Assert.Contains("Monday 19 October 2026 (2026-10-19) 10:00–10:15 | Doctor General Practice | Headache | Scheduled", result);
        Assert.DoesNotContain("Back pain", result);
    }

    [Fact]
    public async Task HospitalInfo_IncludesHoursInsuranceAndPrices()
    {
        var tool = new GetHospitalInfoTool(new HospitalRepo(), new InsurancePlansRepo());

        var result = await tool.RunAsync(Args("{}"), Hari, CancellationToken.None);

        Assert.Contains("1200 Riverside Medical Plaza", result);
        Assert.Contains("closed Sunday", result);
        Assert.Contains("Medicaid (Children only)", result);
        Assert.Contains("ECG (electrocardiogram) $45", result);
    }

    [Fact]
    public async Task Specialties_ListsEverySpecialty()
    {
        var tool = new GetSpecialtiesTool(new SpecialtyService(new SpecialtiesRepo()));

        var result = await tool.RunAsync(Args("{}"), Hari, CancellationToken.None);

        Assert.Equal(5, result.Split('\n').Length);
        Assert.Contains("- Dermatology: Skin, hair, and nail care", result);
    }

    private Task<string> Slots(string argumentsJson) =>
        new GetAvailableSlotsTool(_doctors, _availability, new BookingDrafts(), _time).RunAsync(Args(argumentsJson), Hari, CancellationToken.None);

    private static DateTimeOffset At(int hour, int minute) => HospitalTime.At(new DateOnly(2026, 10, 19), new TimeOnly(hour, minute));

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }
}
