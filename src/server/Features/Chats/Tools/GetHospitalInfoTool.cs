using System.Text.Json;
using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Chats.Tools;

public sealed class GetHospitalInfoTool : IAssistantTool
{
    private readonly IHospitalRepo _hospitalRepo;
    private readonly IInsurancePlansRepo _insurancePlansRepo;

    public GetHospitalInfoTool(IHospitalRepo hospitalRepo, IInsurancePlansRepo insurancePlansRepo)
    {
        _hospitalRepo = hospitalRepo;
        _insurancePlansRepo = insurancePlansRepo;
    }

    public string Name => "get_hospital_info";

    public string Description =>
        "The hospital's address, phone, opening hours, parking, public transport, facilities, accepted insurance plans " +
        "and prices of extra tests (ECG, X-ray, blood tests, flu shot). Use for any of those questions.";

    public JsonElement Parameters { get; } = ToolArguments.Schema("""{ "type": "object", "properties": {}, "required": [] }""");

    public Task<string> RunAsync(JsonElement arguments, ToolContext context, CancellationToken cancellationToken)
    {
        var info = _hospitalRepo.GetInfo();
        var hours = _hospitalRepo.GetHours();
        var openDays = hours.Select(day => day.Day).ToHashSet();
        var closedDays = Enum.GetValues<DayOfWeek>().Where(day => !openDays.Contains(day));

        return Task.FromResult($"""
            {info.Name}
            Address: {info.Address}
            Phone: {info.Phone}
            Opening hours: {string.Join("; ", hours.Select(day => $"{day.Day} {day.Opens:HH:mm}–{day.Closes:HH:mm}"))}; closed {string.Join(", ", closedDays)} and on major US public holidays
            Parking: {info.Parking}
            Public transport: {info.PublicTransport}
            Facilities: {string.Join(", ", info.Facilities)}
            Insurance accepted: {string.Join(", ", _insurancePlansRepo.GetAll().Select(plan => plan.Restrictions is null ? plan.Name : $"{plan.Name} ({plan.Restrictions})"))}
            Extra test prices: {string.Join(", ", _hospitalRepo.GetServicePrices().Select(price => $"{price.Name} ${price.Price}"))}
            """);
    }
}
