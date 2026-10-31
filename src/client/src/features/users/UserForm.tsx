import { useState, type FormEvent } from "react";
import { createPatient, updatePatient, type Patient } from "@/features/users/usersApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import { TextField } from "@/shared/components/Field";
import Modal from "@/shared/components/Modal";

type UserFormProps = {
  patient: Patient | null;
  onSaved: () => void;
  onCancel: () => void;
};

export default function UserForm({ patient, onSaved, onCancel }: UserFormProps) {
  const isEdit = patient !== null;
  const [name, setName] = useState(patient?.name ?? "");
  const [email, setEmail] = useState(patient?.email ?? "");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError("");
    setSubmitting(true);
    try {
      if (patient) await updatePatient(patient.id, { name, email, password: password || undefined });
      else await createPatient({ name, email, password });
      onSaved();
    } catch (err) {
      setError(errorMessage(err, "Something went wrong. Please try again."));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal title={isEdit ? "Edit patient" : "Add patient"} onClose={onCancel}>
      <form onSubmit={handleSubmit} className="space-y-5">
        <TextField label="Full name" required maxLength={100} value={name} onChange={(e) => setName(e.target.value)} />
        <TextField label="Email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        <TextField
          label={isEdit ? "New password" : "Password"}
          type="password"
          required={!isEdit}
          autoComplete="new-password"
          placeholder={isEdit ? "Leave blank to keep" : "Any password, e.g. user"}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />

        {error && <Alert message={error} />}

        <div className="flex justify-end gap-3 pt-1">
          <Button variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? "Saving..." : "Save"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
