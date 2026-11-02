import { useEffect, useState } from "react";
import { formatTime } from "@/features/settings/formatTime";
import { getAvailableModels, testAiModel, withModel, type ModelChoice } from "@/features/settings/settingsApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import Modal from "@/shared/components/Modal";

type AvailableModelsDialogProps = {
  // The saved model or service whose models are listed: its address, key name and prefixes are used for every test.
  choice: ModelChoice & { name: string };
  // The model in use, when this address is the one in use.
  current: string;
  // True while a switch is running.
  busy: boolean;
  onSelect: (model: string) => void;
  onClose: () => void;
};

// The models a service offers for our key. Test sends one tiny request to a model (nothing is saved); Select uses it.
export default function AvailableModelsDialog({ choice, current, busy, onSelect, onClose }: AvailableModelsDialogProps) {
  const [models, setModels] = useState<string[] | null>(null);
  const [loadError, setLoadError] = useState("");
  // "testing" while the request runs, then how long the model took to answer. A failed test is shown in its own dialog.
  const [results, setResults] = useState<Record<string, "testing" | number>>({});
  const [failure, setFailure] = useState<{ model: string; message: string } | null>(null);

  useEffect(() => {
    let ignore = false;
    getAvailableModels(choice.kind, choice.baseUrl, choice.apiKeyName)
      .then((loaded) => !ignore && setModels(loaded.models))
      .catch((err) => !ignore && setLoadError(errorMessage(err, "Could not ask the service for its models.")));
    return () => {
      ignore = true;
    };
  }, [choice.kind, choice.baseUrl, choice.apiKeyName]);

  async function test(model: string) {
    setResults((r) => ({ ...r, [model]: "testing" }));
    try {
      const { milliseconds } = await testAiModel(withModel(choice, model));
      setResults((r) => ({ ...r, [model]: milliseconds }));
    } catch (err) {
      setResults(({ [model]: _removed, ...rest }) => rest);
      setFailure({ model, message: errorMessage(err, "The test failed.") });
    }
  }

  const testing = Object.values(results).includes("testing");
  // The model in use comes first, even when the service no longer lists it.
  const listed = models ?? [];
  const ordered = current ? [current, ...listed.filter((m) => m !== current)] : listed;

  return (
    <>
      <Modal title={`${choice.name}: ${choice.kind} models`} onClose={onClose} maxWidth="max-w-2xl">
        {loadError ? (
          <Alert message={loadError} />
        ) : models === null ? (
          <p className="text-sm text-stone-500">Asking the service which models this key can use...</p>
        ) : ordered.length === 0 ? (
          <p className="text-sm text-stone-500">The service did not list any models for this key.</p>
        ) : (
          <>
            <p className="text-sm text-stone-600">
              {listed.length} {listed.length === 1 ? "model" : "models"} listed{current ? ", the one in use first" : ""}. A
              service can list models this key can't use, so <span className="font-medium text-stone-800">Test</span> a model
              before you <span className="font-medium text-stone-800">Select</span> it.
            </p>
            <ul className="mt-3 max-h-96 divide-y divide-stone-100 overflow-y-auto rounded-xl ring-1 ring-stone-200">
              {ordered.map((name) => (
                <li key={name} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
                  <div className="min-w-0">
                    <span className="break-all font-mono text-sm">{name}</span>
                    {name === current && (
                      <span className="ml-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-semibold text-brand-700">In use</span>
                    )}
                    {name === current && !listed.includes(name) && (
                      <span className="ml-2 text-xs text-amber-700">not in the service's list</span>
                    )}
                    {typeof results[name] === "number" && (
                      <span className="ml-2 text-xs text-emerald-700">✓ answered in {formatTime(results[name])}</span>
                    )}
                    {results[name] === "testing" && <span className="ml-2 text-xs text-stone-500">Testing...</span>}
                  </div>
                  <div className="flex gap-2">
                    <Button variant="secondary" size="sm" disabled={testing || busy} onClick={() => test(name)}>
                      Test
                    </Button>
                    <Button size="sm" disabled={testing || busy || name === current} onClick={() => onSelect(name)}>
                      Select
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          </>
        )}
        <div className="mt-5 flex justify-end">
          <Button variant="secondary" onClick={onClose}>
            Close
          </Button>
        </div>
      </Modal>

      {failure && (
        <Modal title={`Test failed: ${failure.model}`} onClose={() => setFailure(null)} maxWidth="max-w-lg">
          <Alert message={failure.message} />
          <p className="mt-3 text-sm text-stone-500">The model in use was not changed. Pick another model, or check the key.</p>
          <div className="mt-5 flex justify-end">
            <Button onClick={() => setFailure(null)}>Close</Button>
          </div>
        </Modal>
      )}
    </>
  );
}
