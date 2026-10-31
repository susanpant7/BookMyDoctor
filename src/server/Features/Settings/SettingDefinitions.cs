namespace BookMyDoctor.Features.Settings;

public static class SettingKeys
{
    public const string EmbeddingModel = "embedding.model";
    public const string EmbeddingQueryPrefix = "embedding.query_prefix";
    public const string EmbeddingDocumentPrefix = "embedding.document_prefix";
    public const string RetrievalTopK = "retrieval.top_k";
    public const string RetrievalMinScore = "retrieval.min_score";
    public const string RetrievalMaxScoreGap = "retrieval.max_score_gap";
    public const string ChatModel = "chat.model";
    public const string ChatTemperature = "chat.temperature";
    public const string ChatContextSize = "chat.context_size";
    public const string ChatHistoryMessages = "chat.history_messages";
    public const string ChatToolHistoryTurns = "chat.tool_history_turns";
}

public enum SettingKind
{
    Text,
    Integer,
    Number
}

// Min and Max apply to Integer and Number settings.
public sealed record SettingDefinition(
    string Key, string Label, string Description, string Value, SettingKind Kind = SettingKind.Text, double Min = 0, double Max = 0);

// The starting value of every setting. SettingsSeeder adds the ones missing from the table and never changes a row
// that already exists, so values edited on the Settings page are kept.
public static class SettingDefaults
{
    public static IReadOnlyList<SettingDefinition> All { get; } =
    [
        new(SettingKeys.EmbeddingModel, "Embedding model",
            "Model that converts text into vectors. Changing it means every knowledge file must be uploaded again (and the doctor profiles synced), because old and new vectors cannot be compared. Change the two prefixes below to match the new model. The model must also exist in Ollama: run `ollama pull <model>` first.",
            "nomic-embed-text"),
        new(SettingKeys.EmbeddingQueryPrefix, "Embedding query prefix",
            "Text put before the patient's question when it is converted to a vector, joined with one space. It depends on the embedding model: nomic-embed-text expects search_query:.",
            "search_query:"),
        new(SettingKeys.EmbeddingDocumentPrefix, "Embedding document prefix",
            "Text put before each knowledge section when it is converted to a vector, joined with one space. It depends on the embedding model: nomic-embed-text expects search_document:. After changing it, upload the knowledge files again.",
            "search_document:"),
        new(SettingKeys.RetrievalTopK, "Results to retrieve",
            "How many of the closest knowledge sections are sent to the chat model. Higher gives more coverage but adds noise and a larger, slower prompt. Typical: 3 to 5.",
            "4", SettingKind.Integer, 1, 20),
        new(SettingKeys.RetrievalMinScore, "Minimum similarity",
            "Matches scoring below this (0 to 1) are dropped, even if they are in the top results. Higher means stricter matching. The right value depends on the embedding model, so tune it with the search box on the Knowledge page.",
            "0.5", SettingKind.Number, 0, 1),
        new(SettingKeys.RetrievalMaxScoreGap, "Maximum score gap",
            "Sections scoring more than this below the best match are dropped too. Scores bunch together, so weak matches otherwise still pass the minimum similarity. Set 1 to turn this off.",
            "0.05", SettingKind.Number, 0, 1),
        new(SettingKeys.ChatModel, "Chat model",
            "Model that writes the answers and decides when to look things up. Can be changed without re-embedding anything. The assistant's rules were tested with qwen2.5:7b, so re-test after changing it. The model must also exist in Ollama: run `ollama pull <model>` first.",
            "qwen2.5:7b"),
        new(SettingKeys.ChatTemperature, "Temperature",
            "Controls randomness. Lower gives focused, repeatable answers and more reliable tool calls; higher gives more varied ones. Use 0 to 0.3 for factual answers.",
            "0.2", SettingKind.Number, 0, 2),
        new(SettingKeys.ChatContextSize, "Context window (tokens)",
            "Total tokens the model can read at once (instructions, tools, history, retrieved knowledge and the answer). If the prompt is longer, Ollama silently cuts off its start. Larger values use more memory and are slower.",
            "8192", SettingKind.Integer, 2048, 131072),
        new(SettingKeys.ChatHistoryMessages, "Messages of history",
            "How many of the latest chat messages are sent to the model, so it remembers what was said. More adds to the prompt size.",
            "12", SettingKind.Integer, 2, 100),
        new(SettingKeys.ChatToolHistoryTurns, "Turns that keep lookups",
            "How many of the patient's latest messages keep the data the assistant looked up for them, so follow-ups such as 'how much does he charge?' still have the real facts.",
            "3", SettingKind.Integer, 0, 20)
    ];

    public static SettingDefinition? Find(string key) => All.FirstOrDefault(definition => definition.Key == key);
}
