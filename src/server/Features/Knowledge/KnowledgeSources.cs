using System.Text.RegularExpressions;
using BookMyDoctor.Features.Doctors;
using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Knowledge;

// Where knowledge text comes from: the starting files shipped in the Knowledge folder, and a profile per doctor.
// Live facts (fees, slots, bookings) are deliberately left out; they are looked up directly when needed.
public sealed partial class KnowledgeSources
{
    // The source name of doctor profiles. Uploaded files cannot use it.
    public const string DoctorsSource = "doctors";

    private readonly IDoctorsRepo _doctorsRepo;
    private readonly string _folder;

    public KnowledgeSources(IDoctorsRepo doctorsRepo) : this(doctorsRepo, Path.Combine(AppContext.BaseDirectory, "Knowledge"))
    {
    }

    internal KnowledgeSources(IDoctorsRepo doctorsRepo, string folder)
    {
        _doctorsRepo = doctorsRepo;
        _folder = folder;
    }

    // The starting knowledge files: file name and text.
    public IReadOnlyList<(string FileName, string Text)> GetBundledFiles() =>
        Directory.Exists(_folder)
            ? Directory.GetFiles(_folder, "*.md").Order().Select(file => (Path.GetFileName(file), File.ReadAllText(file))).ToList()
            : [];

    public IReadOnlyList<KnowledgeDocument> GetDoctorProfiles() => _doctorsRepo.GetAll().Select(DoctorProfile).ToList();

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

    internal static KnowledgeDocument DoctorProfile(Doctor doctor) =>
        new(
            $"doctor:{doctor.Id}",
            DoctorsSource,
            $"{doctor.Name} – profile",
            $"""
            ## {doctor.Name} – profile
            {doctor.Name} is a {doctor.Specialty} doctor at BookMyDoctor Hospital with {doctor.ExperienceYears} years of experience.
            Languages spoken: {string.Join(", ", doctor.Languages)}.
            {doctor.Bio}
            """);

    private static string Slug(string title) => NonAlphanumeric().Replace(title.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex(@"\n(?=## )")]
    private static partial Regex SectionStart();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
