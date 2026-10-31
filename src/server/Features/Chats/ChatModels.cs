namespace BookMyDoctor.Features.Chats;

public sealed record ChatMessage(
    Guid Id,
    Guid PatientId,
    string Sender,
    string Text,
    DateTimeOffset SentAt);

// What the client sends; the patient comes from the signed-in user's token.
public sealed record NewMessageRequest(string Text);

// Free times the assistant just looked up, pushed to the chat so the patient can pick one with a button.
public sealed record BookingOptions(Guid DoctorId, string DoctorName, string Day, IReadOnlyList<SlotOption> Slots);

public sealed record SlotOption(DateTimeOffset StartsAt, string Label);

// Sent when the patient confirms a time from the buttons; the patient comes from the token.
public sealed record BookSlotRequest(Guid DoctorId, DateTimeOffset StartsAt, string Reason);

public sealed record SaveMessageRequest(
    Guid PatientId,
    string Sender,
    string Text);
