namespace BookMyDoctor.Persistence.Entities;

public class ChatMessageEntity
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public required string Sender { get; set; }
    public required string Text { get; set; }
    public DateTimeOffset SentAt { get; set; }
}
