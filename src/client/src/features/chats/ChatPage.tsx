import { ArrowLeft, Send } from "lucide-react";
import { FormEvent, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useSession } from "../../shared/session";
import { BOOKING_CHAT_ID, getMessages, sendMessage, type ChatMessage } from "./chatApi";

export function ChatPage() {
  const { session } = useSession();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [text, setText] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const threadRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!session) {
      return;
    }
    getMessages(BOOKING_CHAT_ID, session.userId)
      .then(setMessages)
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "Could not load chat."));
  }, [session?.userId]);

  useEffect(() => {
    threadRef.current?.scrollTo({ top: threadRef.current.scrollHeight });
  }, [messages]);

  if (!session) {
    return <p>Sign in to chat with the AI assistant.</p>;
  }

  const patientId = session.userId;

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    if (!text.trim() || sending) {
      return;
    }

    setError(null);
    setSending(true);
    try {
      const message = await sendMessage({
        doctorId: BOOKING_CHAT_ID,
        patientId,
        sender: "patient",
        text: text.trim()
      });
      setMessages((current) => [...current, message]);
      setText("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not send message.");
    } finally {
      setSending(false);
    }
  }

  return (
    <section className="chat">
      <header className="chat-header">
        <Link to="/book" className="chat-back" aria-label="Back to book">
          <ArrowLeft size={18} aria-hidden="true" />
        </Link>
        <div>
          <h1>Vaidya Mitra</h1>
        </div>
      </header>
      <div className="messages" ref={threadRef}>
        {messages.length === 0 ? (
          <p className="muted">How can I help you today?</p>
        ) : null}
        {messages.map((message) => (
          <p key={message.id} className={message.sender === "patient" ? "bubble me" : "bubble"}>
            <span>{message.sender === "patient" ? "You" : "Vaidya Mitra"}</span>
            {message.text}
          </p>
        ))}
      </div>
      <form onSubmit={onSubmit} className="chat-composer">
        <input
          value={text}
          onChange={(event) => setText(event.target.value)}
          placeholder="Write a message"
          aria-label="Message"
        />
        <button type="submit" disabled={sending || !text.trim()} aria-label="Send">
          <Send size={16} aria-hidden="true" />
          Send
        </button>
      </form>
      {error ? <p className="error">{error}</p> : null}
    </section>
  );
}
