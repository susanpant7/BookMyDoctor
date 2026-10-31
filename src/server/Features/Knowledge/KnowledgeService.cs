using BookMyDoctor.Features.Settings;

namespace BookMyDoctor.Features.Knowledge;

public sealed class KnowledgeService : IKnowledgeService
{
    private const int MaxFileBytes = 1_000_000;

    private readonly KnowledgeSources _sources;
    private readonly IEmbedder _embedder;
    private readonly IVectorStore _store;
    private readonly IAiSettingsProvider _settings;
    private readonly ILogger<KnowledgeService> _logger;

    public KnowledgeService(
        KnowledgeSources sources, IEmbedder embedder, IVectorStore store, IAiSettingsProvider settings, ILogger<KnowledgeService> logger)
    {
        _sources = sources;
        _embedder = embedder;
        _store = store;
        _settings = settings;
        _logger = logger;
    }

    // Sections stored before titles and positions were saved have no title. For those, the title and order come from
    // the file's own "# " line and sections: the bundled file of the same name, or the fixed title of a generated file.
    public IReadOnlyList<KnowledgeDocument> GetChunks()
    {
        var chunks = _store.GetAll();
        if (chunks.All(chunk => chunk.DocumentTitle.Length > 0))
        {
            return chunks;
        }

        var bundled = _sources.GetBundledFiles()
            .SelectMany(file => KnowledgeSources.SplitMarkdown(file.FileName, file.Text))
            .ToDictionary(document => document.Id);
        return chunks
            .Select(chunk => chunk.DocumentTitle.Length > 0
                ? chunk
                : bundled.TryGetValue(chunk.Id, out var original)
                    ? chunk with { DocumentTitle = original.DocumentTitle, Position = original.Position }
                    : chunk with { DocumentTitle = KnowledgeSources.TitleFor(chunk.Source) })
            .OrderBy(chunk => chunk.Source).ThenBy(chunk => chunk.Position)
            .ToList();
    }

    public async Task<IReadOnlyList<UploadFileResult>> UploadAsync(IReadOnlyList<UploadedFile> files, CancellationToken cancellationToken = default)
    {
        var results = new List<UploadFileResult>();
        var accepted = new List<(string FileName, List<(KnowledgeDocument Document, float[] Vector)> Items)>();

        foreach (var file in files)
        {
            var (documents, error) = Parse(file, accepted.Select(a => a.FileName));
            if (documents is null)
            {
                results.Add(new UploadFileResult(file.FileName, 0, error));
                continue;
            }

            try
            {
                var vectors = await _embedder.EmbedDocumentsAsync(documents.Select(document => document.Text).ToList(), cancellationToken);
                accepted.Add((file.FileName, documents.Zip(vectors).ToList()));
                results.Add(new UploadFileResult(file.FileName, documents.Count, null));
            }
            catch (HttpRequestException ex)
            {
                results.Add(new UploadFileResult(file.FileName, 0, $"Cannot reach Ollama or the embedding model ({ex.Message}). Make sure Ollama is running and the model is pulled."));
            }
        }

        foreach (var (fileName, items) in accepted)
        {
            _store.ReplaceSource(fileName, items);
        }
        return results;
    }

    public async Task<UploadFileResult> SaveDocumentAsync(SaveKnowledgeDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var title = request.Title.Trim();
        if (title.Length == 0 || title.Length > 100 || title.Contains('\n'))
        {
            throw new ArgumentException("The title is required, on one line, and can be up to 100 characters.");
        }
        if (request.Sections.Count == 0)
        {
            throw new ArgumentException("Add at least one section.");
        }
        foreach (var section in request.Sections)
        {
            if (section.Title.Trim().Length == 0 || section.Title.Contains('\n') || section.Text.Trim().Length == 0)
            {
                throw new ArgumentException("Every section needs a name (on one line) and some details.");
            }
            // A line starting with "## " would split the section in two when the file is read back.
            if (section.Text.ReplaceLineEndings("\n").Split('\n').Any(line => line.StartsWith("# ") || line.StartsWith("## ")))
            {
                throw new ArgumentException($"The details of '{section.Title.Trim()}' have a line starting with '#'. Remove the '#'.");
            }
        }

        var fileName = request.FileName ?? KnowledgeSources.FileNameFor(title);
        if (request.FileName is null && _store.GetHashes(fileName).Count > 0)
        {
            throw new ArgumentException($"There is already a knowledge file called '{title}'. Choose another title, or edit that one.");
        }

        var markdown = $"# {title}\n\n" + string.Join("\n\n", request.Sections.Select(section => $"## {section.Title.Trim()}\n{section.Text.Trim()}"));
        return (await UploadAsync([new UploadedFile(fileName, markdown)], cancellationToken))[0];
    }

    public Task<IReadOnlyList<UploadFileResult>> LoadDefaultsAsync(CancellationToken cancellationToken = default) =>
        UploadAsync(_sources.GetBundledFiles().Select(file => new UploadedFile(file.FileName, file.Text)).ToList(), cancellationToken);

    public int DeleteSource(string source)
    {
        if (KnowledgeSources.GeneratedSources.Contains(source))
        {
            throw new ArgumentException($"{source} follows the doctors and hospital settings. Change those instead.");
        }
        return _store.RemoveSource(source);
    }

    public async Task SyncDoctorSettingsAsync(CancellationToken cancellationToken = default)
    {
        await SyncGeneratedAsync(KnowledgeSources.DoctorSettingsSource, _sources.GetDoctorSettings(), cancellationToken);

        // Profiles from before doctor-settings.md existed.
        if (_store.RemoveSource(KnowledgeSources.LegacyDoctorsSource) is > 0 and var legacy)
        {
            _logger.LogInformation("Removed {Count} old doctor profiles (now in {Source})", legacy, KnowledgeSources.DoctorSettingsSource);
        }
    }

    public Task SyncSpecialtySettingsAsync(CancellationToken cancellationToken = default) =>
        SyncGeneratedAsync(KnowledgeSources.SpecialtySettingsSource, _sources.GetSpecialtySettings(), cancellationToken);

    public Task SyncHospitalSettingsAsync(CancellationToken cancellationToken = default) =>
        SyncGeneratedAsync(KnowledgeSources.HospitalSettingsSource, _sources.GetHospitalSettings(), cancellationToken);

    public async Task SyncSettingsAsync(CancellationToken cancellationToken = default)
    {
        await SyncDoctorSettingsAsync(cancellationToken);
        await SyncSpecialtySettingsAsync(cancellationToken);
        await SyncHospitalSettingsAsync(cancellationToken);
    }

    // Makes a generated file's sections match `wanted`: embeds only new or changed sections (by content hash)
    // and removes sections that no longer exist, e.g. a deleted doctor.
    private async Task SyncGeneratedAsync(string source, IReadOnlyList<KnowledgeDocument> wanted, CancellationToken cancellationToken)
    {
        var stored = _store.GetHashes(source);

        var changed = wanted.Where(document => !stored.TryGetValue(document.Id, out var hash) || hash != document.Hash).ToList();
        if (changed.Count > 0)
        {
            var vectors = await _embedder.EmbedDocumentsAsync(changed.Select(document => document.Text).ToList(), cancellationToken);
            _store.Upsert(changed.Zip(vectors).ToList());
        }

        var wantedIds = wanted.Select(document => document.Id).ToHashSet();
        var removed = stored.Keys.Where(id => !wantedIds.Contains(id)).ToList();
        if (removed.Count > 0)
        {
            _store.Remove(removed);
        }

        _logger.LogInformation("{Source} synced: {Changed} embedded, {Removed} removed", source, changed.Count, removed.Count);
    }

    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(string query, int top, CancellationToken cancellationToken = default)
    {
        var vector = await _embedder.EmbedQueryAsync(query, cancellationToken);
        return _store.Search(vector, top, minScore: -1)
            .Select(result => new KnowledgeSearchResult(
                Math.Round(result.Score, 3), result.Document.Id, result.Document.Source, result.Document.Title, result.Document.Text))
            .ToList();
    }

    private static (List<KnowledgeDocument>? Documents, string? Error) Parse(UploadedFile file, IEnumerable<string> alreadyInUpload)
    {
        if (!file.FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return (null, "Only Markdown (.md) files can be uploaded.");
        }
        if (file.FileName.Contains('#'))
        {
            return (null, "This file name is not allowed. Rename the file.");
        }
        if (KnowledgeSources.GeneratedSources.Contains(file.FileName))
        {
            return (null, $"{file.FileName} is generated from the doctors and hospital settings. Rename your file.");
        }
        if (alreadyInUpload.Contains(file.FileName))
        {
            return (null, "Another file with the same name is in this upload.");
        }
        if (file.Text.Length > MaxFileBytes)
        {
            return (null, "The file is larger than 1 MB.");
        }

        var documents = KnowledgeSources.SplitMarkdown(file.FileName, file.Text).ToList();
        if (documents.Count == 0)
        {
            return (null, "The file has no sections. Add at least one '## Section title' followed by its content.");
        }
        if (documents.Any(document => document.Text.Contains('\n') is false))
        {
            return (null, "A section has no content under its '## ' title.");
        }
        var duplicate = documents.GroupBy(document => document.Id).FirstOrDefault(group => group.Count() > 1);
        return duplicate is null ? (documents, null) : (null, $"Section '{duplicate.First().Title}' appears more than once.");
    }
}
