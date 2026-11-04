using MyHospitalAI.Features.Settings;

namespace MyHospitalAI.Features.Knowledge;

public sealed record UploadedFile(string FileName, string Text);

// Sections is how many were stored; Error is set instead when the file was rejected.
public sealed record UploadFileResult(string FileName, int Sections, string? Error);

// A knowledge file written on the Knowledge page: a title and its sections, without any Markdown.
// FileName is set when editing an existing file, and left out for a new one (it is made from the title).
public sealed record SaveKnowledgeDocumentRequest(string? FileName, string Title, IReadOnlyList<KnowledgeSectionInput> Sections);

public sealed record KnowledgeSectionInput(string Title, string Text);

public sealed record KnowledgeSearchResult(double Score, string Id, string Source, string Title, string Text);

public interface IKnowledgeService
{
    IReadOnlyList<KnowledgeDocument> GetChunks();

    // Stores the sections of each Markdown file, replacing the file's earlier sections. Every file is checked and
    // embedded first, so a bad file changes nothing.
    Task<IReadOnlyList<UploadFileResult>> UploadAsync(IReadOnlyList<UploadedFile> files, CancellationToken cancellationToken = default);

    // Saves a file written as a title and sections, replacing its earlier sections like an upload. Throws
    // ArgumentException for missing or clashing titles; problems from the upload checks come back in the result's Error.
    Task<UploadFileResult> SaveDocumentAsync(SaveKnowledgeDocumentRequest request, CancellationToken cancellationToken = default);

    // Uploads the starting files shipped with the app, then builds the generated ones (doctors, specialties, hospital).
    // onProgress is told how many bundled files are done of how many (one file at a time).
    Task<IReadOnlyList<UploadFileResult>> LoadDefaultsAsync(CancellationToken cancellationToken = default, Func<int, int, Task>? onProgress = null);

    // Returns how many sections were deleted. Throws ArgumentException for the generated files
    // (doctor-settings.md, specialty-settings.md, hospital-settings.md), which follow the data they are built from.
    int DeleteSource(string source);

    // Deletes every stored section, e.g. when no embedding model is selected any more. Returns how many were deleted.
    int DeleteAll();

    // Makes doctor-settings.md match the doctors, embedding only the sections that changed.
    Task SyncDoctorSettingsAsync(CancellationToken cancellationToken = default);

    // Makes specialty-settings.md match the specialties and their doctors, embedding only the sections that changed.
    Task SyncSpecialtySettingsAsync(CancellationToken cancellationToken = default);

    // Makes hospital-settings.md match the hospital settings, embedding only the sections that changed.
    Task SyncHospitalSettingsAsync(CancellationToken cancellationToken = default);

    // All of the above.
    Task SyncSettingsAsync(CancellationToken cancellationToken = default);

    // Embeds every stored section again with the current embedding model (after the model or its address changed,
    // the old vectors can't be compared with new ones). Returns how many sections were embedded. Throws
    // HttpRequestException when the embedding service fails; the old vectors are then kept.
    Task<int> ReembedAllAsync(CancellationToken cancellationToken = default);

    // The same with the given embedding settings, before they are saved: used when switching the embedding model.
    // onProgress is told how many sections are done of how many, before the first batch and after each one.
    Task<int> ReembedAllAsync(AiSettings settings, CancellationToken cancellationToken = default, Func<int, int, Task>? onProgress = null);

    Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(string query, int top, CancellationToken cancellationToken = default);
}
