import { Stethoscope } from "lucide-react";
import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "@/features/auth/useAuth";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import { TextField } from "@/shared/components/Field";

export default function LoginPage() {
  const { login, register } = useAuth();
  const navigate = useNavigate();
  const [registering, setRegistering] = useState(false);
  const [name, setName] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError("");
    setSubmitting(true);
    try {
      const user = registering ? await register(name, username, password) : await login(username, password);
      if (user) {
        navigate(user.role === "Admin" ? "/users" : "/doctors", { replace: true });
      } else {
        setError("Invalid email, username or password");
      }
    } catch (err) {
      setError(errorMessage(err, "Something went wrong. Please try again."));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="flex min-h-[calc(100vh-4rem)] items-center justify-center px-6 py-12">
      <div className="w-full max-w-sm">
        <div className="text-center">
          <span className="mx-auto flex size-12 items-center justify-center rounded-xl bg-indigo-600 text-white shadow-sm">
            <Stethoscope className="size-7" aria-hidden="true" />
          </span>
          <h1 className="mt-6 text-2xl font-bold tracking-tight text-slate-900">
            {registering ? "Create your account" : "Welcome back"}
          </h1>
          <p className="mt-2 text-sm text-slate-500">
            {registering ? "Register as a patient to book doctors" : "Sign in to your BookMyDoctor account"}
          </p>
        </div>

        <form
          onSubmit={handleSubmit}
          className="mt-8 space-y-5 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"
        >
          {registering && (
            <TextField label="Name" required autoComplete="name" value={name} onChange={(e) => setName(e.target.value)} />
          )}
          <TextField
            label={registering ? "Email" : "Email or admin username"}
            type={registering ? "email" : "text"}
            required
            autoComplete="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
          />
          <TextField
            label="Password"
            type="password"
            required
            autoComplete={registering ? "new-password" : "current-password"}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />

          {error && <Alert message={error} />}

          <Button type="submit" disabled={submitting} className="w-full">
            {submitting ? "Please wait..." : registering ? "Register" : "Sign in"}
          </Button>
        </form>

        <p className="mt-6 text-center text-sm text-slate-500">
          {registering ? "Already have an account?" : "New patient?"}{" "}
          <button
            type="button"
            className="font-semibold text-indigo-600 hover:text-indigo-500"
            onClick={() => {
              setRegistering(!registering);
              setError("");
            }}
          >
            {registering ? "Sign in" : "Create an account"}
          </button>
        </p>
      </div>
    </main>
  );
}
