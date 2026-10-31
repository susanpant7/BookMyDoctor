using System.Globalization;
using System.Text.RegularExpressions;
using BookMyDoctor.Features.Chats.Tools;
using BookMyDoctor.Features.Doctors;
using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Chats;

// Checks a reply before the patient sees it, for the most harmful kinds of made-up facts (doctors who don't work
// here, prices that don't exist, appointment times nobody looked up) and for replies that talk about the
// assistant's internal tools or only promise to look something up instead of doing it.
public sealed partial class ReplyFactChecker
{
    private readonly IDoctorService _doctorService;
    private readonly IHospitalRepo _hospitalRepo;
    private readonly IReadOnlyList<string> _toolNames;

    public ReplyFactChecker(IDoctorService doctorService, IHospitalRepo hospitalRepo, IEnumerable<IAssistantTool> tools)
    {
        _doctorService = doctorService;
        _hospitalRepo = hospitalRepo;
        _toolNames = tools.Select(tool => tool.Name).ToList();
    }

    // Start of the problem raised when a reply claims a booking that didn't happen; the assistant looks for it.
    public const string FalseBookingProblem = "The reply says the appointment is booked, but it is NOT booked.";

    // Returns one sentence per problem found; empty when the reply looks fine.
    // `seenTexts` is everything the model was given: tool results, retrieved knowledge and the conversation.
    // A clock time in the reply must appear in one of them, otherwise the model made it up.
    // `turnToolResults` are the results of the tools called while answering this message.
    public IReadOnlyList<string> FindProblems(string reply, IEnumerable<string> seenTexts, IEnumerable<string> turnToolResults)
    {
        var seen = seenTexts.ToList();
        var turnResults = turnToolResults.ToList();
        var problems = FindNameAndPriceProblems(reply).ToList();
        problems.AddRange(FindBookingProblems(reply, seen, turnResults));

        // Patients must never see internal names like get_available_slots, or talk of functions and tools.
        var mentionedTools = _toolNames.Where(name => reply.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (mentionedTools.Count > 0 || ToolTalk().IsMatch(reply))
        {
            problems.Add(
                "The reply talks about your internal tools" +
                (mentionedTools.Count > 0 ? $" ({string.Join(", ", mentionedTools)})" : "") +
                ". Never mention tools or functions to the patient.");
        }

        // "I'll check the free times…" as a final reply means nothing was looked up.
        if (PromiseToLookUp().Match(reply) is { Success: true } promise)
        {
            problems.Add($"The reply says \"{promise.Value}\" but doesn't do it. Call the tool now and answer with the result.");
        }

        // When the reply names a day, its times must come from something about that same day; Monday's free times
        // say nothing about Wednesday.
        var replyDays = WeekdaysIn(reply);
        var seenTimes = (replyDays.Count == 0 ? seen : seen.Where(text => WeekdaysIn(text).Overlaps(replyDays)))
            .SelectMany(TimesIn)
            .ToHashSet();
        foreach (var (text, time) in ClockTimes(reply))
        {
            if (seenTimes.Contains(time))
            {
                continue;
            }

            problems.Add(replyDays.Count == 0
                ? $"\"{text}\" is not a time you looked up. Call get_available_slots before offering or mentioning appointment times."
                : $"\"{text}\" was not looked up for {string.Join(" or ", replyDays)}. Call get_available_slots for that day before offering or mentioning its times.");
        }

        return problems.Distinct().ToList();
    }

    // A reply may only say an appointment is booked when book_appointment booked it in this turn, or when it talks
    // about a booking that already exists. References must be real ones (BMD-…).
    private static IEnumerable<string> FindBookingProblems(string reply, IReadOnlyList<string> seen, IReadOnlyList<string> turnResults)
    {
        var bookedThisTurn = turnResults.Any(result => result.StartsWith("Booked. Reference BMD-", StringComparison.Ordinal));
        var notBookedThisTurn = turnResults.Any(result =>
            result.StartsWith("NOT BOOKED YET", StringComparison.Ordinal) || result.StartsWith("Not booked:", StringComparison.Ordinal));
        var knownReferences = seen.SelectMany(text => BookingReferencePattern().Matches(text).Select(match => match.Value)).ToHashSet();

        if (ClaimsBooked(reply) && !bookedThisTurn && (notBookedThisTurn || knownReferences.Count == 0))
        {
            yield return $"{FalseBookingProblem} Never say it is booked unless book_appointment replied \"Booked\". " +
                "If the patient has just agreed to the time you proposed, call book_appointment with no arguments; otherwise ask them to confirm.";
        }

        foreach (Match match in BookingReferencePattern().Matches(reply))
        {
            if (!knownReferences.Contains(match.Value))
            {
                yield return $"\"{match.Value}\" is not a real booking reference. Only give the reference that book_appointment replied with.";
            }
        }

        foreach (Match match in OtherReference().Matches(reply))
        {
            if (!match.Groups["ref"].Value.StartsWith("BMD-", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"\"{match.Groups["ref"].Value}\" is not a real booking reference. Real references look like BMD-61D3D31B and only come from book_appointment.";
            }
        }
    }

    // "is booked", "confirmed", "Booked." — but not "not booked", "isn't confirmed" or "not yet booked".
    private static bool ClaimsBooked(string reply) =>
        BookedWord().Matches(reply).Any(match =>
        {
            var before = reply[Math.Max(0, match.Index - 16)..match.Index].ToLowerInvariant();
            return !before.Contains("not") && !before.Contains("n't") && !before.Contains("yet");
        });

    private static HashSet<DayOfWeek> WeekdaysIn(string text) =>
        WeekdayName().Matches(text).Select(match => Enum.Parse<DayOfWeek>(match.Groups["day"].Value, ignoreCase: true)).ToHashSet();

    private IReadOnlyList<string> FindNameAndPriceProblems(string reply)
    {
        var doctors = _doctorService.GetAll();
        var problems = new List<string>();

        // "Dr. Ravi Kumar" or "Doctor Ravi": the words after the title must match one of our doctors' names.
        // "Dr. Cardiology" is accepted as a short form of "Doctor Cardiology".
        var knownNames = doctors.Select(doctor => NameAfterTitle(doctor.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in DoctorMention().Matches(reply))
        {
            // The pattern may also grab the next capitalised word ("Doctor Cardiology Monday"), so a known name
            // followed by more words is fine too.
            var name = match.Groups["name"].Value;
            var isKnown = knownNames.Any(known =>
                name.Equals(known, StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(known + " ", StringComparison.OrdinalIgnoreCase));
            if (!isKnown)
            {
                problems.Add($"\"{match.Value}\" is not a doctor at this hospital. The doctors are: {string.Join(", ", doctors.Select(doctor => doctor.Name))}.");
            }
        }

        // Every dollar amount must be a real fee or price.
        var knownAmounts = KnownAmounts(doctors);
        foreach (Match match in DollarAmount().Matches(reply))
        {
            var amount = decimal.Parse(match.Groups["amount"].Value, CultureInfo.InvariantCulture);
            if (!knownAmounts.Contains(amount))
            {
                problems.Add($"\"{match.Value}\" is not a real fee or price here. Use only the fees and prices in the HOSPITAL INFORMATION.");
            }
        }

        return problems;
    }

    // Finds clock times written as 09:00, 9:00, 9.00, 2:20 PM or 9 am.
    private static IEnumerable<(string Text, TimeOnly Time)> ClockTimes(string text)
    {
        foreach (Match match in ClockTime().Matches(text))
        {
            var hour = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
            var minute = match.Groups["minute"].Success ? int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture) : 0;
            var meridiem = match.Groups["meridiem"].Value.ToLowerInvariant().Replace(".", "");
            if (meridiem == "pm" && hour < 12)
            {
                hour += 12;
            }
            else if (meridiem == "am" && hour == 12)
            {
                hour = 0;
            }

            if (hour < 24 && minute < 60)
            {
                yield return (match.Value.Trim(), new TimeOnly(hour, minute));
            }
        }
    }

    private static IEnumerable<TimeOnly> TimesIn(string text) => ClockTimes(text).Select(found => found.Time);

    private HashSet<decimal> KnownAmounts(IReadOnlyList<Doctor> doctors)
    {
        var amounts = doctors.SelectMany(doctor => new[] { doctor.ConsultationFee, doctor.FollowUpFee })
            .Concat(_hospitalRepo.GetServicePrices().Select(price => price.Price))
            .ToHashSet();

        // Amounts written into the hospital's own text, e.g. parking "$3 per hour".
        var info = _hospitalRepo.GetInfo();
        foreach (Match match in DollarAmount().Matches($"{info.Parking} {info.PublicTransport}"))
        {
            amounts.Add(decimal.Parse(match.Groups["amount"].Value, CultureInfo.InvariantCulture));
        }

        return amounts;
    }

    private static string NameAfterTitle(string doctorName) => DoctorTitle().Replace(doctorName, "").Trim();

    [GeneratedRegex(@"\b(?:Dr\.?|Doctor)\s+(?<name>[A-Z][a-z]+(?:\s+[A-Z][a-z]+)?)")]
    private static partial Regex DoctorMention();

    [GeneratedRegex(@"^(?:Dr\.?|Doctor)\s+")]
    private static partial Regex DoctorTitle();

    [GeneratedRegex(@"\$\s?(?<amount>\d+(?:\.\d{1,2})?)")]
    private static partial Regex DollarAmount();

    [GeneratedRegex(@"\b(?<day>monday|tuesday|wednesday|thursday|friday|saturday|sunday)s?\b", RegexOptions.IgnoreCase)]
    private static partial Regex WeekdayName();

    [GeneratedRegex(@"\b(?:booked|confirmed)\b", RegexOptions.IgnoreCase)]
    private static partial Regex BookedWord();

    // The real reference format, see BookingReference.For.
    [GeneratedRegex(@"\bBMD-[0-9A-F]{8}\b")]
    private static partial Regex BookingReferencePattern();

    // "reference number is 123456", "reference: AB12", "booking reference #9981".
    [GeneratedRegex(@"\breference(?:\s+(?:number|no\.?|code|id))?\s*(?:is|:|#)?\s*#?(?<ref>[A-Z0-9][A-Z0-9-]*\d[A-Z0-9-]*)", RegexOptions.IgnoreCase)]
    private static partial Regex OtherReference();

    // "the function", "a tool", "function call", "call the ... function".
    [GeneratedRegex(@"\b(?:function|functions|tool|tools|tool call)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ToolTalk();

    // "I'll look up…", "I will check…", "Let me find…", "I'm going to search…".
    [GeneratedRegex(@"\b(?:I'll|I will|let me|I'm going to|I am going to)\s+(?:now\s+)?(?:look\s+up|look|check|call|find|search|fetch|get)\b[^.!?]*",
        RegexOptions.IgnoreCase)]
    private static partial Regex PromiseToLookUp();

    // Either hour:minute (with an optional am/pm) or an hour with am/pm. Plain numbers like "12 years" don't match.
    [GeneratedRegex(@"\b(?<hour>[01]?\d|2[0-3])[:.](?<minute>[0-5]\d)(?:\s*(?<meridiem>[ap]\.?m\.?))?(?![\d])|\b(?<hour>1[0-2]|0?[1-9])\s*(?<meridiem>[ap]\.?m\.?)(?![a-z])", RegexOptions.IgnoreCase)]
    private static partial Regex ClockTime();
}
