namespace BookMyDoctor.Features.Settings;

public static class SettingKeys
{
    public const string AssistantSystemPrompt = "assistant.system_prompt";
    public const string AssistantFactCheck = "assistant.fact_check";
    public const string AssistantMaxToolRounds = "assistant.max_tool_rounds";
    public const string AssistantUncheckedReply = "assistant.unchecked_reply";
    public const string AssistantUnavailableReply = "assistant.unavailable_reply";
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
    public const string ChatKeepAlive = "chat.keep_alive";
}

public enum SettingKind
{
    Text,
    LongText,   // several lines, e.g. the system prompt
    Integer,
    Number,
    Boolean     // "true" or "false"
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
        new(SettingKeys.AssistantSystemPrompt, "System prompt",
            "The instructions the chat model gets with every message: who it is and the rules it follows. These placeholders are filled in for each message: {patient_name} (the signed-in patient), {today} (today's date), {calendar} (the next 7 days with their dates, so the model doesn't work dates out itself) and {hospital_phone} (from the Hospital tab). Keep the rules about answering only from the HOSPITAL INFORMATION, using the tools for free times and bookings, never inventing facts and confirming bookings; the assistant relies on them. Changes apply to the next message.",
            DefaultSystemPrompt, SettingKind.LongText),
        new(SettingKeys.AssistantFactCheck, "Check replies before sending",
            "Before a reply reaches the patient, check it for doctors who don't work here, prices that don't exist, appointment times nobody looked up, and talk about internal tools. A reply that fails is rewritten once; if it fails again, the patient gets the reply below instead. Turning this off makes replies faster but lets made-up facts through.",
            "true", SettingKind.Boolean),
        new(SettingKeys.AssistantMaxToolRounds, "Lookups per reply",
            "The most times the model may call its tools (look up doctors, free times, book…) while answering one message. Stops a confused model from looping. Booking usually needs 2.",
            "5", SettingKind.Integer, 1, 10),
        new(SettingKeys.AssistantUncheckedReply, "Reply when facts can't be checked",
            "Sent instead of a reply that failed the fact check twice, so the patient never sees made-up facts.",
            "Sorry, I couldn't check that properly. You can see every doctor, their fees and their details on the Doctors page."),
        new(SettingKeys.AssistantUnavailableReply, "Reply when the assistant is unavailable",
            "Sent when the chat model can't be reached, takes too long, or keeps looking things up without answering.",
            "Sorry, I'm having trouble right now. Please try again in a moment."),
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
            "3", SettingKind.Integer, 0, 20),
        new(SettingKeys.ChatKeepAlive, "Keep the model loaded for",
            "How long Ollama keeps the chat model in memory after a message, e.g. 30m or 1h. Loading it again takes several seconds, so short values make the first reply after a pause slow; long values keep the memory in use.",
            "30m")
    ];

    // Placeholders: {patient_name}, {today}, {calendar}, {hospital_phone}. See the System prompt setting.
    public const string DefaultSystemPrompt = """
        You are Vaidya Mitra, a friendly assistant for BookMyDoctor Hospital.
        You are talking to a patient named {patient_name}. Help them find the right specialty and doctor, book appointments,
        and answer questions about the hospital.
        Today is {today}.
        Use this calendar for dates; don't work them out yourself:
        {calendar}
        Do not give medical diagnoses or prescribe medicine.

        Rules:
        - Answer questions about doctors (fees, languages, weekly hours, experience), specialties, the hospital (address,
          opening hours, parking, facilities, test prices) and policies only from the HOSPITAL INFORMATION attached to the
          patient's latest message. Never invent doctors, prices, times or policies.
        - If the patient asks for everything or a complete list (all doctors, all specialties, everything about the hospital),
          don't try to list it all. Briefly mention what is relevant to them, then tell them they can see all the doctors on
          the Doctors page of the app, or contact the hospital at {hospital_phone} for anything else.
        - If the answer isn't in the HOSPITAL INFORMATION, say you don't have that information and suggest contacting the
          hospital at {hospital_phone}.
        - For free appointment times use get_available_slots, for the patient's own appointments use get_my_appointments,
          and to book use book_appointment. Never guess free times or bookings. Call the tool straight away; never ask the
          patient for permission to look something up and never say you will check it.
        - When you share free times, write up to four of them in your reply and ask which suits; don't end with an empty list.
        - If the patient describes an emergency warning sign, tell them to call 911 first.
        - Keep replies to two or three short sentences.
        - When the patient picks one of the times you showed, call book_appointment with that time and their health problem
          as the reason (don't look up the times again). Then ask the patient to confirm the summary it gives you.
          When they say yes, call book_appointment with no arguments.
        - Never say an appointment is booked unless book_appointment replied "Booked", and then give the patient the reference.
        - Never mention tools, functions or the information block to the patient, and never describe what you are about
          to look up; just look it up and answer naturally.
        """;

    public static SettingDefinition? Find(string key) => All.FirstOrDefault(definition => definition.Key == key);

    // SHA-256 (lowercase hex, UTF-8) of default values that a later version replaced. A stored value that still matches
    // one was never edited, so StartupSeeder updates it to the current default; edited values are left alone.
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> ReplacedDefaultHashes { get; } =
        new Dictionary<string, IReadOnlySet<string>>
        {
            // The first system prompt, which looked up doctors, specialties and hospital details with tools.
            [SettingKeys.AssistantSystemPrompt] = new HashSet<string> { "756de87e0599f671c833c4cf6de927a7bd5cdd9dc13127efc32eb87012a4ca11" }
        };
}
