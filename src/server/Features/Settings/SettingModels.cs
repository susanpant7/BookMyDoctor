using MyHospitalAI.Features.Chats;

namespace MyHospitalAI.Features.Settings;

public sealed record Setting(string Key, string Label, string Description, string Value, string DefaultValue, string Kind);

public sealed record UpdateSettingRequest(string Value);

// The notes on the Settings page about the services that can run the chat and the embedding models. The Has… flags
// say whether the keys are set in the .env file; the keys themselves are never sent.
public sealed record AiProvidersInfo(
    string ApiKeyName, bool HasApiKey, IReadOnlyList<AiProvider> Providers,
    string EmbeddingApiKeyName, bool HasEmbeddingApiKey, IReadOnlyList<EmbeddingProvider> EmbeddingProviders);
