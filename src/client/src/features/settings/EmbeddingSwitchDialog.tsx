import Button from "@/shared/components/Button";
import Modal from "@/shared/components/Modal";

// What the dialog shows: the question, the work going on (with how far it is), or how it ended.
export type EmbeddingSwitch =
  | { state: "confirm"; model: string }
  | { state: "running"; model: string; step: string; done: number; total: number }
  | { state: "finished"; model: string; ok: boolean; message: string };

type EmbeddingSwitchDialogProps = {
  value: EmbeddingSwitch;
  onConfirm: () => void;
  // Cancels the question, or closes the dialog after it ended. Not available while the work runs.
  onClose: () => void;
};

// The dialog for changing the embedding model. It stays open from the question to the end: while the stored knowledge
// is embedded again it shows how many sections are done, and when it is over it says what happened. It can't be closed
// while it works.
export default function EmbeddingSwitchDialog({ value, onConfirm, onClose }: EmbeddingSwitchDialogProps) {
  const running = value.state === "running";
  const percent = value.state === "running" && value.total > 0 ? Math.round((value.done / value.total) * 100) : 0;

  return (
    <Modal
      title={value.state === "confirm" ? "Clean and reinsert the knowledge?" : `Embedding model: ${value.model}`}
      onClose={onClose}
      maxWidth="max-w-md"
      dismissible={!running}
    >
      {value.state === "confirm" && (
        <>
          <p className="text-sm leading-6 text-stone-600">
            The stored knowledge will be cleaned and reinserted with <span className="font-mono">{value.model}</span>: every
            section is embedded again, because vectors from different models can't be compared. This can take a minute. If
            it fails (for example a wrong key), nothing is changed.
          </p>
          <div className="mt-6 flex justify-end gap-3">
            <Button variant="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button onClick={onConfirm}>Clean, reinsert and use</Button>
          </div>
        </>
      )}

      {value.state === "running" && (
        <div aria-live="polite">
          <p className="text-sm font-medium text-stone-800">{value.step}</p>
          <div
            className="mt-3 h-3 overflow-hidden rounded-full bg-stone-100"
            role="progressbar"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={value.total > 0 ? percent : undefined}
          >
            <div
              className={`h-full rounded-full bg-brand-600 transition-all duration-300 ${value.total === 0 ? "w-1/3 animate-pulse" : ""}`}
              style={value.total > 0 ? { width: `${percent}%` } : undefined}
            />
          </div>
          <p className="mt-2 text-sm text-stone-500">
            {value.total > 0 ? `${value.done} of ${value.total} (${percent}%)` : "Starting..."}
          </p>
          <p className="mt-4 text-sm text-stone-500">Please keep this page open. When it is over, this shows the result.</p>
        </div>
      )}

      {value.state === "finished" && (
        <>
          <p
            className={`rounded-lg px-3 py-2 text-sm ring-1 ring-inset ${
              value.ok ? "bg-emerald-50 text-emerald-700 ring-emerald-200" : "bg-red-50 text-red-700 ring-red-200"
            }`}
          >
            {value.message}
          </p>
          <div className="mt-6 flex justify-end">
            <Button onClick={onClose}>Close</Button>
          </div>
        </>
      )}
    </Modal>
  );
}
