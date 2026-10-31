namespace BookMyDoctor.Features.Knowledge;

public sealed record UploadedFile(string FileName, string Text);

// Sections is how many were stored; Error is set instead when the file was rejected.
public sealed record UploadFileResult(string FileName, int Sections, string? Error);

public sealed record KnowledgeSearchResult(double Score, string Id, string Source, string Title, string Text);

public interface IKnowledgeService
{
    IReadOnlyList<KnowledgeDocument> GetChunks();

    // Stores the sections of each Markdown file, replacing the file's earlier sections. Every file is checked and
    // embedded first, so a bad file changes nothing.
    Task<IReadOnlyList<UploadFileResult>> UploadAsync(IReadOnlyList<UploadedFile> files, CancellationToken cancellationToken = default);

    // Uploads the starting files shipped with the app.
    Task<IReadOnlyList<UploadFileResult>> LoadDefaultsAsync(CancellationToken cancellationToken = default);

    // Returns how many sections were deleted. Throws ArgumentException for the generated files
    // (doctor-settings.md, specialty-settings.md, hospital-settings.md), which follow the data they are built from.
    int DeleteSource(string source);

    // Makes doctor-settings.md match the doctors, embedding only the sections that changed.
    Task SyncDoctorSettingsAsync(CancellationToken cancellationToken = default);

    // Makes specialty-settings.md match the specialties and their doctors, embedding only the sections that changed.
    Task SyncSpecialtySettingsAsync(CancellationToken cancellationToken = default);

    // Makes hospital-settings.md match the hospital settings, embedding only the sections that changed.
    Task SyncHospitalSettingsAsync(CancellationToken cancellationToken = default);

    // All of the above.
    Task SyncSettingsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(string query, int top, CancellationToken cancellationToken = default);
}
