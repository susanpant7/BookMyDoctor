import { Send } from "lucide-react";
import { FormEvent, useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useSession } from "../../shared/session";
import { getMessages, sendMessage, type ChatMessage } from "../chats/chatApi";
import { getDoctors, type Doctor } from "../doctors/doctorApi";

export function BookPage() {
  const { session } = useSession();
  const [params] = useSearchParams();
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [doctorId, setDoctorId] = useState(params.get("doctorId") ?? "");
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [text, setText] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const threadRef = useRef<HTMLDivElement>(null);

  const selectedDoctor = doctors.find((doctor) => doctor.id === doctorId);

  useEffect(() => {
    getDoctors()
      .then((nextDoctors) => {
        setDoctors(nextDoctors);
        if (!doctorId && nextDoctors[0]) {
          setDoctorId(nextDoctors[0].id);
        }
      })
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "Could not load doctors."));
  }, []);

  useEffect(() => {
    if (!session || !doctorId) {
      return;
    }
    getMessages(doctorId, session.userId)
      .then(setMessages)
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "Could not load chat."));
  }, [doctorId, session?.userId]);

  useEffect(() => {
    threadRef.current?.scrollTo({ top: threadRef.current.scrollHeight });
  }, [messages]);

  if (!session) {
    return <p>Sign in to chat with a doctor.</p>;
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
        doctorId,
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
        <div>
          <h1>Chat</h1>
          <p className="muted">{selectedDoctor?.specialty ?? "Choose a doctor"}</p>
        </div>
        <label>
          Doctor
          <select value={doctorId} onChange={(event) => setDoctorId(event.target.value)}>
            {doctors.map((doctor) => (
              <option key={doctor.id} value={doctor.id}>
                {doctor.name}
              </option>
            ))}
          </select>
        </label>
      </header>
      <div className="messages" ref={threadRef}>
        {messages.length === 0 ? (
          <p className="muted">No messages yet. Ask the doctor about booking a visit.</p>
        ) : null}
        {messages.map((message) => (
          <p key={message.id} className={message.sender === "patient" ? "bubble me" : "bubble"}>
            <span>{message.sender === "patient" ? "You" : selectedDoctor?.name ?? "Doctor"}</span>
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
