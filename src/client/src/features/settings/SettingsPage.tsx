import { useEffect, useState } from "react";
import { getChunks } from "@/features/knowledge/knowledgeApi";
import HospitalSettings from "@/features/settings/HospitalSettings";
import ModelList, { type ModelInUse } from "@/features/settings/ModelList";
import SettingForm from "@/features/settings/SettingForm";
import ManageAppData from "@/features/settings/ManageAppData";
import { getSettings, type Setting } from "@/features/settings/settingsApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import PageHeader from "@/shared/components/PageHeader";
import Tabs from "@/shared/components/Tabs";

const tabs = [
  { id: "hospital", label: "Hospital" },
  { id: "ai", label: "AI assistant" },
  { id: "data", label: "Manage App Data" }
];

// The AI tab shows one group at a time, so the page stays short. These are the names on the buttons.
const groupTitles: Record<string, string> = {
  assistant: "Assistant",
  chat: "Chat model",
  embedding: "Embeddings",
  retrieval: "Searching"
};

const groupName = (key: string) => key.split(".")[0];

// Set with Select in the Chat models and Embedding models lists, so they are not shown as rows.
const setByModelLists = new Set([
  "chat.base_url",
  "chat.model",
  "chat.api_key_name",
  "embedding.base_url",
  "embedding.api_key_name",
  "embedding.model",
  "embedding.query_prefix",
  "embedding.document_prefix"
]);

function displayValue(setting: Setting) {
  if (setting.kind === "boolean") return setting.value === "true" ? "On" : "Off";
  return setting.value === "" ? "(empty)" : setting.value;
}

export default function SettingsPage() {
  const [tab, setTab] = useState("hospital");
  // null until known; the knowledge is only counted to warn that there is none.
  const [knowledgeCount, setKnowledgeCount] = useState<number | null>(null);
  const [knowledgeVersion, setKnowledgeVersion] = useState(0);
  // Whether a chat model and an embedding model are selected (null until known). A setting that is missing counts as not selected.
  const [selected, setSelected] = useState<{ chat: boolean; embedding: boolean } | null>(null);
  const knowledgeChanged = () => setKnowledgeVersion((v) => v + 1);

  useEffect(() => {
    let ignore = false;
    getChunks()
      .then((chunks) => !ignore && setKnowledgeCount(chunks.length))
      .catch(() => !ignore && setKnowledgeCount(null));
    getSettings()
      .then((all) => {
        const has = (key: string) => (all.find((s) => s.key === key)?.value ?? "") !== "";
        if (!ignore) setSelected({ chat: has("chat.model"), embedding: has("embedding.model") });
      })
      .catch(() => !ignore && setSelected(null));
    return () => {
      ignore = true;
    };
  }, [knowledgeVersion]);

  return (
    <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6 sm:py-10">
      {selected && (!selected.chat || !selected.embedding) && (
        <div className="mb-6 space-y-3">
          {!selected.chat && (
            <Alert
              variant="warning"
              message="No chat model is selected, so the assistant cannot answer. Choose one on the AI assistant tab (Chat model)."
            />
          )}
          {!selected.embedding && (
            <Alert
              variant="warning"
              message="No embedding model is selected, so the assistant cannot search the knowledge. Choose one on the AI assistant tab (Embeddings)."
            />
          )}
        </div>
      )}
      {knowledgeCount === 0 && (
        <div role="alert" className="mb-6 rounded-xl bg-red-600 px-4 py-3 text-sm font-medium text-white shadow-sm">
          {selected?.embedding === false
            ? "The app does not have any knowledge files. Select an embedding model to load the knowledge data."
            : "The app does not have any knowledge files. Load them with Manage App Data (Knowledge files)."}
        </div>
      )}
      <PageHeader
        title="Settings"
        description="The hospital details patients see, and how the assistant searches the knowledge and writes its answers."
      />
      <div className="mt-6 overflow-hidden rounded-2xl border border-stone-200 bg-white shadow-sm">
        <Tabs fill tabs={tabs} active={tab} onChange={setTab} />
      </div>
      <div className="mt-6">
        {tab === "hospital" ? (
          <HospitalSettings />
        ) : tab === "ai" ? (
          <AiSettingsList onKnowledgeChanged={knowledgeChanged} />
        ) : (
          <ManageAppData onChanged={knowledgeChanged} />
        )}
      </div>
    </main>
  );
}

function AiSettingsList({ onKnowledgeChanged }: { onKnowledgeChanged: () => void }) {
  const [settings, setSettings] = useState<Setting[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [editing, setEditing] = useState<Setting | null>(null);
  const [version, setVersion] = useState(0);
  const [section, setSection] = useState("chat");
  useEffect(() => {
    let ignore = false;
    getSettings()
      .then((loaded) => {
        if (ignore) return;
        setSettings(loaded);
        setError("");
      })
      .catch((err) => !ignore && setError(errorMessage(err, "Could not load the settings.")))
      .finally(() => !ignore && setLoading(false));
    return () => {
      ignore = true;
    };
  }, [version]);

  // Switching the model may have changed the knowledge.
  useEffect(() => {
    onKnowledgeChanged();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [version]);

  const groups = [...new Set(settings.map((s) => groupName(s.key)))];
  const current = groups.includes(section) ? section : (groups[0] ?? "chat");
  const value = (key: string) => settings.find((s) => s.key === key)?.value ?? "";
  const inUse: Record<string, ModelInUse> = {
    chat: {
      baseUrl: value("chat.base_url"),
      model: value("chat.model"),
      apiKeyName: value("chat.api_key_name"),
      queryPrefix: "",
      documentPrefix: ""
    },
    embedding: {
      baseUrl: value("embedding.base_url"),
      model: value("embedding.model"),
      apiKeyName: value("embedding.api_key_name"),
      queryPrefix: value("embedding.query_prefix"),
      documentPrefix: value("embedding.document_prefix")
    }
  };

  return (
    <>
      <div className="space-y-3">
        {notice && <Alert variant="success" message={notice} />}
        {error && <Alert message={error} />}
      </div>
      {loading ? (
        <p className="mt-8 text-center text-sm text-stone-500">Loading settings...</p>
      ) : settings.length === 0 && !error ? (
        <p className="mt-8 text-center text-sm text-stone-500">
          No AI settings are saved yet. Load them on the Manage App Data tab; until then the assistant uses the built-in defaults.
        </p>
      ) : (
        <>
          <nav aria-label="Parts of the AI assistant settings" className="flex flex-wrap gap-2">
            {groups.map((group) => (
              <button
                key={group}
                type="button"
                aria-current={group === current ? "page" : undefined}
                onClick={() => setSection(group)}
                className={`rounded-full px-4 py-1.5 text-sm font-medium transition ${
                  group === current
                    ? "bg-brand-600 text-white shadow-sm"
                    : "bg-white text-stone-600 ring-1 ring-stone-200 hover:bg-stone-50 hover:text-stone-900"
                }`}
              >
                {groupTitles[group] ?? group}
                {(group === "chat" || group === "embedding") && !value(`${group}.model`) && (
                  <span className="ml-2 inline-block size-2 rounded-full bg-amber-400" title="No model is selected" />
                )}
              </button>
            ))}
          </nav>

          <section className="mt-4 space-y-4">
            {(current === "chat" || current === "embedding") && (
              <ModelList kind={current} current={inUse[current]} onChanged={() => setVersion((v) => v + 1)} />
            )}
            {settings.some((s) => groupName(s.key) === current && !setByModelLists.has(s.key)) && (
              <div className="divide-y divide-stone-100 overflow-hidden rounded-2xl border border-stone-200 bg-white shadow-sm">
                {settings
                  .filter((s) => groupName(s.key) === current && !setByModelLists.has(s.key))
                  .map((setting) => (
                    <div key={setting.key} className="grid gap-2 px-4 py-3 sm:px-5 lg:grid-cols-[1fr_16rem_auto] lg:items-center">
                      <div className="min-w-0">
                        <p className="font-medium text-stone-900">{setting.label}</p>
                        <p className="mt-0.5 line-clamp-2 text-sm text-stone-500" title={setting.description}>
                          {setting.description}
                        </p>
                      </div>
                      <p
                        className={`break-words rounded-lg bg-stone-50 px-3 py-2 font-mono text-sm text-stone-700 ${
                          setting.kind === "longtext" ? "line-clamp-4 whitespace-pre-line" : ""
                        }`}
                      >
                        {displayValue(setting)}
                      </p>
                      <div>
                        <Button variant="ghost" size="sm" onClick={() => setEditing(setting)}>
                          Edit
                        </Button>
                      </div>
                    </div>
                  ))}
              </div>
            )}
          </section>
        </>
      )}

      {editing && (
        <SettingForm
          key={editing.key}
          setting={editing}
          onSaved={() => {
            setNotice(`Saved "${editing.label}". It applies from the next message.`);
            setEditing(null);
            setVersion((v) => v + 1);
          }}
          onCancel={() => setEditing(null)}
        />
      )}

    </>
  );
}
