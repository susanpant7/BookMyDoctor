import { useEffect, useState, type ReactNode } from "react";
import { loadDefaultKnowledge, reembedKnowledge } from "@/features/knowledge/knowledgeApi";
import AvailableModelsDialog from "@/features/settings/AvailableModelsDialog";
import EmbeddingSwitchDialog, { type EmbeddingSwitch } from "@/features/settings/EmbeddingSwitchDialog";
import ModelForm from "@/features/settings/ModelForm";
import ModelTourDialog, { type TourProvider } from "@/features/settings/ModelTourDialog";
import {
  deleteAiModel,
  getAiKeys,
  getAiModels,
  getAiServices,
  switchAiModel,
  withModel,
  type AiKeysInfo,
  type AiModel,
  type AiModelKind,
  type AiService,
  type ModelChoice,
  type SaveAiModelRequest
} from "@/features/settings/settingsApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import ConfirmDialog from "@/shared/components/ConfirmDialog";

// What the assistant uses now, from the settings.
export type ModelInUse = { baseUrl: string; model: string; apiKeyName: string; queryPrefix: string; documentPrefix: string };

type ModelListProps = {
  kind: AiModelKind;
  current: ModelInUse;
  // Called after the model in use changed, so the page loads the settings again.
  onChanged: () => void;
};

// One row of the lists: the model in use, a saved model or a known service.
type Choice = ModelChoice & { key: string; name: string; notes: string; saved?: AiModel };

const sameUrl = (a: string, b: string) => a.replace(/\/+$/, "") === b.replace(/\/+$/, "");

// The same address, model and (for embeddings) prefixes. The key name doesn't count: the same model with another key
// is still the same model.
const sameModel = (a: ModelInUse, b: ModelInUse) =>
  sameUrl(a.baseUrl, b.baseUrl) && a.model === b.model && a.queryPrefix === b.queryPrefix && a.documentPrefix === b.documentPrefix;

// An address with no path is Ollama, which needs no key. Same rule as the server.
function isOllama(baseUrl: string) {
  try {
    return new URL(baseUrl).pathname.replace(/\//g, "") === "";
  } catch {
    return false;
  }
}

// Notes may contain web addresses (where to get a key); those become links.
function linkify(text: string): ReactNode {
  return text.split(/(https?:\/\/[^\s]+)/g).map((part, i) =>
    /^https?:\/\//.test(part) ? (
      <a key={i} href={part} target="_blank" rel="noreferrer" className="font-semibold text-brand-600">
        {part.replace(/^https?:\/\//, "")}
      </a>
    ) : (
      part
    )
  );
}

// Choosing the chat or embedding model: the model in use, the admin's saved models and the known services. "View
// models" asks a service which models our key can use, and Select switches to one: a chat model is tested first; for
// an embedding model all the knowledge is cleaned and inserted again with it first. A failure changes nothing.
export default function ModelList({ kind, current, onChanged }: ModelListProps) {
  const isEmbedding = kind === "embedding";
  const what = isEmbedding ? "embedding" : "chat";
  const [keys, setKeys] = useState<AiKeysInfo | null>(null);
  const [services, setServices] = useState<AiService[]>([]);
  const [saved, setSaved] = useState<AiModel[]>([]);
  const [version, setVersion] = useState(0);
  const [working, setWorking] = useState("");
  const [notice, setNotice] = useState<{ variant: "success" | "warning" | "error"; message: string } | null>(null);
  const [form, setForm] = useState<{
    model: AiModel | null;
    initial?: SaveAiModelRequest;
    title?: string;
    inUse?: boolean;
  } | null>(null);
  const [deleting, setDeleting] = useState<AiModel | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  // The dialog for changing the embedding model: the question, then the progress, then the result.
  const [switching, setSwitching] = useState<(EmbeddingSwitch & { choice: Choice }) | null>(null);
  const [viewing, setViewing] = useState<Choice | null>(null);
  const [tour, setTour] = useState<"current" | "beginning" | null>(null);
  const [confirmingSearch, setConfirmingSearch] = useState(false);

  // Loaded again when the model in use or the saved models change, since they decide which keys are read.
  useEffect(() => {
    let ignore = false;
    getAiKeys()
      .then((loaded) => !ignore && setKeys(loaded))
      .catch(() => !ignore && setKeys(null));
    return () => {
      ignore = true;
    };
  }, [current.baseUrl, current.model, current.apiKeyName, version]);

  useEffect(() => {
    let ignore = false;
    getAiServices(kind)
      .then((loaded) => !ignore && setServices(loaded))
      .catch(() => !ignore && setServices([]));
    return () => {
      ignore = true;
    };
  }, [kind]);

  useEffect(() => {
    let ignore = false;
    getAiModels(kind)
      .then((loaded) => !ignore && setSaved(loaded))
      .catch((err) => !ignore && setNotice({ variant: "error", message: errorMessage(err, "Could not load the saved models.") }));
    return () => {
      ignore = true;
    };
  }, [kind, version]);

  // Prefixes only count for embedding models.
  const inUseValues: ModelInUse = isEmbedding ? current : { ...current, queryPrefix: "", documentPrefix: "" };
  const isCurrent = (c: ModelInUse) => sameModel(c, inUseValues);

  const savedChoices: Choice[] = saved.map((m) => ({ ...m, key: `saved-${m.id}`, saved: m }));
  const serviceChoices: Choice[] = services.map((s) => ({ ...s, kind, key: `service-${s.name}` }));
  const savedInUse = savedChoices.find(isCurrent);
  const serviceInUse = serviceChoices.find(isCurrent);
  // The model in use is only shown at the top; a service that a saved model copies exactly is not listed twice.
  const savedNotInUse = savedChoices.filter((c) => c !== savedInUse);
  const servicesShown = serviceChoices.filter((c) => !isCurrent(c) && !savedChoices.some((s) => sameModel(s, c)));

  // The variable the model in use really reads, as the server works it out (its own key name, else the key of the
  // service at its address, else the variable of the first version).
  const keyInUse = keys ? (isEmbedding ? keys.embeddingKeyNameInUse : keys.chatKeyNameInUse) : "";
  const keyInUseSet = keys ? (isEmbedding ? keys.embeddingKeyInUseSet : keys.chatKeyInUseSet) : true;
  const missingKey = current.baseUrl !== "" && !isOllama(current.baseUrl) && !keyInUseSet;
  const inUseChoice: Choice = {
    ...inUseValues,
    kind,
    key: "in-use",
    apiKeyName: isOllama(current.baseUrl) ? "" : keyInUse,
    name: savedInUse?.name ?? serviceInUse?.name ?? `Current ${what} model`,
    notes: savedInUse?.notes ?? serviceInUse?.notes ?? ""
  };

  const isSet = (name: string) => (keys ? keys.setKeyNames.includes(name) : true);

  // The services the tour goes through: the ones we have a key for (Ollama needs none), then the saved models at
  // other addresses. The service at the address in use is tested with the key the chat really reads. Cloudflare's
  // <ACCOUNT_ID> is filled in by the server from CLOUDFLARE_ACCOUNT_ID; without it the tour says why it skipped it.
  const tourProviders: TourProvider[] = [...serviceChoices, ...savedChoices]
    .map((c) => (sameUrl(c.baseUrl, current.baseUrl) && !isOllama(c.baseUrl) ? { ...c, apiKeyName: keyInUse } : c))
    .filter((c) => isOllama(c.baseUrl) || (c.apiKeyName !== "" && isSet(c.apiKeyName)))
    .filter((c, i, all) => all.findIndex((other) => sameUrl(other.baseUrl, c.baseUrl)) === i);

  // After the first embedding model is chosen there is no knowledge yet, so the default knowledge files are loaded with
  // it. A failure is a message, not an error: the model is in use either way.
  async function loadKnowledge(onProgress: (done: number, total: number) => void) {
    try {
      const results = await loadDefaultKnowledge(onProgress);
      const failed = results.filter((result) => result.error);
      return failed.length > 0
        ? { ok: false, text: `${failed.length} of ${results.length} knowledge files were not loaded. ${failed[0].error}` }
        : { ok: true, text: `The ${results.length} default knowledge files were loaded.` };
    } catch (err) {
      return {
        ok: false,
        text: `The knowledge files were not loaded: ${errorMessage(err, "please try again.")} Load them on the Knowledge page.`
      };
    }
  }

  // Chat: tested, then used.
  async function applyChat(choice: Choice) {
    setNotice(null);
    setWorking(choice.key);
    try {
      await switchAiModel({
        kind,
        baseUrl: choice.baseUrl,
        model: choice.model,
        apiKeyName: choice.apiKeyName,
        queryPrefix: "",
        documentPrefix: ""
      });
      setNotice({ variant: "success", message: `Now using ${choice.model}. It answered a test question, so it works with this key.` });
      onChanged();
    } catch (err) {
      setNotice({ variant: "error", message: errorMessage(err, "Could not switch the model. Nothing was changed.") });
    } finally {
      setWorking("");
    }
  }

  // Embedding: all the knowledge is embedded with it, then it is used. The dialog stays open the whole time and shows
  // how far it is.
  async function switchEmbedding(choice: Choice) {
    setNotice(null);
    setWorking(choice.key);
    const show = (step: string, done = 0, total = 0) =>
      setSwitching({ state: "running", model: choice.model, step, done, total, choice });
    const finish = (ok: boolean, message: string) => setSwitching({ state: "finished", model: choice.model, ok, message, choice });

    show("Starting...");
    try {
      const { reembedded } = await switchAiModel(
        {
          kind,
          baseUrl: choice.baseUrl,
          model: choice.model,
          apiKeyName: choice.apiKeyName,
          queryPrefix: choice.queryPrefix,
          documentPrefix: choice.documentPrefix
        },
        (done, total) => show("Embedding the stored sections again with the new model...", done, total)
      );
      if (reembedded === 0) {
        // There was no knowledge, so the default knowledge files are loaded with the new model.
        show("Loading the default knowledge files...");
        const loaded = await loadKnowledge((done, total) => show("Loading the default knowledge files...", done, total));
        finish(loaded.ok, `Now using ${choice.model}. ${loaded.text}`);
      } else {
        finish(
          true,
          `Now using ${choice.model}. The knowledge was cleaned and reinserted: ${reembedded} ${reembedded === 1 ? "section was" : "sections were"} embedded again.`
        );
      }
      onChanged();
    } catch (err) {
      finish(false, errorMessage(err, "Could not switch the model. Nothing was changed."));
    } finally {
      setWorking("");
    }
  }

  const choose = (choice: Choice) =>
    isEmbedding ? setSwitching({ state: "confirm", model: choice.model, choice }) : applyChat(choice);

  async function reembed() {
    setNotice(null);
    setWorking("reembed");
    try {
      const { sections } = await reembedKnowledge();
      setNotice({ variant: "success", message: `Embedded ${sections} knowledge sections again with ${current.model}.` });
    } catch (err) {
      setNotice({ variant: "error", message: errorMessage(err, "Embedding the knowledge again failed.") });
    } finally {
      setWorking("");
    }
  }

  // Saving the saved model that is in use switches to the changed values (with the same test as Use).
  async function handleSaved(model: AiModel) {
    const wasInUse = form?.model ? isCurrent(form.model) : false;
    setForm(null);
    setVersion((v) => v + 1);
    if (wasInUse && !isCurrent(model)) choose({ ...model, key: `saved-${model.id}`, saved: model });
  }

  async function handleDelete() {
    if (!deleting) return;
    setDeleteBusy(true);
    try {
      await deleteAiModel(deleting.id);
      setVersion((v) => v + 1);
    } catch (err) {
      setNotice({ variant: "error", message: errorMessage(err, "Could not delete the model. Please try again.") });
    } finally {
      setDeleteBusy(false);
      setDeleting(null);
    }
  }

  // The form for a new model starts from the model in use, with the key it really reads.
  const blank = { kind, name: "", notes: "", ...inUseValues, apiKeyName: inUseChoice.apiKeyName };

  const rowActions = (choice: Choice) => (
    <>
      <Button variant="ghost" size="sm" disabled={working !== ""} onClick={() => setViewing(choice)}>
        View models
      </Button>
      {choice.saved ? (
        <>
          <Button variant="ghost" size="sm" disabled={working !== ""} onClick={() => setForm({ model: choice.saved! })}>
            Edit
          </Button>
          <Button variant="dangerGhost" size="sm" disabled={working !== ""} onClick={() => setDeleting(choice.saved!)}>
            Delete
          </Button>
        </>
      ) : (
        <Button
          variant="ghost"
          size="sm"
          disabled={working !== ""}
          onClick={() =>
            setForm({
              model: null,
              title: `Edit a copy of ${choice.name}`,
              initial: { ...blank, ...choice, kind, name: `${choice.name} (mine)` }
            })
          }
        >
          Edit
        </Button>
      )}
    </>
  );

  return (
    <div className="mb-4 rounded-2xl border border-brand-100 bg-brand-50/40 p-4 shadow-sm sm:p-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h3 className="text-base font-semibold text-stone-900">{isEmbedding ? "Embedding" : "Chat"} model in use</h3>
        <div className="flex flex-wrap gap-2">
          <Button
            size="sm"
            disabled={working !== "" || tourProviders.length === 0}
            onClick={() => setTour("current")}
            title={`Tests the ${what} models after the one in use, one by one, and stops at the first that answers`}
          >
            Next model
          </Button>
          <Button
            variant="secondary"
            size="sm"
            disabled={working !== "" || tourProviders.length === 0}
            onClick={() => setConfirmingSearch(true)}
            title={`Tests the ${what} models from the first one, and stops at the first that answers`}
          >
            Search model from the beginning
          </Button>
          <Button variant="secondary" size="sm" disabled={working !== ""} onClick={() => setForm({ model: null })}>
            Add {what} model
          </Button>
          {isEmbedding && (
            <Button variant="secondary" size="sm" disabled={working !== ""} onClick={reembed}>
              {working === "reembed" ? "Embedding the knowledge..." : "Re-embed all knowledge"}
            </Button>
          )}
        </div>
      </div>

      {missingKey && (
        <div className="mt-3">
          <Alert
            message={`The ${what} model in use is an online service, but its key (${keyInUse}) is not set in .env, so ${isEmbedding ? "the knowledge search" : "the assistant"} can't work.`}
          />
        </div>
      )}
      {working !== "" && working !== "reembed" && !isEmbedding && (
        <div className="mt-3">
          <Alert variant="warning" message="Testing the model with a short question before switching. It is only used when it answers." />
        </div>
      )}
      {notice && (
        <div className="mt-3">
          <Alert variant={notice.variant} message={notice.message} />
        </div>
      )}

      {!current.model ? (
        <div className="mt-3 rounded-xl bg-amber-50 p-4 text-sm text-amber-800 ring-1 ring-inset ring-amber-200">
          No {what} model is selected yet. Choose one below with View models and Select, or try Next model.
          {isEmbedding && " The knowledge files are then loaded with it."}
        </div>
      ) : (
        <ul className="mt-3 rounded-xl bg-white px-4 ring-1 ring-brand-100">
          <Row
            choice={inUseChoice}
            isEmbedding={isEmbedding}
            isSet={isSet}
            inUse
            actions={
              <>
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={working !== ""}
                  onClick={() =>
                    savedInUse
                      ? setForm({ model: savedInUse.saved! })
                      : setForm({ model: null, inUse: true, title: `Edit the ${what} model in use`, initial: blank })
                  }
                >
                  Edit
                </Button>
                <Button variant="ghost" size="sm" disabled={working !== ""} onClick={() => setViewing(inUseChoice)}>
                  View models
                </Button>
              </>
            }
          />
        </ul>
      )}

      <details className="mt-3 text-sm text-stone-600">
        <summary className="cursor-pointer font-medium text-brand-700">How choosing a {what} model works</summary>
        <div className="mt-2 space-y-1">
          {isEmbedding ? (
            <p>
              The knowledge search turns text into vectors with this model. It can be a different service from the chat; when
              hosting in the cloud without Ollama, use an online one.{" "}
              <span className="font-medium text-stone-800">
                Selecting a model cleans the stored knowledge and inserts it again with the new model
              </span>{" "}
              (about a minute), and is only saved when that worked.
            </p>
          ) : (
            <p>
              The model writes the answers and decides when to look things up, so it must support tool calling. An address with
              no path, such as <span className="font-mono">http://localhost:11434</span>, is Ollama on this computer (free,
              private, slower); one with a path is an online service (fast, but the chats are sent to it). The model list can
              include models this key can't use, so <span className="font-medium text-stone-800">Select tests the model first</span>{" "}
              and only switches when it answers.
            </p>
          )}
          <p>
            Each online service's key goes in the server's <span className="font-mono">.env</span> file under the{" "}
            <span className="font-medium text-stone-800">API key name</span> on its row (for example{" "}
            <span className="font-mono">GEMINI_API_KEY</span>), then a server restart. Only the name is saved here, never the
            key. Ollama needs no key. <span className="font-medium text-stone-800">Next model</span> and{" "}
            <span className="font-medium text-stone-800">Search model from the beginning</span> try the {what} models of the
            services we have a key for, one after another, with the response time of each, and stop at the first that answers.
          </p>
        </div>
      </details>

      <ModelGroup title={`Saved ${what} models`} count={savedNotInUse.length} defaultOpen={savedNotInUse.length > 0}>
        {savedNotInUse.length === 0 ? (
          <li className="py-3 text-sm text-stone-500">
            None yet. Add a model (its address and name), or edit a copy of a service below, to switch to it later with one click.
          </li>
        ) : (
          savedNotInUse.map((c) => <CompactRow key={c.key} choice={c} isSet={isSet} actions={rowActions(c)} />)
        )}
      </ModelGroup>

      {servicesShown.length > 0 && (
        <ModelGroup title="Services we can use" count={servicesShown.length}>
          {servicesShown.map((c) => (
            <CompactRow key={c.key} choice={c} isSet={isSet} actions={rowActions(c)} />
          ))}
        </ModelGroup>
      )}

      {tour && (
        <ModelTourDialog
          kind={kind}
          providers={tourProviders}
          current={current}
          start={tour}
          onSelect={(provider, model) => {
            setTour(null);
            choose({ ...provider, ...withModel(provider, model), notes: "" });
          }}
          onClose={() => setTour(null)}
        />
      )}

      {viewing && (
        <AvailableModelsDialog
          choice={viewing}
          current={sameUrl(viewing.baseUrl, current.baseUrl) ? current.model : ""}
          busy={working !== ""}
          onSelect={(model) => {
            const chosen = viewing;
            setViewing(null);
            choose({ ...chosen, ...withModel(chosen, model) });
          }}
          onClose={() => setViewing(null)}
        />
      )}

      {form && (
        <ModelForm
          kind={kind}
          model={form.model}
          initial={form.initial ?? blank}
          title={form.title}
          onUse={
            form.inUse
              ? (values) => {
                  setForm(null);
                  choose({ ...values, key: "in-use", name: "the edited model" });
                }
              : undefined
          }
          onSaved={handleSaved}
          onCancel={() => setForm(null)}
        />
      )}

      {deleting && (
        <ConfirmDialog
          title={`Delete ${what} model`}
          message={`Delete "${deleting.name}" from the saved models? The model in use is not changed.`}
          confirmLabel="Delete"
          busy={deleteBusy}
          onConfirm={handleDelete}
          onCancel={() => setDeleting(null)}
        />
      )}

      {confirmingSearch && (
        <ConfirmDialog
          title="Search model from the beginning?"
          message={`Every ${what} model of the services we have a key for is tested one after another, from the first one. It stops at the first model that answers, and nothing is changed until we select it.`}
          confirmLabel="Start searching"
          onConfirm={() => {
            setConfirmingSearch(false);
            setTour("beginning");
          }}
          onCancel={() => setConfirmingSearch(false)}
        />
      )}

      {switching && (
        <EmbeddingSwitchDialog value={switching} onConfirm={() => switchEmbedding(switching.choice)} onClose={() => setSwitching(null)} />
      )}
    </div>
  );
}

type RowProps = {
  choice: Choice;
  isEmbedding: boolean;
  // Whether the .env variable with this name is set.
  isSet: (keyName: string) => boolean;
  inUse: boolean;
  actions: ReactNode;
};

function Row({ choice, isEmbedding, isSet, inUse, actions }: RowProps) {
  return (
    <li className="grid gap-2 py-4 sm:grid-cols-[1fr_auto] sm:items-start sm:gap-4">
      <div className="min-w-0 space-y-1">
        <p className="font-medium text-stone-800">
          {choice.name}
          {inUse && <span className="ml-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-semibold text-brand-700">In use</span>}
        </p>
        <p className="break-all text-sm text-stone-600">
          Address: <span className="font-mono">{choice.baseUrl}</span>
        </p>
        <p className="break-all text-sm text-stone-600">
          Model: <span className="font-mono">{choice.model}</span>
        </p>
        {isEmbedding && (choice.queryPrefix || choice.documentPrefix) && (
          <p className="break-all text-sm text-stone-600">
            Prefixes: <span className="font-mono">{choice.queryPrefix || "(none)"}</span> for a question,{" "}
            <span className="font-mono">{choice.documentPrefix || "(none)"}</span> for a section
          </p>
        )}
        <p className="break-all text-sm text-stone-600">
          API key name:{" "}
          {choice.apiKeyName ? (
            <>
              <span className="font-mono">{choice.apiKeyName}</span>{" "}
              {isSet(choice.apiKeyName) ? (
                <span className="text-emerald-700">✓ set in .env</span>
              ) : (
                <span className="text-amber-700">not set in .env</span>
              )}
            </>
          ) : isOllama(choice.baseUrl) ? (
            <span className="text-stone-400">none needed</span>
          ) : (
            <span className="text-stone-500">none saved (uses the key of the service at this address)</span>
          )}
        </p>
        {choice.notes && <p className="break-words text-sm text-stone-500">{linkify(choice.notes)}</p>}
      </div>
      <div className="flex flex-wrap gap-2">{actions}</div>
    </li>
  );
}

// A list that can be folded away, with how many items it holds.
function ModelGroup({
  title,
  count,
  defaultOpen = false,
  children
}: {
  title: string;
  count: number;
  defaultOpen?: boolean;
  children: ReactNode;
}) {
  return (
    <details open={defaultOpen} className="group mt-3 rounded-xl bg-white ring-1 ring-stone-200">
      <summary className="flex cursor-pointer list-none items-center justify-between gap-3 px-4 py-3 text-sm font-semibold text-stone-700">
        <span>
          {title} <span className="ml-1 rounded-full bg-stone-100 px-2 py-0.5 text-xs font-medium text-stone-600">{count}</span>
        </span>
        <span className="text-stone-400 transition group-open:rotate-180" aria-hidden="true">
          &#9662;
        </span>
      </summary>
      <ul className="divide-y divide-stone-100 border-t border-stone-100 px-4">{children}</ul>
    </details>
  );
}

// One saved model or service in a short form: its name, model and key status. The address and the notes are under Details.
function CompactRow({ choice, isSet, actions }: { choice: Choice; isSet: (keyName: string) => boolean; actions: ReactNode }) {
  return (
    <li className="flex flex-wrap items-start justify-between gap-x-4 gap-y-2 py-3">
      <div className="min-w-0 flex-1">
        <p className="font-medium text-stone-800">
          {choice.name}
          {choice.apiKeyName ? (
            isSet(choice.apiKeyName) ? (
              <span className="ml-2 text-xs font-normal text-emerald-700">&#10003; key set</span>
            ) : (
              <span className="ml-2 text-xs font-normal text-amber-700">key not set in .env</span>
            )
          ) : isOllama(choice.baseUrl) ? (
            <span className="ml-2 text-xs font-normal text-stone-400">no key needed</span>
          ) : null}
        </p>
        <p className="break-all font-mono text-xs text-stone-500">{choice.model}</p>
        <details className="mt-1 text-xs text-stone-500">
          <summary className="cursor-pointer text-brand-600">Details</summary>
          <p className="mt-1 break-all">
            Address: <span className="font-mono">{choice.baseUrl}</span>
          </p>
          {choice.apiKeyName && (
            <p>
              API key name: <span className="font-mono">{choice.apiKeyName}</span>
            </p>
          )}
          {choice.notes && <p className="mt-1 break-words">{linkify(choice.notes)}</p>}
        </details>
      </div>
      <div className="flex flex-wrap gap-1">{actions}</div>
    </li>
  );
}
