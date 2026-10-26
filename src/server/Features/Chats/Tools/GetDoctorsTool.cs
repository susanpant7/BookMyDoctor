using System.Text.Json;
using BookMyDoctor.Features.Doctors;

namespace BookMyDoctor.Features.Chats.Tools;

public sealed class GetDoctorsTool : IAssistantTool
{
    private readonly IDoctorService _doctorService;

    public GetDoctorsTool(IDoctorService doctorService)
    {
        _doctorService = doctorService;
    }

    public string Name => "get_doctors";

    public string Description =>
        "List the hospital's doctors with their specialty, experience, languages and consultation fees. " +
        "Use when the patient asks which doctors there are, about a doctor's fee or language, or to find a doctor for a specialty.";

    public JsonElement Parameters { get; } = ToolArguments.Schema("""
        {
          "type": "object",
          "properties": {
            "specialty": { "type": "string", "description": "Optional specialty to filter by, e.g. Dermatology or Pediatrics" }
          },
          "required": []
        }
        """);

    public Task<string> RunAsync(JsonElement arguments, ToolContext context, CancellationToken cancellationToken)
    {
        var specialty = ToolArguments.OptionalString(arguments, "specialty");
        var doctors = _doctorService.GetAll()
            .Where(doctor => specialty is null || doctor.Specialty.Contains(specialty, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (doctors.Count == 0)
        {
            var specialties = string.Join(", ", _doctorService.GetAll().Select(doctor => doctor.Specialty).Distinct());
            return Task.FromResult($"No doctors found for specialty '{specialty}'. Available specialties: {specialties}.");
        }

        return Task.FromResult(string.Join("\n", doctors.Select(doctor =>
            $"- {doctor.Name} | {doctor.Specialty} | {doctor.ExperienceYears} years | speaks {string.Join(", ", doctor.Languages)} | " +
            $"new visit ${doctor.ConsultationFee}, follow-up ${doctor.FollowUpFee}")));
    }
}
