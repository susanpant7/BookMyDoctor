using System.Globalization;
using System.Text.Json;
using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Features.Doctors;

namespace BookMyDoctor.Features.Chats.Tools;

public static class ToolArguments
{
    public static JsonElement Schema(string json) => JsonDocument.Parse(json).RootElement.Clone();

    // Small models sometimes send "" or omit optional arguments, so treat both as "not given".
    public static string? OptionalString(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object &&
        arguments.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    public static string RequiredString(JsonElement arguments, string name) =>
        OptionalString(arguments, name) ?? throw new ArgumentException($"The '{name}' argument is required.");

    // Small models are unreliable at turning "Tuesday" into a date, so they may pass the word and the server works it out.
    public static DateOnly? OptionalDate(JsonElement arguments, string name, DateOnly today)
    {
        var text = OptionalString(arguments, name);
        return text is null ? null : ResolveDate(text, today);
    }

    public static DateOnly RequiredDate(JsonElement arguments, string name, DateOnly today) =>
        OptionalDate(arguments, name, today) ?? throw new ArgumentException($"The '{name}' argument is required.");

    // "today", "tomorrow", a weekday ("tuesday" = the next Tuesday from today, including today; "next tuesday" = after today),
    // or YYYY-MM-DD.
    internal static DateOnly ResolveDate(string text, DateOnly today)
    {
        var words = text.Trim().ToLowerInvariant();
        if (DateOnly.TryParseExact(words, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return exact;
        }

        if (words is "today")
        {
            return today;
        }

        if (words is "tomorrow")
        {
            return today.AddDays(1);
        }

        var strictlyAfterToday = words.StartsWith("next ");
        var dayName = (strictlyAfterToday ? words[5..] : words).Trim();
        var day = Enum.GetValues<DayOfWeek>().FirstOrDefault(
            candidate => candidate.ToString().StartsWith(dayName, StringComparison.OrdinalIgnoreCase) && dayName.Length >= 3,
            (DayOfWeek)(-1));
        if (day >= 0)
        {
            var daysAhead = ((int)day - (int)today.DayOfWeek + 7) % 7;
            return today.AddDays(daysAhead == 0 && strictlyAfterToday ? 7 : daysAhead);
        }

        throw new ArgumentException($"'{text}' is not a date I understand. Use today, tomorrow, a weekday like tuesday, or YYYY-MM-DD.");
    }

    public static TimeOnly RequiredTime(JsonElement arguments, string name) =>
        OptionalTime(arguments, name) ?? throw new ArgumentException($"The '{name}' argument is required.");

    // Accepts 24-hour times like "14:20" and also "2:20 PM", which models sometimes send.
    public static TimeOnly? OptionalTime(JsonElement arguments, string name)
    {
        var text = OptionalString(arguments, name);
        if (text is null)
        {
            return null;
        }

        string[] formats = ["HH:mm", "H:mm", "h:mm tt", "h:mmtt", "h tt", "htt"];
        return TimeOnly.TryParseExact(text.ToUpperInvariant(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : throw new ArgumentException($"'{text}' is not a valid time. Use 24-hour HH:mm, e.g. 14:20.");
    }
}

public static class ToolFormat
{
    public static string Date(DateOnly date) => $"{date:dddd d MMMM yyyy} ({date:yyyy-MM-dd})";

    public static string Time(DateTimeOffset instant) => $"{TimeZoneInfo.ConvertTime(instant, HospitalTime.Zone):HH:mm}";
}

public static class DoctorLookup
{
    // Finds a doctor from what the model sends: the exact name ("Doctor Chhaalarog Bisesagya"), part of it,
    // or the specialty in another form ("dermatologist", "pediatrician"). Returns an error the model can act on otherwise.
    public static Doctor Find(IDoctorService doctorService, string query)
    {
        var doctors = doctorService.GetAll();
        var text = query.Trim().ToLowerInvariant();

        var matches = doctors.Where(doctor => doctor.Name.Equals(query.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
        {
            matches = doctors.Where(doctor =>
                doctor.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                text.Contains(doctor.Name.ToLowerInvariant()) ||
                text.Contains(SpecialtyStem(doctor.Specialty))).ToList();
        }

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new ArgumentException(
                $"No doctor matches '{query}'. Doctors: {string.Join(", ", doctors.Select(doctor => $"{doctor.Name} ({doctor.Specialty})"))}."),
            _ => throw new ArgumentException(
                $"'{query}' matches several doctors: {string.Join(", ", matches.Select(doctor => doctor.Name))}. Ask the patient which one.")
        };
    }

    // "Dermatology" -> "dermat", which also appears in "dermatologist".
    private static string SpecialtyStem(string specialty) => specialty.ToLowerInvariant()[..Math.Min(6, specialty.Length)];
}
