using System.Text.Json;
using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Features.Chats.Tools;
using BookMyDoctor.Features.Doctors;
using BookMyDoctor.Persistence;
using Xunit;

namespace BookMyDoctor.Tests;

public sealed class BookAppointmentToolTests
{
    // Monday 19 Oct 2026, 06:00 in Texas. Doctor General Practice works 9:00–19:00 in 15-minute slots.
    private static readonly DateTimeOffset Now = HospitalTime.At(new DateOnly(2026, 10, 19), new TimeOnly(6, 0));
    private const string GpAt10 = """{"doctor":"Doctor General Practice","date":"2026-10-19","time":"10:00","reason":"Headache"}""";

    private readonly AppointmentService _appointments;
    private readonly BookAppointmentTool _tool;
    private readonly GetAvailableSlotsTool _slots;

    public BookAppointmentToolTests()
    {
        var time = new FixedTime(Now);
        var repo = new AppointmentsRepo();
        var availability = new AvailabilityService(
            new DoctorsRepo(), new DoctorSchedulesRepo(DoctorSchedulesRepo.SeedSchedules, []), repo, time);
        var doctors = new DoctorService(new DoctorsRepo());
        var drafts = new BookingDrafts();
        _appointments = new AppointmentService(repo, availability, time);
        _tool = new BookAppointmentTool(doctors, availability, _appointments, drafts, time);
        _slots = new GetAvailableSlotsTool(doctors, availability, drafts, time);
    }

    [Fact]
    public async Task PickingATimeFromShownSlots_NeedsOnlyTimeAndReason_ThenYesNeedsNothing()
    {
        // Turn 1: patient asks; the model looks up times (the server remembers doctor + day).
        await _slots.RunAsync(Args("""{"doctor":"dermatologist","date":"tuesday"}"""), Turn(SeedIds.HariPatientId), CancellationToken.None);

        // Turn 2: "12:20 please" -> the model passes just the time and reason.
        var proposal = await Run("""{"time":"12:20","reason":"itchy rash on arm"}""", Turn(SeedIds.HariPatientId));
        Assert.StartsWith("NOT BOOKED YET", proposal);
        Assert.Contains("Doctor Dermatology, Tuesday 20 October 2026 (2026-10-20), 12:20–12:40, reason: itchy rash on arm", proposal);

        // Turn 3: "yes" -> the model calls with no arguments.
        var booked = await Run("{}", Turn(SeedIds.HariPatientId));
        Assert.StartsWith("Booked. Reference BMD-", booked);
        var appointment = Assert.Single(_appointments.GetForPatient(SeedIds.HariPatientId));
        Assert.Equal("itchy rash on arm", appointment.Reason);
    }

    [Fact]
    public async Task ConfirmingWithNothingPending_DoesNotBook()
    {
        var result = await Run("{}", Turn(SeedIds.HariPatientId));

        Assert.StartsWith("Not booked: which doctor?", result);
        Assert.Empty(_appointments.GetForPatient(SeedIds.HariPatientId));
    }

    [Fact]
    public async Task FirstCall_OnlyProposes()
    {
        var result = await Run(GpAt10, Turn(SeedIds.HariPatientId));

        Assert.StartsWith("NOT BOOKED YET", result);
        Assert.Contains("Doctor General Practice, Monday 19 October 2026 (2026-10-19), 10:00–10:15, reason: Headache", result);
        Assert.Empty(_appointments.GetForPatient(SeedIds.HariPatientId));
    }

    [Fact]
    public async Task RepeatingTheCallInTheSameTurn_DoesNotBook()
    {
        // The model can't skip confirmation by calling twice before the patient replies.
        var turn = Turn(SeedIds.HariPatientId);
        await Run(GpAt10, turn);

        var result = await Run(GpAt10, turn);

        Assert.StartsWith("NOT BOOKED YET", result);
        Assert.Empty(_appointments.GetForPatient(SeedIds.HariPatientId));
    }

    [Fact]
    public async Task SameDetailsAfterThePatientReplies_Books()
    {
        await Run(GpAt10, Turn(SeedIds.HariPatientId));

        var result = await Run(GpAt10, Turn(SeedIds.HariPatientId));

        Assert.StartsWith("Booked. Reference BMD-", result);
        var appointment = Assert.Single(_appointments.GetForPatient(SeedIds.HariPatientId));
        Assert.Equal(HospitalTime.At(new DateOnly(2026, 10, 19), new TimeOnly(10, 0)), appointment.StartsAt);
        Assert.Contains(BookingReference.For(appointment.Id), result);
    }

    [Fact]
    public async Task DifferentTimeAfterThePatientReplies_ProposesAgainInstead()
    {
        await Run(GpAt10, Turn(SeedIds.HariPatientId));

        var result = await Run(GpAt10.Replace("10:00", "2:15 PM"), Turn(SeedIds.HariPatientId));

        Assert.StartsWith("NOT BOOKED YET", result);
        Assert.Contains("14:15–14:30", result);
        Assert.Empty(_appointments.GetForPatient(SeedIds.HariPatientId));
    }

    [Fact]
    public async Task MeaninglessReason_IsRejected()
    {
        var result = await Run(GpAt10.Replace("Headache", "it"), Turn(SeedIds.HariPatientId));

        Assert.StartsWith("Not booked: ask the patient what the visit is for", result);
    }

    [Fact]
    public async Task UnavailableTime_IsNotProposed()
    {
        var result = await Run(GpAt10.Replace("10:00", "10:07"), Turn(SeedIds.HariPatientId));

        Assert.StartsWith("Not booked: Doctor General Practice is not free at 10:07", result);
    }

    [Fact]
    public async Task AnotherPatientCannotConfirmSomeoneElsesProposal()
    {
        await Run(GpAt10, Turn(SeedIds.HariPatientId));

        var result = await Run(GpAt10, Turn(SeedIds.MadanPatientId));

        Assert.StartsWith("NOT BOOKED YET", result);
        Assert.Empty(_appointments.GetForPatient(SeedIds.MadanPatientId));
        Assert.Empty(_appointments.GetForPatient(SeedIds.HariPatientId));
    }

    // Each call to Turn is a new patient message.
    private static ToolContext Turn(Guid patientId) => new(patientId, Guid.NewGuid());

    private Task<string> Run(string argumentsJson, ToolContext context) =>
        _tool.RunAsync(Args(argumentsJson), context, CancellationToken.None);

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }
}
