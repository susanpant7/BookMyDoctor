using BookMyDoctor.Features.Chats.Tools;

namespace BookMyDoctor.Features.Chats;

// The final reply text, and the free times looked up for it (if any), which the chat shows as buttons.
public sealed record AssistantReply(string Text, SlotOffer? SlotsShown = null);

public interface IChatAssistant
{
    // Returns the final reply. While working, the assistant may show a draft through `draft` as it is written;
    // the final reply always replaces it, so a draft that fails the checks never stays on screen.
    Task<AssistantReply> ReplyAsync(ChatMessage patientMessage, IReplyDraft? draft = null);
}

// Where the assistant shows its reply while it's being written (the chat pushes it to the patient's tabs).
public interface IReplyDraft
{
    // The reply so far.
    Task ShowAsync(string textSoFar);

    // Removes the draft, e.g. when the model switches to calling a tool or has to rewrite its answer.
    Task ClearAsync();
}
