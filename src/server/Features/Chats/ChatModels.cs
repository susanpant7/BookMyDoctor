namespace BookMyDoctor.Features.Chats;

public sealed record ChatMessage(
    Guid Id,
    Guid DoctorId,
    Guid PatientId,
    string Sender,
    string Text,
    DateTimeOffset SentAt);

public sealed record SendMessageRequest(
    Guid DoctorId,
    Guid PatientId,
    string Sender,
    string Text);
