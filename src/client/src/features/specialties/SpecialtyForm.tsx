import { useState, type FormEvent } from "react";
import { createSpecialty, updateSpecialty, type Specialty } from "@/features/specialties/specialtiesApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import { TextAreaField, TextField } from "@/shared/components/Field";
import Modal from "@/shared/components/Modal";

type SpecialtyFormProps = {
  specialty: Specialty | null;
  onSaved: () => void;
  onCancel: () => void;
};

export default function SpecialtyForm({ specialty, onSaved, onCancel }: SpecialtyFormProps) {
  const [name, setName] = useState(specialty?.name ?? "");
  const [description, setDescription] = useState(specialty?.description ?? "");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError("");
    setSubmitting(true);
    try {
      const input = { name, description };
      if (specialty) await updateSpecialty(specialty.id, input);
      else await createSpecialty(input);
      onSaved();
    } catch (err) {
      setError(errorMessage(err, "Something went wrong. Please try again."));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal title={specialty ? "Edit specialty" : "Add specialty"} onClose={onCancel}>
      <form onSubmit={handleSubmit} className="space-y-5">
        <TextField label="Name" required maxLength={100} value={name} onChange={(e) => setName(e.target.value)} />
        <TextAreaField
          label="Description"
          rows={3}
          maxLength={500}
          placeholder="What this specialty treats, in a few words"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
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
