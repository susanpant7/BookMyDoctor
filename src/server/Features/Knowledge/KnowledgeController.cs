using BookMyDoctor.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookMyDoctor.Features.Knowledge;

public sealed record DeleteKnowledgeResult(int Deleted);

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/knowledge")]
public sealed class KnowledgeController : ControllerBase
{
    private const long MaxFileBytes = 1_000_000;

    private readonly IKnowledgeService _knowledgeService;

    public KnowledgeController(IKnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }

    [HttpGet("chunks")]
    public ActionResult<IReadOnlyList<KnowledgeDocument>> GetChunks() => Ok(_knowledgeService.GetChunks());

    // Shows which sections a question retrieves, with their similarity scores (before the minimum similarity is applied).
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<KnowledgeSearchResult>>> Search(
        [FromQuery] string q, [FromQuery] int k = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length > 500)
        {
            return BadRequest(new { message = "Provide a question of 1 to 500 characters in 'q'." });
        }

        try
        {
            return Ok(await _knowledgeService.SearchAsync(q.Trim(), Math.Clamp(k, 1, 20), cancellationToken));
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }

    [HttpPost("upload")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<IReadOnlyList<UploadFileResult>>> Upload([FromForm] List<IFormFile> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return BadRequest(new { message = "Select at least one file." });
        }

        var uploaded = new List<UploadedFile>();
        var rejected = new List<UploadFileResult>();
        foreach (var file in files)
        {
            if (file.Length > MaxFileBytes)
            {
                rejected.Add(new UploadFileResult(Path.GetFileName(file.FileName), 0, "The file is larger than 1 MB."));
                continue;
            }

            using var reader = new StreamReader(file.OpenReadStream());
            uploaded.Add(new UploadedFile(Path.GetFileName(file.FileName), await reader.ReadToEndAsync(cancellationToken)));
        }
        return Ok((await _knowledgeService.UploadAsync(uploaded, cancellationToken)).Concat(rejected).ToList());
    }

    [HttpPost("load-defaults")]
    public async Task<ActionResult<IReadOnlyList<UploadFileResult>>> LoadDefaults(CancellationToken cancellationToken) =>
        Ok(await _knowledgeService.LoadDefaultsAsync(cancellationToken));

    // Rebuilds doctor-settings.md, specialty-settings.md and hospital-settings.md from the database, e.g. after edits made while Ollama was down.
    [HttpPost("sync-settings")]
    public async Task<IActionResult> SyncSettings(CancellationToken cancellationToken)
    {
        try
        {
            await _knowledgeService.SyncSettingsAsync(cancellationToken);
            return NoContent();
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = $"Cannot reach Ollama ({ex.Message})." });
        }
    }

    [HttpDelete("sources/{source}")]
    public ActionResult<DeleteKnowledgeResult> DeleteSource(string source) =>
        Ok(new DeleteKnowledgeResult(_knowledgeService.DeleteSource(source)));
}
