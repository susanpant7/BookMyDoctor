using System.Globalization;

namespace BookMyDoctor.Features.Settings;

// The settings the chat assistant, the embedder and the retriever read, parsed from the Settings table.
public sealed record AiSettings(
    string ChatModel,
    double Temperature,
    int ContextSize,
    int MaxHistoryMessages,
    int ToolHistoryTurns,
    string EmbeddingModel,
    string EmbeddingQueryPrefix,
    string EmbeddingDocumentPrefix,
    int RetrievalTop,
    double RetrievalMinScore,
    double RetrievalMaxScoreGap)
{
    // A value that is missing or cannot be read falls back to its default.
    public static AiSettings From(IReadOnlyDictionary<string, string> values) => new(
        Text(values, SettingKeys.ChatModel),
        Number(values, SettingKeys.ChatTemperature),
        (int)Number(values, SettingKeys.ChatContextSize),
        (int)Number(values, SettingKeys.ChatHistoryMessages),
        (int)Number(values, SettingKeys.ChatToolHistoryTurns),
        Text(values, SettingKeys.EmbeddingModel),
        Text(values, SettingKeys.EmbeddingQueryPrefix),
        Text(values, SettingKeys.EmbeddingDocumentPrefix),
        (int)Number(values, SettingKeys.RetrievalTopK),
        Number(values, SettingKeys.RetrievalMinScore),
        Number(values, SettingKeys.RetrievalMaxScoreGap));

    public static AiSettings Defaults { get; } = From(new Dictionary<string, string>());

    // Some embedding models need a prefix on queries and on documents. An empty prefix means none.
    public static string WithPrefix(string prefix, string text) => prefix.Length == 0 ? text : $"{prefix} {text}";

    private static string Text(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : SettingDefaults.Find(key)!.Value;

    private static double Number(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && double.TryParse(value, CultureInfo.InvariantCulture, out var number)
            ? number
            : double.Parse(SettingDefaults.Find(key)!.Value, CultureInfo.InvariantCulture);
}

public interface IAiSettingsProvider
{
    // Reads the current values, so a change on the Settings page applies to the next message.
    AiSettings Get();
}
