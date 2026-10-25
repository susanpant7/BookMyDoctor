import { FormEvent, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useSession } from "../../shared/session";
import { login, register } from "./authApi";

type Mode = "login" | "register";

export function AuthPage() {
  const navigate = useNavigate();
  const { setSession } = useSession();
  const [mode, setMode] = useState<Mode>("login");
  const [name, setName] = useState("");
  const [email, setEmail] = useState("hb@gmail.com");
  const [password, setPassword] = useState("susan123");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setPending(true);
    setError(null);
    try {
      const user =
        mode === "login" ? await login(email, password) : await register(name, email, password);
      setSession(user);
      navigate("/doctors");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong.");
    } finally {
      setPending(false);
    }
  }

  return (
    <section className="panel">
      <h1>{mode === "login" ? "Sign in" : "Create account"}</h1>
      <p className="muted">Demo login: hb@gmail.com / susan123</p>
      <form onSubmit={onSubmit} className="stack">
        {mode === "register" ? (
          <label>
            Name
            <input value={name} onChange={(event) => setName(event.target.value)} required />
          </label>
        ) : null}
        <label>
          Email
          <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
        </label>
        <label>
          Password
          <input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            required
          />
        </label>
        {error ? <p className="error">{error}</p> : null}
        <button type="submit" disabled={pending}>
          {pending ? "Please wait..." : mode === "login" ? "Sign in" : "Register"}
        </button>
      </form>
      <button
        type="button"
        className="link"
        onClick={() => setMode(mode === "login" ? "register" : "login")}
      >
        {mode === "login" ? "Need an account?" : "Already have an account?"}
      </button>
    </section>
  );
}
