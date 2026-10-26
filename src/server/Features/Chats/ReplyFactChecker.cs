using System.Globalization;
using System.Text.RegularExpressions;
using BookMyDoctor.Features.Doctors;
using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Chats;

// Checks a reply before the patient sees it, for the most harmful kinds of made-up facts:
// doctors who don't work here, prices that don't exist, and appointment times nobody looked up.
public sealed partial class ReplyFactChecker
{
    private readonly IDoctorService _doctorService;
    private readonly IHospitalRepo _hospitalRepo;

    public ReplyFactChecker(IDoctorService doctorService, IHospitalRepo hospitalRepo)
    {
        _doctorService = doctorService;
        _hospitalRepo = hospitalRepo;
    }

    // Returns one sentence per problem found; empty when the reply looks fine.
    // `seenTexts` is everything the model was given: tool results, retrieved knowledge and the conversation.
    // A clock time in the reply must appear in one of them, otherwise the model made it up.
    public IReadOnlyList<string> FindProblems(string reply, IEnumerable<string> seenTexts)
    {
        var problems = FindNameAndPriceProblems(reply).ToList();

        var seenTimes = seenTexts.SelectMany(TimesIn).ToHashSet();
        foreach (var (text, time) in ClockTimes(reply))
        {
            if (!seenTimes.Contains(time))
            {
                problems.Add($"\"{text}\" is not a time you looked up. Call get_available_slots before offering or mentioning appointment times.");
            }
        }

        return problems.Distinct().ToList();
    }

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
                problems.Add($"\"{match.Value}\" is not a real fee or price here. Look prices up with get_doctors or get_hospital_info.");
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

    // Either hour:minute (with an optional am/pm) or an hour with am/pm. Plain numbers like "12 years" don't match.
    [GeneratedRegex(@"\b(?<hour>[01]?\d|2[0-3])[:.](?<minute>[0-5]\d)(?:\s*(?<meridiem>[ap]\.?m\.?))?(?![\d])|\b(?<hour>1[0-2]|0?[1-9])\s*(?<meridiem>[ap]\.?m\.?)(?![a-z])", RegexOptions.IgnoreCase)]
    private static partial Regex ClockTime();
}
