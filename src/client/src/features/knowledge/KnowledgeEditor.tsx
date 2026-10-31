import { useState, type FormEvent } from "react";
import { Plus, Trash2 } from "lucide-react";
import { saveKnowledgeDocument, type KnowledgeSection, type UploadFileResult } from "@/features/knowledge/knowledgeApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import { TextAreaField, TextField } from "@/shared/components/Field";
import Modal from "@/shared/components/Modal";

type KnowledgeEditorProps = {
  // The file being edited, or null for a new one.
  fileName: string | null;
  initialTitle: string;
  initialSections: KnowledgeSection[];
  onSaved: (result: UploadFileResult) => void;
  onCancel: () => void;
};

const empty: KnowledgeSection = { title: "", text: "" };

// Writes a knowledge file as a title and any number of sections. Each section is stored and searched on its own.
export default function KnowledgeEditor({ fileName, initialTitle, initialSections, onSaved, onCancel }: KnowledgeEditorProps) {
  const [title, setTitle] = useState(initialTitle);
  const [sections, setSections] = useState<KnowledgeSection[]>(initialSections.length > 0 ? initialSections : [empty]);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  const change = (index: number, changes: Partial<KnowledgeSection>) =>
    setSections(sections.map((section, i) => (i === index ? { ...section, ...changes } : section)));

  const complete = title.trim().length > 0 && sections.every((s) => s.title.trim().length > 0 && s.text.trim().length > 0);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError("");
    setSaving(true);
    try {
      const result = await saveKnowledgeDocument({ fileName, title: title.trim(), sections });
      if (result.error) {
        setError(result.error);
        setSaving(false);
        return;
      }
      onSaved(result);
    } catch (err) {
      setError(errorMessage(err, "Could not save the knowledge. Please try again."));
      setSaving(false);
    }
  }

  return (
    <Modal title={fileName ? `Edit ${initialTitle}` : "Write knowledge"} onClose={onCancel} maxWidth="max-w-3xl">
      <form onSubmit={handleSubmit} className="space-y-5">
        <TextField
          label="Title"
          required
          maxLength={100}
          placeholder="Visiting the hospital"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
        />

        <div className="space-y-4">
          {sections.map((section, index) => (
            <fieldset key={index} className="space-y-3 rounded-xl bg-stone-50 p-4 ring-1 ring-inset ring-stone-200">
              <div className="flex items-end gap-3">
                <div className="min-w-0 flex-1">
                  <TextField
                    label={`Section ${index + 1} name`}
                    required
                    maxLength={150}
                    placeholder="Visiting hours"
                    value={section.title}
                    onChange={(e) => change(index, { title: e.target.value })}
                  />
                </div>
                {sections.length > 1 && (
                  <Button
                    variant="dangerGhost"
                    size="sm"
                    aria-label={`Remove section ${index + 1}`}
                    onClick={() => setSections(sections.filter((_, i) => i !== index))}
                  >
                    <Trash2 className="size-4" />
                  </Button>
                )}
              </div>
              <TextAreaField
                label="Details"
                required
                rows={5}
                placeholder="Visitors are welcome from 10:00 to 20:00 every day, two per patient at a time."
                value={section.text}
                onChange={(e) => change(index, { text: e.target.value })}
              />
            </fieldset>
          ))}
          <Button variant="secondary" size="sm" onClick={() => setSections([...sections, empty])}>
            <Plus className="size-4" />
            Add section
          </Button>
        </div>

        <p className="text-xs text-stone-500">
          The assistant finds each section on its own, so keep one topic per section and mention what it is about in the details.
        </p>

        {error && <Alert message={error} />}

        <div className="flex justify-end gap-3 pt-1">
          <Button variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" disabled={saving || !complete}>
            {saving ? "Saving..." : "Save"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
