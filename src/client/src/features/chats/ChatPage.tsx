import type { HubConnection } from "@microsoft/signalr";
import { ArrowLeft, Send } from "lucide-react";
import { FormEvent, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useSession } from "../../shared/session";
import { getMessages, type ChatMessage } from "./chatApi";
import { createChatConnection, joinChat, onReceiveMessage, sendChatMessage } from "./chatHub";

// Adds messages that aren't already in the list (history and live pushes can overlap).
function mergeMessages(current: ChatMessage[], incoming: ChatMessage[]) {
  const known = new Set(current.map((message) => message.id));
  return [...current, ...incoming.filter((message) => !known.has(message.id))];
}

export function ChatPage() {
  const { session } = useSession();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [text, setText] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const [connected, setConnected] = useState(false);
  const threadRef = useRef<HTMLDivElement>(null);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    if (!session) {
      return;
    }

    const token = session.token;
    const connection = createChatConnection(() => token);
    connectionRef.current = connection;
    let stopped = false;

    onReceiveMessage(connection, (message) => setMessages((current) => mergeMessages(current, [message])));

    // A reconnect gets a new connection id, so the server-side group membership must be renewed.
    connection.onreconnecting(() => setConnected(false));
    connection.onreconnected(() => {
      joinChat(connection).then(() => setConnected(true));
    });
    connection.onclose(() => setConnected(false));

    async function start() {
      try {
        await connection.start();
        await joinChat(connection);
        // Load history after joining so nothing sent in between is missed.
        const history = await getMessages();
        if (!stopped) {
          setMessages((current) => mergeMessages(history, current));
          setConnected(true);
        }
      } catch (err) {
        if (!stopped) {
          setError(err instanceof Error ? err.message : "Could not connect to chat.");
        }
      }
    }

    start();

    return () => {
      stopped = true;
      connectionRef.current = null;
      connection.stop();
    };
  }, [session?.userId, session?.token]);

  useEffect(() => {
    threadRef.current?.scrollTo({ top: threadRef.current.scrollHeight });
  }, [messages]);

  if (!session) {
    return <p>Sign in to chat with the AI assistant.</p>;
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    const connection = connectionRef.current;
    if (!text.trim() || sending || !connection) {
      return;
    }

    setError(null);
    setSending(true);
    try {
      // The server pushes both our message and the reply back through "ReceiveMessage".
      await sendChatMessage(connection, text.trim());
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
          {connected ? null : <p className="muted">Connecting...</p>}
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
        <button type="submit" disabled={sending || !connected || !text.trim()} aria-label="Send">
          <Send size={16} aria-hidden="true" />
          Send
        </button>
      </form>
      {error ? <p className="error">{error}</p> : null}
    </section>
  );
}
