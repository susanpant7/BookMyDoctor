import { useEffect, useRef, useState } from "react";
import { formatTime } from "@/features/settings/formatTime";
import { getAvailableModels, testAiModel, type ModelChoice } from "@/features/settings/settingsApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Button from "@/shared/components/Button";
import Modal from "@/shared/components/Modal";

// A service (or saved model) the tour goes through: its address and key name are used for every model of it.
export type TourProvider = ModelChoice & { key: string; name: string };

type ModelTourDialogProps = {
  providers: TourProvider[];
  // The model in use. "Next" starts after it; "from the beginning" starts at the first model of the first service.
  current: { baseUrl: string; model: string };
  start: "current" | "beginning";
  onSelect: (provider: TourProvider, model: string) => void;
  onClose: () => void;
};

type Status =
  | { kind: "working"; text: string }
  | { kind: "ready"; provider: TourProvider; model: string; ms: number }
  | { kind: "done" };

type LogLine = { id: number; ok: boolean; text: string };

const sameUrl = (a: string, b: string) => a.replace(/\/+$/, "") === b.replace(/\/+$/, "");

// A service allows only a few requests a minute on a free plan, so the tests are spaced out.
const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

// Goes through the chat models one after another: for each service we have a key for, each of its models. Every
// model is tested with a tiny request, and the dialog stops at the first one that answers, showing how long it took.
// Select uses it, Next goes on to the next one. A model that fails (an error, no answer) is skipped by itself, and
// the reason is kept in the list below.
export default function ModelTourDialog({ providers, current, start, onSelect, onClose }: ModelTourDialogProps) {
  const [status, setStatus] = useState<Status>({ kind: "working", text: "Starting..." });
  const [log, setLog] = useState<LogLine[]>([]);

  // Each run has its own token, so closing the dialog (or starting over) stops the run that is going on.
  const token = useRef({ cancelled: false });
  const lists = useRef<Record<string, string[]>>({});
  const cursor = useRef({ p: 0, m: -1 });
  const lineId = useRef(0);

  const addLog = (ok: boolean, text: string) => setLog((lines) => [{ id: lineId.current++, ok, text }, ...lines]);

  // The models of a service, listed once. A service that can't be listed (a wrong key, not running) is skipped.
  async function listOf(provider: TourProvider) {
    if (lists.current[provider.key]) return lists.current[provider.key];
    try {
      const { models } = await getAvailableModels("chat", provider.baseUrl, provider.apiKeyName);

      // The model the service is listed with first, then the others, newest names first: the old ones are the ones a
      // service retires.
      const ordered = [...models].sort((a, b) => b.localeCompare(a, undefined, { numeric: true }));
      const preferred = ordered.indexOf(provider.model);
      if (preferred > 0) ordered.unshift(...ordered.splice(preferred, 1));

      lists.current[provider.key] = ordered;
      return ordered;
    } catch (err) {
      addLog(false, `${provider.name}: skipped, its models could not be listed. ${errorMessage(err, "")}`.trim());
      lists.current[provider.key] = [];
      return [];
    }
  }

  // Goes on from the model after (p, m) until one answers, or there are no more. A failing model is skipped by itself.
  async function advance(from: { p: number; m: number }, run: { cancelled: boolean }) {
    let { p, m } = from;
    let waited = false;
    let first = true;
    while (!run.cancelled) {
      if (p >= providers.length) {
        setStatus({ kind: "done" });
        return;
      }

      const provider = providers[p];
      setStatus({ kind: "working", text: `${provider.name}: listing the models...` });
      const models = await listOf(provider);
      if (run.cancelled) return;

      m++;
      if (m >= models.length) {
        p++;
        m = -1;
        continue;
      }

      const model = models[m];
      if (!first) await sleep(800);
      first = false;
      if (run.cancelled) return;

      setStatus({ kind: "working", text: `Testing ${provider.name} · ${model}...` });
      try {
        const { milliseconds } = await testAiModel({
          kind: "chat",
          baseUrl: provider.baseUrl,
          model,
          apiKeyName: provider.apiKeyName,
          queryPrefix: "",
          documentPrefix: ""
        });
        if (run.cancelled) return;
        addLog(true, `${provider.name} · ${model}: answered in ${formatTime(milliseconds)}`);
        cursor.current = { p, m };
        setStatus({ kind: "ready", provider, model, ms: milliseconds });
        return;
      } catch (err) {
        if (run.cancelled) return;
        const message = errorMessage(err, "No answer.");
        addLog(false, `${provider.name} · ${model}: ${message.length > 160 ? `${message.slice(0, 160)}...` : message}`);
        // Every model of a service shares its limit. After a "too many requests" we wait and try this model once
        // more; if the service is still busy, the rest of it is skipped.
        if (/\b429\b/.test(message)) {
          if (!waited) {
            waited = true;
            addLog(false, `${provider.name}: its limit was reached, waiting 20 seconds before going on.`);
            setStatus({ kind: "working", text: `${provider.name}: its limit was reached. Waiting 20 seconds...` });
            await sleep(20000);
            m--; // the same model again
          } else {
            addLog(false, `${provider.name}: still busy, the rest of it is skipped.`);
            p++;
            m = -1;
            waited = false;
          }
        }
      }
    }
  }

  async function begin(mode: "current" | "beginning") {
    token.current.cancelled = true;
    const run = { cancelled: false };
    token.current = run;
    setLog([]);

    if (providers.length === 0) {
      setStatus({ kind: "done" });
      return;
    }
    if (mode === "beginning") {
      cursor.current = { p: 0, m: -1 };
      await advance(cursor.current, run);
      return;
    }

    // After the model in use: find its service and its place in the list of that service.
    const p = Math.max(
      0,
      providers.findIndex((provider) => sameUrl(provider.baseUrl, current.baseUrl))
    );
    const models = await listOf(providers[p]);
    if (run.cancelled) return;
    cursor.current = { p, m: models.indexOf(current.model) };
    await advance(cursor.current, run);
  }

  useEffect(() => {
    void begin(start);
    return () => {
      token.current.cancelled = true;
    };
    // Runs once, when the dialog opens; the buttons start it again.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const working = status.kind === "working";

  return (
    <Modal title="Find a chat model" onClose={onClose} maxWidth="max-w-2xl">
      <p className="text-sm text-stone-600">
        This goes through the chat models of the services we have a key for, one after another, and stops at the first one
        that answers a small test question, showing how long it took.{" "}
        <span className="font-medium text-stone-800">Select</span> uses it,{" "}
        <span className="font-medium text-stone-800">Next</span> tries the next one. A model that gives an error or no answer
        is skipped by itself. The test has no tools, so check a few questions in the chat after switching.
      </p>

      <div className="mt-4 rounded-xl bg-stone-50 p-4 ring-1 ring-stone-200">
        {status.kind === "working" && <p className="text-sm text-stone-600">{status.text}</p>}
        {status.kind === "ready" && (
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="min-w-0">
              <p className="text-sm text-stone-500">{status.provider.name}</p>
              <p className="break-all font-mono text-sm font-medium text-stone-900">{status.model}</p>
              <p className="mt-1 text-sm text-emerald-700">✓ answered in {formatTime(status.ms)}</p>
            </div>
            <div className="flex gap-2">
              <Button onClick={() => onSelect(status.provider, status.model)}>Select</Button>
              <Button variant="secondary" onClick={() => advance(cursor.current, token.current)}>
                Next
              </Button>
            </div>
          </div>
        )}
        {status.kind === "done" && (
          <p className="text-sm text-stone-600">
            There are no more models to try.
            {log.some((line) => !line.ok) && " The reasons the models were skipped are listed below."}
          </p>
        )}
      </div>

      <div className="mt-3 flex flex-wrap gap-2">
        <Button variant="secondary" size="sm" disabled={working} onClick={() => begin("current")}>
          Next after the model in use
        </Button>
        <Button variant="secondary" size="sm" disabled={working} onClick={() => begin("beginning")}>
          Start from the beginning
        </Button>
      </div>

      {log.length > 0 && (
        <ul className="mt-4 max-h-56 space-y-1 overflow-y-auto text-xs">
          {log.map((line) => (
            <li key={line.id} className={line.ok ? "text-emerald-700" : "text-amber-700"}>
              {line.ok ? "✓" : "✕"} {line.text}
            </li>
          ))}
        </ul>
      )}

      <div className="mt-5 flex justify-end">
        <Button variant="secondary" onClick={onClose}>
          Close
        </Button>
      </div>
    </Modal>
  );
}
