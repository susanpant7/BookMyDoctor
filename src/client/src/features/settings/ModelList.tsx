import { useEffect, useState, type ReactNode } from "react";
import { loadDefaultKnowledge, reembedKnowledge } from "@/features/knowledge/knowledgeApi";
import AvailableModelsDialog from "@/features/settings/AvailableModelsDialog";
import ModelForm from "@/features/settings/ModelForm";
import ModelTourDialog, { type TourProvider } from "@/features/settings/ModelTourDialog";
import {
  deleteAiModel,
  getAiKeys,
  getAiModels,
  getAiServices,
  switchAiModel,
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

// Choosing the chat or embedding model: the model in use, the admin's saved models and the known services. "Use"
// tests a chat model before switching to it; for an embedding model it embeds all the knowledge with it first. A
// failure changes nothing. "View models" asks a service which models our key can use.
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
  const [confirming, setConfirming] = useState<Choice | null>(null);
  const [viewing, setViewing] = useState<Choice | null>(null);
  const [tour, setTour] = useState<"current" | "beginning" | null>(null);

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
  // other addresses. The service at the address in use is tested with the key the chat really reads. An address
  // still to be filled in (Cloudflare's <ACCOUNT_ID>) is left out.
  const tourProviders: TourProvider[] = [...serviceChoices, ...savedChoices]
    .map((c) => (sameUrl(c.baseUrl, current.baseUrl) && !isOllama(c.baseUrl) ? { ...c, apiKeyName: keyInUse } : c))
    .filter((c) => !c.baseUrl.includes("<"))
    .filter((c) => isOllama(c.baseUrl) || (c.apiKeyName !== "" && isSet(c.apiKeyName)))
    .filter((c, i, all) => all.findIndex((other) => sameUrl(other.baseUrl, c.baseUrl)) === i);

  // Chat: tested, then used. Embedding: all the knowledge is embedded with it, then it is used.
  // After the first embedding model is chosen there is no knowledge yet, so the default knowledge files are loaded
  // with it. A failure is a message, not an error: the model is in use either way.
  async function loadKnowledge() {
    try {
      const results = await loadDefaultKnowledge();
      const failed = results.filter((result) => result.error);
      return failed.length > 0
        ? `${failed.length} of ${results.length} knowledge files were not loaded. ${failed[0].error}`
        : `The ${results.length} default knowledge files were loaded.`;
    } catch (err) {
      return `The knowledge files were not loaded: ${errorMessage(err, "please try again.")} Load them on the Knowledge page.`;
    }
  }

  async function apply(choice: Choice) {
    setNotice(null);
    setConfirming(null);
    setWorking(choice.key);
    try {
      const { reembedded } = await switchAiModel({
        kind,
        baseUrl: choice.baseUrl,
        model: choice.model,
        apiKeyName: choice.apiKeyName,
        queryPrefix: isEmbedding ? choice.queryPrefix : "",
        documentPrefix: isEmbedding ? choice.documentPrefix : ""
      });
      if (isEmbedding && reembedded === 0) {
        setNotice({ variant: "success", message: `Now using ${choice.model}. Loading the knowledge files...` });
        setNotice({ variant: "success", message: `Now using ${choice.model}. ${await loadKnowledge()}` });
      } else {
        setNotice({
          variant: "success",
          message: isEmbedding
            ? `Now using ${choice.model}. ${reembedded} knowledge ${reembedded === 1 ? "section was" : "sections were"} embedded with it.`
            : `Now using ${choice.model}. It answered a test question, so it works with this key.`
        });
      }
      onChanged();
    } catch (err) {
      setNotice({ variant: "error", message: errorMessage(err, "Could not switch the model. Nothing was changed.") });
    } finally {
      setWorking("");
    }
  }

  const choose = (choice: Choice) => (isEmbedding ? setConfirming(choice) : apply(choice));

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
      <Button variant="secondary" size="sm" disabled={working !== ""} onClick={() => choose(choice)}>
        {working === choice.key ? (isEmbedding ? "Converting..." : "Testing...") : "Use"}
      </Button>
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
    <div className="mb-4 rounded-2xl border border-brand-100 bg-brand-50/40 p-6 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 max-w-3xl">
          <h3 className="text-base font-semibold text-stone-900">Choosing the {what} model</h3>
          <div className="mt-1 space-y-1 text-sm text-stone-600">
            {isEmbedding ? (
              <p>
                The knowledge search turns text into vectors with this model. It can be a different service from the chat;
                when hosting in the cloud without Ollama, use an online one.{" "}
                <span className="font-medium text-stone-800">Use embeds all the knowledge with the new model</span> (it can
                take a minute), and only switches when that worked.
              </p>
            ) : (
              <p>
                The model writes the answers and decides when to look things up, so it must support tool calling. An address
                with no path, such as <span className="font-mono">http://localhost:11434</span>, is Ollama on this computer
                (free, private, slower); one with a path is an online service (fast, but the chats are sent to it).{" "}
                <span className="font-medium text-stone-800">Use tests the model first</span> and only switches when it answers.
              </p>
            )}
            <p>
              Each online service's key goes in the server's <span className="font-mono">.env</span> file under the{" "}
              <span className="font-medium text-stone-800">API key name</span> on its row (for example{" "}
              <span className="font-mono">GEMINI_API_KEY</span>), then a server restart. Only the name is saved here, never the
              key. Ollama needs no key.
            </p>
          </div>
        </div>
        {isEmbedding && (
          <Button variant="secondary" size="sm" disabled={working !== ""} onClick={reembed}>
            {working === "reembed" ? "Embedding the knowledge..." : "Re-embed all knowledge"}
          </Button>
        )}
      </div>

      {missingKey && (
        <div className="mt-3">
          <Alert
            message={`The ${what} model in use is an online service, but its key (${keyInUse}) is not set in .env, so ${isEmbedding ? "the knowledge search" : "the assistant"} can't work.`}
          />
        </div>
      )}
      {working !== "" && working !== "reembed" && (
        <div className="mt-3">
          <Alert
            variant="warning"
            message={
              isEmbedding
                ? "Embedding the knowledge with the new model. This can take a minute, please keep this page open."
                : "Testing the model with a short question before switching. It is only used when it answers."
            }
          />
        </div>
      )}
      {notice && (
        <div className="mt-3">
          <Alert variant={notice.variant} message={notice.message} />
        </div>
      )}

      {!isEmbedding && (
        <div className="mt-4 flex flex-wrap gap-2">
          <Button size="sm" disabled={working !== "" || tourProviders.length === 0} onClick={() => setTour("current")}>
            Next model
          </Button>
          <Button
            variant="secondary"
            size="sm"
            disabled={working !== "" || tourProviders.length === 0}
            onClick={() => setTour("beginning")}
          >
            Start from the beginning
          </Button>
          <span className="self-center text-sm text-stone-500">
            Tries the models of the services we have a key for, one after another, with the response time of each.
          </span>
        </div>
      )}

      {!current.model && (
        <div className="mt-5 rounded-xl bg-amber-50 p-4 text-sm text-amber-800 ring-1 ring-inset ring-amber-200">
          No {what} model is selected yet.{" "}
          {isEmbedding
            ? "Choose one below with Use: the knowledge files are then loaded with it."
            : "Choose one below with Use, or try Next model to find one that answers."}
        </div>
      )}

      {current.model && (
        <ul className="mt-5 rounded-xl bg-white px-4 ring-1 ring-brand-100">
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
                {!savedInUse && !serviceInUse && (
                  <Button variant="ghost" size="sm" onClick={() => setForm({ model: null, initial: blank })}>
                    Save to the list
                  </Button>
                )}
              </>
            }
          />
        </ul>
      )}

      <div className="mt-6 flex items-center justify-between gap-3">
        <p className="text-sm font-semibold text-stone-700">Saved {what} models</p>
        <Button variant="secondary" size="sm" onClick={() => setForm({ model: null })}>
          Add {what} model
        </Button>
      </div>
      {savedNotInUse.length === 0 ? (
        <p className="mt-2 text-sm text-stone-500">
          None yet. Add a model (its address and name), or edit a copy of a service below, to switch to it later with one
          click.
        </p>
      ) : (
        <ul className="mt-2 divide-y divide-stone-200">
          {savedNotInUse.map((c) => (
            <Row key={c.key} choice={c} isEmbedding={isEmbedding} isSet={isSet} inUse={false} actions={rowActions(c)} />
          ))}
        </ul>
      )}

      {servicesShown.length > 0 && (
        <>
          <p className="mt-6 text-sm font-semibold text-stone-700">Services we can use</p>
          <ul className="mt-2 divide-y divide-stone-200">
            {servicesShown.map((c) => (
              <Row key={c.key} choice={c} isEmbedding={isEmbedding} isSet={isSet} inUse={false} actions={rowActions(c)} />
            ))}
          </ul>
        </>
      )}

      {tour && (
        <ModelTourDialog
          providers={tourProviders}
          current={current}
          start={tour}
          onSelect={(provider, model) => {
            setTour(null);
            choose({ ...provider, model, notes: "" });
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
            choose({ ...chosen, model });
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

      {confirming && (
        <ConfirmDialog
          title="Embed the knowledge with this model?"
          message={`Every knowledge section will be embedded again with ${confirming.model}, because vectors from different models can't be compared. This can take a minute. If it fails (for example a wrong key), nothing is changed.`}
          confirmLabel="Convert and use"
          onConfirm={() => apply(confirming)}
          onCancel={() => setConfirming(null)}
        />
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
