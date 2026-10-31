using System.Text.RegularExpressions;
using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Features.Doctors;
using BookMyDoctor.Features.Hospital;
using BookMyDoctor.Features.Specialties;
using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Knowledge;

// Where knowledge text comes from: the starting files shipped in the Knowledge folder, and three files generated from
// the database: doctor-settings.md (a section per doctor), specialty-settings.md (a section per specialty) and
// hospital-settings.md (the hospital's details).
// Free slots and bookings are deliberately left out; they change constantly and are looked up when needed.
public sealed partial class KnowledgeSources
{
    // Generated from the doctors and the hospital settings and kept in sync by the server. Uploads cannot use these names.
    public const string DoctorSettingsSource = "doctor-settings.md";
    public const string SpecialtySettingsSource = "specialty-settings.md";
    public const string HospitalSettingsSource = "hospital-settings.md";
    public static readonly IReadOnlySet<string> GeneratedSources =
        new HashSet<string>([DoctorSettingsSource, SpecialtySettingsSource, HospitalSettingsSource], StringComparer.OrdinalIgnoreCase);

    // Where doctor profiles were stored before doctor-settings.md; removed on the next sync.
    public const string LegacyDoctorsSource = "doctors";

    private readonly IDoctorsRepo _doctorsRepo;
    private readonly IDoctorSchedulesRepo _schedulesRepo;
    private readonly ISpecialtiesRepo _specialtiesRepo;
    private readonly IHospitalRepo _hospitalRepo;
    private readonly string _folder;

    public KnowledgeSources(
        IDoctorsRepo doctorsRepo, IDoctorSchedulesRepo schedulesRepo, ISpecialtiesRepo specialtiesRepo, IHospitalRepo hospitalRepo)
        : this(doctorsRepo, schedulesRepo, specialtiesRepo, hospitalRepo, Path.Combine(AppContext.BaseDirectory, "Knowledge"))
    {
    }

    internal KnowledgeSources(
        IDoctorsRepo doctorsRepo, IDoctorSchedulesRepo schedulesRepo, ISpecialtiesRepo specialtiesRepo, IHospitalRepo hospitalRepo,
        string folder)
    {
        _doctorsRepo = doctorsRepo;
        _schedulesRepo = schedulesRepo;
        _specialtiesRepo = specialtiesRepo;
        _hospitalRepo = hospitalRepo;
        _folder = folder;
    }

    // The starting knowledge files: file name and text.
    public IReadOnlyList<(string FileName, string Text)> GetBundledFiles() =>
        Directory.Exists(_folder)
            ? Directory.GetFiles(_folder, "*.md").Order().Select(file => (Path.GetFileName(file), File.ReadAllText(file))).ToList()
            : [];

    // doctor-settings.md: one section per doctor. Ids use the doctor's id, so renaming a doctor updates their section.
    public IReadOnlyList<KnowledgeDocument> GetDoctorSettings() =>
        _doctorsRepo.GetAll().Select(doctor => DoctorSection(doctor, _schedulesRepo.GetWeekly(doctor.Id))).ToList();

    // specialty-settings.md: one section per specialty with its doctors. Ids use the specialty's id, so renaming updates it.
    public IReadOnlyList<KnowledgeDocument> GetSpecialtySettings() =>
        _specialtiesRepo.GetAll().Select(specialty => SpecialtySection(specialty, _doctorsRepo.GetBySpecialty(specialty.Id))).ToList();

    // hospital-settings.md: the hospital's details, one section per topic.
    public IReadOnlyList<KnowledgeDocument> GetHospitalSettings() =>
        SplitMarkdown(HospitalSettingsSource, HospitalSettingsMarkdown(
            _hospitalRepo.GetInfo(), _hospitalRepo.GetHours(), _hospitalRepo.GetServicePrices())).ToList();

    // Each "## " section becomes one document. The heading stays in the text so the chunk makes sense on its own.
    internal static IEnumerable<KnowledgeDocument> SplitMarkdown(string fileName, string markdown) =>
        SectionStart().Split(markdown.ReplaceLineEndings("\n"))
            .Where(part => part.StartsWith("## "))
            .Select(part => part.Trim())
            .Select(section =>
            {
                var title = section[3..(section.IndexOf('\n') is var end and >= 0 ? end : section.Length)].Trim();
                return new KnowledgeDocument($"{fileName}#{Slug(title)}", fileName, title, section);
            });

    internal static KnowledgeDocument DoctorSection(Doctor doctor, IReadOnlyList<DoctorSchedule> schedule)
    {
        var hours = schedule.Count == 0
            ? "No regular weekly hours are set."
            : string.Join("; ", schedule
                .OrderBy(block => ((int)block.Day + 6) % 7)   // Monday first
                .ThenBy(block => block.Start)
                .Select(block => $"{block.Day} {block.Start:HH:mm}–{block.End:HH:mm}")) +
              $" ({schedule[0].SlotMinutes}-minute appointments).";

        return new KnowledgeDocument(
            $"{DoctorSettingsSource}#{doctor.Id}",
            DoctorSettingsSource,
            doctor.Name,
            $"""
            ## {doctor.Name}
            {doctor.Name} is a {doctor.Specialty} doctor at BookMyDoctor Hospital with {doctor.ExperienceYears} years of experience.
            Languages spoken: {string.Join(", ", doctor.Languages)}.
            Consultation fees: ${doctor.ConsultationFee:0.##} for a new visit, ${doctor.FollowUpFee:0.##} for a follow-up visit about the same problem within 30 days.
            Weekly hours: {hours} Exact free times depend on bookings and days off.
            {doctor.Bio}
            """.TrimEnd());
    }

    internal static KnowledgeDocument SpecialtySection(Specialty specialty, IReadOnlyList<Doctor> doctors)
    {
        var description = string.IsNullOrWhiteSpace(specialty.Description) ? "" : $" {specialty.Description.Trim().TrimEnd('.')}.";
        var doctorNames = doctors.Count == 0
            ? $"No doctor in {specialty.Name} at the moment."
            : $"{specialty.Name} doctors: {string.Join(", ", doctors.Select(doctor => doctor.Name).Order())}.";

        return new KnowledgeDocument(
            $"{SpecialtySettingsSource}#{specialty.Id}",
            SpecialtySettingsSource,
            specialty.Name,
            $"""
            ## {specialty.Name}
            {specialty.Name} is a specialty at BookMyDoctor Hospital.{description}
            {doctorNames}
            """);
    }

    internal static string HospitalSettingsMarkdown(
        HospitalInfo info,
        IReadOnlyList<HospitalHours> hours,
        IReadOnlyList<ServicePrice> prices)
    {
        var openDays = hours.Select(day => day.Day).ToHashSet();
        var closedDays = Enum.GetValues<DayOfWeek>().Where(day => !openDays.Contains(day)).ToList();
        var sections = new List<string>
        {
            $"""
            ## Hospital address and phone
            {info.Name} is at {info.Address}. Phone: {info.Phone}.
            """,
            $"""
            ## Hospital opening hours
            {info.Name} outpatient opening hours: {(hours.Count == 0 ? "not set" : string.Join("; ", hours
                .OrderBy(day => ((int)day.Day + 6) % 7)
                .Select(day => $"{day.Day} {day.Opens:HH:mm}–{day.Closes:HH:mm}")))}.{(closedDays.Count == 0 ? "" : $" Closed on {string.Join(", ", closedDays)}.")} Also closed on major US public holidays.
            """,
            $"""
            ## Parking and public transport
            Parking at {info.Name}: {info.Parking}
            Public transport: {info.PublicTransport}
            """
        };

        if (info.Facilities.Count > 0)
        {
            sections.Add($"""
                ## Hospital facilities
                {info.Name} has: {string.Join(", ", info.Facilities)}.
                """);
        }

        if (prices.Count > 0)
        {
            sections.Add($"""
                ## Prices of extra tests and procedures
                Extra tests and procedures at {info.Name}, paid on top of the consultation fee: {string.Join("; ", prices.Select(price => $"{price.Name} ${price.Price:0.##}"))}.
                """);
        }

        return string.Join("\n\n", sections);
    }

    private static string Slug(string title) => NonAlphanumeric().Replace(title.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex(@"\n(?=## )")]
    private static partial Regex SectionStart();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
