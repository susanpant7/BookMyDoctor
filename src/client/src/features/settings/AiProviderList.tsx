import { useEffect, useState, type ReactNode } from "react";
import ChatModelForm from "@/features/settings/ChatModelForm";
import {
  deleteChatModel,
  getAiProviders,
  getChatModels,
  updateSetting,
  type AiProvidersInfo,
  type SavedChatModel
} from "@/features/settings/settingsApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import ConfirmDialog from "@/shared/components/ConfirmDialog";

type AiProviderListProps = {
  // The current "Chat API address" and "Chat model" settings.
  baseUrl: string;
  model: string;
  onChanged: () => void;
};

const sameUrl = (a: string, b: string) => a.replace(/\/+$/, "") === b.replace(/\/+$/, "");

// Notes on switching the chat model: the chat models the admin saved, and the known services with their address, a
// model and where to get a key. "Use" sets the Chat API address and the Chat model to that row's values.
export default function AiProviderList({ baseUrl, model, onChanged }: AiProviderListProps) {
  const [info, setInfo] = useState<AiProvidersInfo | null>(null);
  const [saved, setSaved] = useState<SavedChatModel[]>([]);
  const [savedVersion, setSavedVersion] = useState(0);
  const [switching, setSwitching] = useState("");
  const [error, setError] = useState("");
  const [form, setForm] = useState<{ chatModel: SavedChatModel | null } | null>(null);
  const [deleting, setDeleting] = useState<SavedChatModel | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);

  useEffect(() => {
    let ignore = false;
    getAiProviders()
      .then((loaded) => !ignore && setInfo(loaded))
      .catch(() => !ignore && setInfo(null));
    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    let ignore = false;
    getChatModels()
      .then((loaded) => !ignore && setSaved(loaded))
      .catch((err) => !ignore && setError(errorMessage(err, "Could not load your chat models.")));
    return () => {
      ignore = true;
    };
  }, [savedVersion]);

  if (!info) return null;

  const savedInUse = saved.find((m) => sameUrl(m.baseUrl, baseUrl) && m.model === model);
  const serviceInUse = info.providers.find((p) => sameUrl(p.baseUrl, baseUrl));

  async function use(key: string, useBaseUrl: string, useModel: string) {
    setError("");
    setSwitching(key);
    try {
      await updateSetting("chat.base_url", useBaseUrl);
      await updateSetting("chat.model", useModel);
      onChanged();
    } catch (err) {
      setError(errorMessage(err, "Could not switch the chat model. Please try again."));
    } finally {
      setSwitching("");
    }
  }

  async function handleDelete() {
    if (!deleting) return;
    setDeleteBusy(true);
    try {
      await deleteChatModel(deleting.id);
      setSavedVersion((v) => v + 1);
    } catch (err) {
      setError(errorMessage(err, "Could not delete the chat model. Please try again."));
    } finally {
      setDeleteBusy(false);
      setDeleting(null);
    }
  }

  const useButton = (key: string, useBaseUrl: string, useModel: string) => (
    <Button
      variant="secondary"
      size="sm"
      disabled={switching !== "" || (sameUrl(useBaseUrl, baseUrl) && useModel === model)}
      onClick={() => use(key, useBaseUrl, useModel)}
    >
      {switching === key ? "Switching..." : "Use"}
    </Button>
  );

  return (
    <div className="mb-4 rounded-2xl border border-stone-200 bg-white p-6 shadow-sm">
      <p className="font-medium text-stone-900">Switching the chat model</p>
      <div className="mt-1 space-y-1 text-sm text-stone-500">
        <p>
          The chat model is set by three things: the <span className="font-medium text-stone-700">Chat API address</span> and
          the <span className="font-medium text-stone-700">Chat model</span> below, and for an online service its key in{" "}
          <span className="font-mono">{info.apiKeyName}</span> in the server's <span className="font-mono">.env</span> file.
        </p>
        <p>
          To switch, click <span className="font-medium text-stone-700">Use</span> on one of your chat models or a service below
          (or type the address and model in the settings), then put that service's key in <span className="font-mono">.env</span>{" "}
          and restart the server. Ollama needs no key. Models are renamed from time to time, so check the service's list if
          replies fail.
        </p>
      </div>

      <p className="mt-3 text-sm">
        {info.hasApiKey ? (
          <span className="text-emerald-700">✓ {info.apiKeyName} is set</span>
        ) : (
          <span className="text-stone-500">{info.apiKeyName} is not set</span>
        )}
      </p>
      {serviceInUse?.needsApiKey && !info.hasApiKey && (
        <div className="mt-3">
          <Alert message={`The chat uses ${serviceInUse.name}, but ${info.apiKeyName} is not set in .env, so the assistant can't reply.`} />
        </div>
      )}
      {error && (
        <div className="mt-3">
          <Alert message={error} />
        </div>
      )}

      <div className="mt-6 flex items-center justify-between gap-3">
        <p className="text-sm font-semibold text-stone-700">Your chat models</p>
        <Button variant="secondary" size="sm" onClick={() => setForm({ chatModel: null })}>
          Add chat model
        </Button>
      </div>
      {saved.length === 0 ? (
        <p className="mt-2 text-sm text-stone-500">
          None yet. Add a chat model (its address and model) to switch to it later with one click.
        </p>
      ) : (
        <ul className="mt-2 divide-y divide-stone-100">
          {saved.map((m) => (
            <Row
              key={m.id}
              name={m.name}
              inUse={savedInUse === m}
              baseUrl={m.baseUrl}
              model={m.model}
              notes={m.notes}
              actions={
                <>
                  {useButton(m.id, m.baseUrl, m.model)}
                  <Button variant="ghost" size="sm" onClick={() => setForm({ chatModel: m })}>
                    Edit
                  </Button>
                  <Button variant="dangerGhost" size="sm" onClick={() => setDeleting(m)}>
                    Delete
                  </Button>
                </>
              }
            />
          ))}
        </ul>
      )}

      <p className="mt-6 text-sm font-semibold text-stone-700">Services you can use</p>
      <ul className="mt-2 divide-y divide-stone-100">
        {info.providers.map((provider) => (
          <Row
            key={provider.name}
            name={provider.name}
            inUse={!savedInUse && serviceInUse === provider}
            baseUrl={provider.baseUrl}
            model={provider.exampleModel}
            usedModel={serviceInUse === provider && model !== provider.exampleModel ? model : undefined}
            notes={
              <>
                {provider.freeTier}{" "}
                <a href={provider.signUpUrl} target="_blank" rel="noreferrer" className="font-semibold text-brand-600">
                  {provider.needsApiKey ? "Get a key" : "Download"}
                </a>
              </>
            }
            actions={useButton(provider.name, provider.baseUrl, provider.exampleModel)}
          />
        ))}
      </ul>

      {form && (
        <ChatModelForm
          chatModel={form.chatModel}
          initial={{ name: "", baseUrl, model, notes: "" }}
          onSaved={() => {
            setForm(null);
            setSavedVersion((v) => v + 1);
          }}
          onCancel={() => setForm(null)}
        />
      )}

      {deleting && (
        <ConfirmDialog
          title="Delete chat model"
          message={`Delete "${deleting.name}" from your chat models? The chat settings are not changed.`}
          confirmLabel="Delete"
          busy={deleteBusy}
          onConfirm={handleDelete}
          onCancel={() => setDeleting(null)}
        />
      )}
    </div>
  );
}

type RowProps = {
  name: string;
  inUse: boolean;
  baseUrl: string;
  model: string;
  // The model in use with this address, when it isn't the one shown.
  usedModel?: string;
  notes: ReactNode;
  actions: ReactNode;
};

function Row({ name, inUse, baseUrl, model, usedModel, notes, actions }: RowProps) {
  return (
    <li className="grid gap-2 py-4 sm:grid-cols-[1fr_auto] sm:items-start sm:gap-4">
      <div className="min-w-0 space-y-1">
        <p className="font-medium text-stone-800">
          {name}
          {inUse && <span className="ml-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-semibold text-brand-700">In use</span>}
        </p>
        <p className="break-all text-sm text-stone-600">
          Address: <span className="font-mono">{baseUrl}</span>
        </p>
        <p className="break-all text-sm text-stone-600">
          Model: <span className="font-mono">{model}</span>
          {usedModel && <span className="text-stone-400"> (you use {usedModel})</span>}
        </p>
        {notes && <p className="text-sm text-stone-500">{notes}</p>}
      </div>
      <div className="flex flex-wrap gap-2">{actions}</div>
    </li>
  );
}
