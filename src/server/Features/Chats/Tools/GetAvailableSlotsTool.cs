using System.Text.Json;
using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Features.Doctors;

namespace BookMyDoctor.Features.Chats.Tools;

public sealed class GetAvailableSlotsTool : IAssistantTool
{
    // When the requested day is full, look this far ahead for the next free day.
    private const int SearchAheadDays = 14;

    private readonly IDoctorService _doctorService;
    private readonly IAvailabilityService _availabilityService;
    private readonly BookingDrafts _drafts;
    private readonly TimeProvider _time;

    public GetAvailableSlotsTool(
        IDoctorService doctorService, IAvailabilityService availabilityService, BookingDrafts drafts, TimeProvider time)
    {
        _doctorService = doctorService;
        _availabilityService = availabilityService;
        _drafts = drafts;
        _time = time;
    }

    public string Name => "get_available_slots";

    public string Description =>
        "Free appointment times for one doctor on a given date. If that day is full or the doctor doesn't work then, " +
        "it returns the next day with free times. Use when the patient asks when a doctor is available or wants to book.";

    public JsonElement Parameters { get; } = ToolArguments.Schema("""
        {
          "type": "object",
          "properties": {
            "doctor": { "type": "string", "description": "The doctor's name or specialty, e.g. Doctor Dermatology or Dermatology" },
            "date": { "type": "string", "description": "The day, as a word like today, tomorrow or tuesday (copy what the patient said), or as YYYY-MM-DD. Leave empty for today." },
            "time_of_day": { "type": "string", "enum": ["morning", "afternoon", "evening"], "description": "Optional: only times before 12:00 (morning), 12:00–17:00 (afternoon) or from 17:00 (evening)" }
          },
          "required": ["doctor"]
        }
        """);

    public Task<string> RunAsync(JsonElement arguments, ToolContext context, CancellationToken cancellationToken)
    {
        var doctor = DoctorLookup.Find(_doctorService, ToolArguments.RequiredString(arguments, "doctor"));
        var today = HospitalTime.Today(_time);
        var date = ToolArguments.OptionalDate(arguments, "date", today) ?? today;
        if (date < today)
        {
            return Task.FromResult($"{ToolFormat.Date(date)} is in the past. Today is {ToolFormat.Date(today)}.");
        }

        var timeOfDay = ToolArguments.OptionalString(arguments, "time_of_day")?.ToLowerInvariant();
        var inPeriod = PeriodFilter(timeOfDay);
        var period = timeOfDay is null ? "" : $" in the {timeOfDay}";

        var slots = _availabilityService.GetAvailableSlots(doctor.Id, date, 1).Where(inPeriod).ToList();
        if (slots.Count > 0)
        {
            Remember(context, doctor, date, slots);
            return Task.FromResult($"{doctor.Name} has free times{period} on {ToolFormat.Date(date)}: {Times(slots)}.");
        }

        var later = _availabilityService.GetAvailableSlots(doctor.Id, date.AddDays(1), SearchAheadDays).Where(inPeriod).ToList();
        if (later.Count == 0)
        {
            return Task.FromResult($"{doctor.Name} has no free times{period} on {ToolFormat.Date(date)} or in the {SearchAheadDays} days after.");
        }

        var nextDate = HospitalTime.DateOf(later[0].StartsAt);
        var nextDaySlots = later.Where(slot => HospitalTime.DateOf(slot.StartsAt) == nextDate).ToList();
        Remember(context, doctor, nextDate, nextDaySlots);
        return Task.FromResult(
            $"{doctor.Name} has no free times{period} on {ToolFormat.Date(date)}. " +
            $"The next free day is {ToolFormat.Date(nextDate)}: {Times(nextDaySlots)}.");
    }

    // Lets book_appointment fill in the doctor and day when the patient just picks one of these times,
    // and lets the chat show them as buttons.
    private void Remember(ToolContext context, Doctor doctor, DateOnly date, IReadOnlyList<TimeSlot> slots)
    {
        var offer = new SlotOffer(doctor.Id, doctor.Name, date, slots);
        context.SlotsShown = offer;                      // returned with the reply, so the chat shows the buttons
        _drafts.SetOffer(context.PatientId, offer);      // kept, so "11:30 please" knows which doctor and day
    }

    private static Func<TimeSlot, bool> PeriodFilter(string? timeOfDay)
    {
        (int From, int To) hours = timeOfDay switch
        {
            null => (0, 24),
            "morning" => (0, 12),
            "afternoon" => (12, 17),
            "evening" => (17, 24),
            _ => throw new ArgumentException($"'{timeOfDay}' is not a valid time_of_day. Use morning, afternoon or evening.")
        };
        return slot =>
        {
            var hour = TimeZoneInfo.ConvertTime(slot.StartsAt, HospitalTime.Zone).Hour;
            return hour >= hours.From && hour < hours.To;
        };
    }

    private static string Times(IReadOnlyList<TimeSlot> slots) =>
        $"{string.Join(", ", slots.Select(slot => ToolFormat.Time(slot.StartsAt)))} " +
        $"(each {(slots[0].EndsAt - slots[0].StartsAt).TotalMinutes:0} minutes)";
}
