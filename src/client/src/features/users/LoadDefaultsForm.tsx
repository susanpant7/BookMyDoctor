import { useState, type FormEvent } from "react";
import { loadDefaultPatients, type LoadDefaultsResult } from "@/features/users/usersApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import { TextField } from "@/shared/components/Field";
import Modal from "@/shared/components/Modal";

type LoadDefaultsFormProps = {
  onLoaded: (result: LoadDefaultsResult) => void;
  onCancel: () => void;
};

export default function LoadDefaultsForm({ onLoaded, onCancel }: LoadDefaultsFormProps) {
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError("");
    setSubmitting(true);
    try {
      onLoaded(await loadDefaultPatients(password));
    } catch (err) {
      setError(errorMessage(err, "Could not load the demo patients. Please try again."));
      setSubmitting(false);
    }
  }

  return (
    <Modal title="Load demo patients" onClose={onCancel}>
      <form onSubmit={handleSubmit} className="space-y-5">
        <p className="text-sm leading-6 text-slate-600">
          This adds two demo patients (Hari Bahadur, hb@gmail.com, and Madan Bahadur, mb@gmail.com). A patient that already
          exists is skipped. Enter the password both of them will sign in with.
        </p>

        <TextField
          label="Password for both patients"
          type="password"
          required
          minLength={6}
          autoComplete="new-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />

        {error && <Alert message={error} />}

        <div className="flex justify-end gap-3 pt-1">
          <Button variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting || password.length < 6}>
            {submitting ? "Loading..." : "Load patients"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
