using System.Text.Json;
using BookMyDoctor.Features.Specialties;

namespace BookMyDoctor.Features.Chats.Tools;

public sealed class GetSpecialtiesTool : IAssistantTool
{
    private readonly ISpecialtyService _specialtyService;

    public GetSpecialtiesTool(ISpecialtyService specialtyService)
    {
        _specialtyService = specialtyService;
    }

    public string Name => "get_specialties";

    public string Description =>
        "List the medical specialties the hospital offers. Use when the patient asks which specialties or departments there are.";

    public JsonElement Parameters { get; } = ToolArguments.Schema("""{ "type": "object", "properties": {}, "required": [] }""");

    public Task<string> RunAsync(JsonElement arguments, ToolContext context, CancellationToken cancellationToken) =>
        Task.FromResult(string.Join("\n", _specialtyService.GetAll().Select(specialty => $"- {specialty.Name}: {specialty.Description}")));
}
