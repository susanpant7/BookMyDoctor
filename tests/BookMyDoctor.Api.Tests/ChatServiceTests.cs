using BookMyDoctor.Features.Chats;
using BookMyDoctor.Persistence;
using Xunit;

namespace BookMyDoctor.Tests;

public sealed class ChatServiceTests
{
    [Fact]
    public void Send_StoresAndReturnsMessage()
    {
        var service = new ChatService(new ChatsRepo());
        var patientId = Guid.NewGuid();

        var message = service.Send(new SendMessageRequest(patientId, "patient", "Hello assistant"));

        Assert.Equal("patient", message.Sender);
        Assert.Equal("Hello assistant", message.Text);
        Assert.Single(service.GetMessages(patientId));
    }

    [Fact]
    public void Send_RejectsEmptyText()
    {
        var service = new ChatService(new ChatsRepo());

        Assert.Throws<ArgumentException>(() =>
            service.Send(new SendMessageRequest(Guid.NewGuid(), "patient", "  ")));
    }
}
