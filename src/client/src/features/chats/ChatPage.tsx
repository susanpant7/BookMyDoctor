import type { HubConnection } from "@microsoft/signalr";
import { ArrowLeft, Bot, Send } from "lucide-react";
import { useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import { Link } from "react-router-dom";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import { TextField } from "@/shared/components/Field";
import { useAuth } from "@/features/auth/useAuth";
import { getMessages, type ChatMessage } from "@/features/chats/chatApi";
import {
  bookSlot,
  createChatConnection,
  joinChat,
  onAssistantDraft,
  onAssistantTyping,
  onBookingOptions,
  onReceiveMessage,
  sendChatMessage,
  type BookingOptions
} from "@/features/chats/chatHub";

// Adds messages that aren't already in the list (history and live pushes can overlap).
function mergeMessages(current: ChatMessage[], incoming: ChatMessage[]) {
  const known = new Set(current.map((message) => message.id));
  return [...current, ...incoming.filter((message) => !known.has(message.id))];
}

import { getToken } from "@/shared/api/token";

export default function ChatPage() {
  const { user } = useAuth();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [text, setText] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const [connected, setConnected] = useState(false);
  const [assistantTyping, setAssistantTyping] = useState(false);
  const [draft, setDraft] = useState<string | null>(null);
  const [bookingOptions, setBookingOptions] = useState<BookingOptions | null>(null);
  const [selectedSlot, setSelectedSlot] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [booking, setBooking] = useState(false);
  const threadRef = useRef<HTMLDivElement>(null);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    if (!user) {
      return;
    }

    const connection = createChatConnection(() => getToken() ?? "");
    connectionRef.current = connection;
    let stopped = false;

    onReceiveMessage(connection, (message) => {
      setMessages((current) => mergeMessages(current, [message]));
      // The final assistant reply replaces the draft that was shown while it was written.
      if (message.sender === "assistant") {
        setDraft(null);
      }
    });
    onAssistantTyping(connection, setAssistantTyping);
    onAssistantDraft(connection, setDraft);
    onBookingOptions(connection, (options) => {
      setBookingOptions(options);
      setSelectedSlot(null);
    });

    // A reconnect gets a new connection id, so the server-side group membership must be renewed.
    // A dropped connection may also miss the "typing stopped" push, so clear the indicator.
    connection.onreconnecting(() => {
      setConnected(false);
      setAssistantTyping(false);
      setDraft(null);
    });
    connection.onreconnected(() => {
      joinChat(connection).then(() => setConnected(true));
    });
    connection.onclose(() => {
      setConnected(false);
      setAssistantTyping(false);
      setDraft(null);
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
  }, [user?.userId]);

  useEffect(() => {
    threadRef.current?.scrollTo({ top: threadRef.current.scrollHeight });
  }, [messages, assistantTyping, draft, bookingOptions, selectedSlot]);

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
    <main className="mx-auto flex h-[calc(100vh-4rem)] max-w-3xl flex-col px-4 py-6">
      <header className="flex items-center gap-3 rounded-t-2xl border border-stone-200 bg-white px-4 py-3">
        <Link
          to="/book"
          aria-label="Back to book"
          className="rounded-lg p-2 text-stone-500 hover:bg-stone-100 hover:text-stone-700"
        >
          <ArrowLeft className="size-5" aria-hidden="true" />
        </Link>
        <span className="flex size-9 items-center justify-center rounded-full bg-brand-600 text-white">
          <Bot className="size-5" aria-hidden="true" />
        </span>
        <div className="leading-tight">
          <h1 className="font-semibold text-stone-900">Vaidya Mitra</h1>
          <p className="text-xs text-stone-500">{connected ? "Online" : "Connecting..."}</p>
        </div>
      </header>

      <div
        ref={threadRef}
        className="flex-1 space-y-3 overflow-y-auto border-x border-stone-200 bg-stone-50 px-4 py-5"
      >
        {messages.length === 0 && <p className="text-center text-sm text-stone-500">How can I help you today?</p>}
        {messages.map((message) => (
          <Bubble key={message.id} mine={message.sender === "patient"} name={message.sender === "patient" ? "You" : "Vaidya Mitra"}>
            {message.text}
          </Bubble>
        ))}
        {draft ? (
          // Shown while the reply is being written; replaced by the final, checked reply.
          <Bubble mine={false} name="Vaidya Mitra" busy>
            {draft}
          </Bubble>
        ) : assistantTyping ? (
          <Bubble mine={false} name="Vaidya Mitra" label="Vaidya Mitra is typing">
            <span className="typing-dot" />
            <span className="typing-dot" />
            <span className="typing-dot" />
          </Bubble>
        ) : null}
        {bookingOptions && (
          <div
            role="group"
            aria-label={`Free times with ${bookingOptions.doctorName}`}
            className="rounded-2xl border border-brand-200 bg-white p-4 shadow-sm"
          >
            <p className="text-sm font-semibold text-stone-900">
              {bookingOptions.doctorName} · {bookingOptions.day}
            </p>
            <div className="mt-3 flex flex-wrap gap-2">
              {bookingOptions.slots.map((slot) => (
                <button
                  key={slot.startsAt}
                  type="button"
                  aria-pressed={slot.startsAt === selectedSlot}
                  onClick={() => selectSlot(slot.startsAt)}
                  className={`rounded-lg px-3 py-1.5 text-sm font-medium ring-1 ring-inset transition ${
                    slot.startsAt === selectedSlot
                      ? "bg-brand-600 text-white ring-brand-600"
                      : "bg-white text-stone-700 ring-stone-300 hover:bg-stone-50"
                  }`}
                >
                  {slot.label}
                </button>
              ))}
            </div>
            {selectedSlot && (
              <form className="mt-4 space-y-3" onSubmit={onConfirmSlot}>
                <TextField label="Reason for the visit" value={reason} onChange={(event) => setReason(event.target.value)} required />
                <Button type="submit" disabled={booking || reason.trim().length < 3}>
                  {booking ? "Booking..." : `Confirm ${selectedLabel}`}
                </Button>
              </form>
            )}
          </div>
        )}
      </div>

      <form onSubmit={onSubmit} className="flex gap-3 rounded-b-2xl border border-stone-200 bg-white p-3">
        <input
          value={text}
          onChange={(event) => setText(event.target.value)}
          placeholder="Write a message"
          aria-label="Message"
          className="block w-full rounded-lg border-0 bg-white px-3 py-2.5 text-sm text-stone-900 shadow-sm ring-1 ring-inset ring-stone-300 placeholder:text-stone-400 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-brand-600"
        />
        <Button type="submit" disabled={sending || !connected || !text.trim()} aria-label="Send">
          <Send className="size-4" aria-hidden="true" />
          Send
        </Button>
      </form>
      {error && (
        <div className="mt-3">
          <Alert message={error} />
        </div>
      )}
    </main>
  );
}

function Bubble({
  mine,
  name,
  busy,
  label,
  children
}: {
  mine: boolean;
  name: string;
  busy?: boolean;
  label?: string;
  children: ReactNode;
}) {
  return (
    <div className={`flex ${mine ? "justify-end" : "justify-start"}`}>
      <p
        aria-busy={busy}
        aria-label={label}
        role={label ? "status" : undefined}
        className={`max-w-[85%] whitespace-pre-wrap rounded-2xl px-4 py-2.5 text-sm leading-6 ${
          mine ? "rounded-br-sm bg-brand-600 text-white" : "rounded-bl-sm bg-white text-stone-800 shadow-sm ring-1 ring-stone-200"
        }`}
      >
        <span className={`block text-xs font-semibold ${mine ? "text-brand-200" : "text-stone-400"}`}>{name}</span>
        {children}
      </p>
    </div>
  );
}
