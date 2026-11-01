import { useState, type FormEvent } from "react";
import { addChatModel, updateChatModel, type SaveChatModelRequest, type SavedChatModel } from "@/features/settings/settingsApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import { TextAreaField, TextField } from "@/shared/components/Field";
import Modal from "@/shared/components/Modal";

type ChatModelFormProps = {
  // The saved chat model being edited, or null to add one.
  chatModel: SavedChatModel | null;
  // The starting values when adding: the chat model in use now.
  initial: SaveChatModelRequest;
  onSaved: () => void;
  onCancel: () => void;
};

// Saves a chat model (a Chat API address and a model) to switch to later with "Use".
export default function ChatModelForm({ chatModel, initial, onSaved, onCancel }: ChatModelFormProps) {
  const [values, setValues] = useState<SaveChatModelRequest>(chatModel ?? initial);
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const change = (changes: Partial<SaveChatModelRequest>) => setValues({ ...values, ...changes });

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError("");
    setSubmitting(true);
    try {
      if (chatModel) await updateChatModel(chatModel.id, values);
      else await addChatModel(values);
      onSaved();
    } catch (err) {
      setError(errorMessage(err, "Could not save the chat model. Please try again."));
      setSubmitting(false);
    }
  }

  return (
    <Modal title={chatModel ? `Edit ${chatModel.name}` : "Add a chat model"} onClose={onCancel} maxWidth="max-w-xl">
      <form onSubmit={handleSubmit} className="space-y-5">
        <TextField
          label="Name"
          required
          maxLength={100}
          placeholder="Gemini Flash (free)"
          value={values.name}
          onChange={(e) => change({ name: e.target.value })}
        />
        <TextField
          label="Chat API address"
          required
          maxLength={200}
          placeholder="https://generativelanguage.googleapis.com/v1beta/openai"
          value={values.baseUrl}
          onChange={(e) => change({ baseUrl: e.target.value })}
        />
        <TextField
          label="Model"
          required
          maxLength={200}
          placeholder="gemini-flash-latest"
          value={values.model}
          onChange={(e) => change({ model: e.target.value })}
        />
        <TextAreaField
          label="Notes"
          rows={3}
          maxLength={500}
          placeholder="Free tier, 250 requests a day. Key from aistudio.google.com."
          value={values.notes}
          onChange={(e) => change({ notes: e.target.value })}
        />
        <p className="text-sm text-stone-500">
          An address with no path (http://localhost:11434) is Ollama. For an online service, its key goes in AI_API_KEY in the
          server's .env file when you switch to it.
        </p>

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
