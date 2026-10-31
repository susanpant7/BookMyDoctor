import { useState, type FormEvent } from "react";
import { updateSetting, type Setting } from "@/features/settings/settingsApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import { SelectField, TextAreaField, TextField } from "@/shared/components/Field";
import Modal from "@/shared/components/Modal";

type SettingFormProps = {
  setting: Setting;
  onSaved: () => void;
  onCancel: () => void;
};

export default function SettingForm({ setting, onSaved, onCancel }: SettingFormProps) {
  const [value, setValue] = useState(setting.value);
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const isEmbeddingModel = setting.key === "embedding.model";

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError("");
    setSubmitting(true);
    try {
      await updateSetting(setting.key, value);
      onSaved();
    } catch (err) {
      setError(errorMessage(err, "Could not save the setting. Please try again."));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal title={setting.label} onClose={onCancel} maxWidth={setting.kind === "longtext" ? "max-w-4xl" : "max-w-xl"}>
      <form onSubmit={handleSubmit} className="space-y-5">
        <p className="text-sm leading-6 text-stone-600">{setting.description}</p>
        {setting.kind === "longtext" ? (
          <TextAreaField
            label="Value"
            required
            rows={20}
            spellCheck={false}
            className="font-mono leading-6"
            value={value}
            onChange={(e) => setValue(e.target.value)}
          />
        ) : setting.kind === "boolean" ? (
          <SelectField label="Value" value={value} onChange={(e) => setValue(e.target.value)}>
            <option value="true">On</option>
            <option value="false">Off</option>
          </SelectField>
        ) : (
          <TextField
            label="Value"
            type={setting.kind === "text" ? "text" : "number"}
            step={setting.kind === "number" ? "any" : undefined}
            value={value}
            onChange={(e) => setValue(e.target.value)}
          />
        )}
        <p className="text-sm text-stone-500">
          Default:{" "}
          <span className="font-mono">
            {setting.kind === "longtext"
              ? "the original text"
              : setting.kind === "boolean"
                ? setting.defaultValue === "true" ? "On" : "Off"
                : setting.defaultValue === "" ? "(empty)" : setting.defaultValue}
          </span>
          {value !== setting.defaultValue && (
            <button type="button" className="ml-3 font-semibold text-brand-600" onClick={() => setValue(setting.defaultValue)}>
              Use default
            </button>
          )}
        </p>

        {isEmbeddingModel && (
          <Alert
            variant="warning"
            message="Changing the embedding model makes the stored knowledge unsearchable until you upload the knowledge files again and click Sync settings on the Knowledge page. Vectors made by different models cannot be compared."
          />
        )}

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
