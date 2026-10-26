import type { HubConnection } from "@microsoft/signalr";
import { ArrowLeft, Send } from "lucide-react";
import { FormEvent, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useSession } from "../../shared/session";
import { getMessages, type ChatMessage } from "./chatApi";
import {
  bookSlot,
  createChatConnection,
  joinChat,
  onAssistantTyping,
  onBookingOptions,
  onReceiveMessage,
  sendChatMessage,
  type BookingOptions
} from "./chatHub";

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
  const [assistantTyping, setAssistantTyping] = useState(false);
  const [bookingOptions, setBookingOptions] = useState<BookingOptions | null>(null);
  const [selectedSlot, setSelectedSlot] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [booking, setBooking] = useState(false);
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
    onAssistantTyping(connection, setAssistantTyping);
    onBookingOptions(connection, (options) => {
      setBookingOptions(options);
      setSelectedSlot(null);
    });

    // A reconnect gets a new connection id, so the server-side group membership must be renewed.
    // A dropped connection may also miss the "typing stopped" push, so clear the indicator.
    connection.onreconnecting(() => {
      setConnected(false);
      setAssistantTyping(false);
    });
    connection.onreconnected(() => {
      joinChat(connection).then(() => setConnected(true));
    });
    connection.onclose(() => {
      setConnected(false);
      setAssistantTyping(false);
    });

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
  }, [messages, assistantTyping, bookingOptions, selectedSlot]);

  if (!session) {
    return <p>Sign in to chat with the AI assistant.</p>;
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    const connection = connectionRef.current;
    const messageText = text.trim();
    if (!messageText || sending || !connection) {
      return;
    }

    setError(null);
    setSending(true);
    // Clear right away: invoke only resolves after the assistant has replied, which can take a while.
    setText("");
    try {
      // The server pushes both our message and the reply back through "ReceiveMessage".
      await sendChatMessage(connection, messageText);
    } catch (err) {
      // Give the text back so it isn't lost, unless they've already started typing something new.
      setText((current) => current || messageText);
      setError(err instanceof Error ? err.message : "Could not send message.");
    } finally {
      setSending(false);
    }
  }

  function selectSlot(startsAt: string) {
    setSelectedSlot(startsAt);
    // Suggest the patient's latest message as the reason; they can edit it before confirming.
    const lastPatientText = [...messages].reverse().find((message) => message.sender === "patient")?.text ?? "";
    setReason((current) => current || lastPatientText);
  }

  async function onConfirmSlot(event: FormEvent) {
    event.preventDefault();
    const connection = connectionRef.current;
    if (!connection || !bookingOptions || !selectedSlot || booking) {
      return;
    }

    setError(null);
    setBooking(true);
    try {
      // The server books it and posts the confirmation into the chat; it also hides the buttons.
      await bookSlot(connection, { doctorId: bookingOptions.doctorId, startsAt: selectedSlot, reason: reason.trim() });
      setReason("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not book that time.");
    } finally {
      setBooking(false);
    }
  }

  const selectedLabel = bookingOptions?.slots.find((slot) => slot.startsAt === selectedSlot)?.label;

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
        {assistantTyping ? (
          <p className="bubble typing" role="status" aria-label="Vaidya Mitra is typing">
            <span>Vaidya Mitra</span>
            <i className="dot" />
            <i className="dot" />
            <i className="dot" />
          </p>
        ) : null}
        {bookingOptions ? (
          <div className="slot-picker" role="group" aria-label={`Free times with ${bookingOptions.doctorName}`}>
            <p className="slot-picker-title">
              {bookingOptions.doctorName} · {bookingOptions.day}
            </p>
            <div className="slot-list">
              {bookingOptions.slots.map((slot) => (
                <button
                  key={slot.startsAt}
                  type="button"
                  className={slot.startsAt === selectedSlot ? "slot selected" : "slot"}
                  aria-pressed={slot.startsAt === selectedSlot}
                  onClick={() => selectSlot(slot.startsAt)}
                >
                  {slot.label}
                </button>
              ))}
            </div>
            {selectedSlot ? (
              <form className="slot-confirm" onSubmit={onConfirmSlot}>
                <label>
                  Reason for the visit
                  <input value={reason} onChange={(event) => setReason(event.target.value)} required />
                </label>
                <button type="submit" disabled={booking || reason.trim().length < 3}>
                  {booking ? "Booking..." : `Confirm ${selectedLabel}`}
                </button>
              </form>
            ) : null}
          </div>
        ) : null}
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
