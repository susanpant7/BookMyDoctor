import { useState, type FormEvent } from "react";
import { addAiModel, updateAiModel, type AiModel, type AiModelKind, type SaveAiModelRequest } from "@/features/settings/settingsApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import { TextAreaField, TextField } from "@/shared/components/Field";
import Modal from "@/shared/components/Modal";

type ModelFormProps = {
  kind: AiModelKind;
  // The saved model being edited, or null to add one.
  model: AiModel | null;
  // The starting values when adding: the model in use now, or the service a copy is made of.
  initial: SaveAiModelRequest;
  // A title when it is not the plain add or edit.
  title?: string;
  // Given for the model in use: the form then has no name or notes, and its button hands the values over to be
  // tested and used (nothing is saved to the list).
  onUse?: (values: SaveAiModelRequest) => void;
  onSaved: (saved: AiModel) => void;
  onCancel: () => void;
};

// Adds or edits a chat or embedding model: where it runs, its name there and (for embeddings) its prefixes.
export default function ModelForm({ kind, model, initial, title, onUse, onSaved, onCancel }: ModelFormProps) {
  const [values, setValues] = useState<SaveAiModelRequest>(model ?? initial);
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const isEmbedding = kind === "embedding";

  const change = (changes: Partial<SaveAiModelRequest>) => setValues({ ...values, ...changes });

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (onUse) {
      onUse({ ...values, kind });
      return;
    }
    setError("");
    setSubmitting(true);
    try {
      const request = { ...values, kind };
      onSaved(model ? await updateAiModel(model.id, request) : await addAiModel(request));
    } catch (err) {
      setError(errorMessage(err, "Could not save the model. Please try again."));
      setSubmitting(false);
    }
  }

  const what = isEmbedding ? "embedding model" : "chat model";

  return (
    <Modal title={title ?? (model ? `Edit ${model.name}` : `Add a ${what}`)} onClose={onCancel} maxWidth="max-w-xl">
      <form onSubmit={handleSubmit} className="space-y-5">
        {!onUse && (
          <TextField
            label="Name"
            required
            maxLength={100}
            placeholder={isEmbedding ? "Google Gemini Embedding" : "Google Gemini Flash"}
            value={values.name}
            onChange={(e) => change({ name: e.target.value })}
          />
        )}
        <TextField
          label="API address"
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
          placeholder={isEmbedding ? "gemini-embedding-001" : "gemini-flash-latest"}
          value={values.model}
          onChange={(e) => change({ model: e.target.value })}
        />
        <TextField
          label="API key name"
          maxLength={100}
          placeholder="GEMINI_API_KEY"
          value={values.apiKeyName}
          onChange={(e) => change({ apiKeyName: e.target.value })}
        />
        <p className="-mt-3 text-sm text-stone-500">
          The name of the variable in the server's <span className="font-mono">.env</span> file that holds this service's
          key (it must end in <span className="font-mono">API_KEY</span>). Leave it empty for Ollama. Only the name is saved,
          never the key.
        </p>
        {isEmbedding && (
          <div className="grid gap-5 sm:grid-cols-2">
            <TextField
              label="Query prefix"
              maxLength={100}
              placeholder="(none)"
              value={values.queryPrefix}
              onChange={(e) => change({ queryPrefix: e.target.value })}
            />
            <TextField
              label="Document prefix"
              maxLength={100}
              placeholder="(none)"
              value={values.documentPrefix}
              onChange={(e) => change({ documentPrefix: e.target.value })}
            />
          </div>
        )}
        {!onUse && (
          <TextAreaField
            label="Notes"
            rows={3}
            maxLength={500}
            placeholder="Free tier, with a daily limit. Key: https://aistudio.google.com/apikey"
            value={values.notes}
            onChange={(e) => change({ notes: e.target.value })}
          />
        )}
        <p className="text-sm text-stone-500">
          An address with no path (http://localhost:11434) is Ollama. Any other address is an online service in the OpenAI
          format.
          {isEmbedding &&
            " The prefixes are put before each question and each knowledge section; only some models need them (nomic-embed-text: search_query: and search_document:)."}
          {onUse &&
            (isEmbedding
              ? " All the knowledge is embedded again with the new model; it is only used when that works."
              : " The model is tested with a short question first; it is only used when it answers.")}
        </p>

        {error && <Alert message={error} />}

        <div className="flex justify-end gap-3 pt-1">
          <Button variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {onUse ? (isEmbedding ? "Convert and use" : "Test and use") : submitting ? "Saving..." : "Save"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
