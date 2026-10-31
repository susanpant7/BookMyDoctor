using BookMyDoctor.Features.Chats;
using BookMyDoctor.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookMyDoctor.Persistence;

public sealed class ChatsRepo : IChatsRepo
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ChatsRepo(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public IReadOnlyList<ChatMessage> GetMessages(Guid patientId)
    {
        using var db = _factory.CreateDbContext();
        return db.ChatMessages
            .AsNoTracking()
            .Where(message => message.PatientId == patientId)
            .OrderBy(message => message.SentAt)
            .ToList()
            .Select(message => new ChatMessage(message.Id, message.PatientId, message.Sender, message.Text, message.SentAt))
            .ToList();
    }

    public void Add(ChatMessage message)
    {
        using var db = _factory.CreateDbContext();
        db.ChatMessages.Add(new ChatMessageEntity
        {
            Id = message.Id,
            PatientId = message.PatientId,
            Sender = message.Sender,
            Text = message.Text,
            SentAt = message.SentAt
        });
        db.SaveChanges();
    }
}
