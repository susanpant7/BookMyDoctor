import { Bot } from "lucide-react";
import { Link } from "react-router-dom";
import { useSession } from "../../shared/session";

export function BookPage() {
  const { session } = useSession();

  if (!session) {
    return <p>Sign in to book an appointment.</p>;
  }

  return (
    <section className="panel book-assist">
      <h1>Book</h1>
      <Link to="/book/chat" className="assist-card">
        <Bot size={28} aria-hidden="true" />
        <span>
          <strong>Don&apos;t know which doctor to book?</strong>
          <span className="muted">Ask your Vaidya Mitra</span>
        </span>
      </Link>
    </section>
  );
}
