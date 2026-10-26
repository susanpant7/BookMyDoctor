namespace BookMyDoctor.Features.Chats;

public sealed record ChatMessage(
    Guid Id,
    Guid PatientId,
    string Sender,
    string Text,
    DateTimeOffset SentAt);

// What the client sends; the patient comes from the signed-in user's token.
public sealed record NewMessageRequest(string Text);

public sealed record SendMessageRequest(
    Guid PatientId,
    string Sender,
    string Text);
