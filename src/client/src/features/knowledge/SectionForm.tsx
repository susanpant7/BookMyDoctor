import { useState, type FormEvent } from "react";
import type { KnowledgeSection } from "@/features/knowledge/knowledgeApi";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import { TextAreaField, TextField } from "@/shared/components/Field";
import Modal from "@/shared/components/Modal";

type SectionFormProps = {
  documentTitle: string;
  // The section being edited, or null to add one.
  section: KnowledgeSection | null;
  // Saves the section into its file; rejects with a message to show.
  onSave: (section: KnowledgeSection) => Promise<void>;
  onCancel: () => void;
};

// Adds one section to a knowledge file, or edits one.
export default function SectionForm({ documentTitle, section, onSave, onCancel }: SectionFormProps) {
  const [title, setTitle] = useState(section?.title ?? "");
  const [text, setText] = useState(section?.text ?? "");
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError("");
    setSaving(true);
    try {
      await onSave({ title: title.trim(), text: text.trim() });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save the section. Please try again.");
      setSaving(false);
    }
  }

  return (
    <Modal title={section ? `Edit section in ${documentTitle}` : `Add a section to ${documentTitle}`} onClose={onCancel} maxWidth="max-w-2xl">
      <form onSubmit={handleSubmit} className="space-y-5">
        <TextField label="Section name" required maxLength={150} value={title} onChange={(e) => setTitle(e.target.value)} />
        <TextAreaField label="Details" required rows={8} value={text} onChange={(e) => setText(e.target.value)} />

        {error && <Alert message={error} />}

        <div className="flex justify-end gap-3 pt-1">
          <Button variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" disabled={saving || title.trim().length === 0 || text.trim().length === 0}>
            {saving ? "Saving..." : "Save section"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
