import { useEffect, useState } from "react";
import HospitalSettings from "@/features/settings/HospitalSettings";
import SettingForm from "@/features/settings/SettingForm";
import { getSettings, restoreDefaultSettings, type Setting } from "@/features/settings/settingsApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import ConfirmDialog from "@/shared/components/ConfirmDialog";
import PageHeader from "@/shared/components/PageHeader";
import Tabs from "@/shared/components/Tabs";

const tabs = [
  { id: "hospital", label: "Hospital" },
  { id: "ai", label: "AI assistant" }
];

const groupTitles: Record<string, string> = {
  assistant: "Assistant behaviour",
  chat: "Chat model",
  embedding: "Embeddings",
  retrieval: "Searching the knowledge"
};

const groupName = (key: string) => key.split(".")[0];

function displayValue(setting: Setting) {
  if (setting.kind === "boolean") return setting.value === "true" ? "On" : "Off";
  return setting.value === "" ? "(empty)" : setting.value;
}

export default function SettingsPage() {
  const [tab, setTab] = useState("hospital");

  return (
    <main className="mx-auto max-w-6xl px-6 py-10">
      <PageHeader
        title="Settings"
        description="The hospital details patients see, and how the assistant searches the knowledge and writes its answers."
      />
      <div className="mt-6 overflow-hidden rounded-2xl border border-stone-200 bg-white shadow-sm">
        <Tabs tabs={tabs} active={tab} onChange={setTab} />
      </div>
      <div className="mt-6">{tab === "hospital" ? <HospitalSettings /> : <AiSettingsList />}</div>
    </main>
  );
}

function AiSettingsList() {
  const [settings, setSettings] = useState<Setting[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [editing, setEditing] = useState<Setting | null>(null);
  const [version, setVersion] = useState(0);
  const [notice, setNotice] = useState("");
  const [confirmingRestore, setConfirmingRestore] = useState(false);
  const [restoring, setRestoring] = useState(false);

  // Restoring a different embedding model or prefix makes the stored knowledge vectors incomparable.
  const embeddingChanges = settings.some((s) => groupName(s.key) === "embedding" && s.value !== s.defaultValue);

  async function restoreDefaults() {
    setError("");
    setNotice("");
    setRestoring(true);
    try {
      setSettings(await restoreDefaultSettings());
      setNotice(
        embeddingChanges
          ? "The default settings are restored. The embedding settings changed, so upload the knowledge files again and click Sync settings on the Knowledge page."
          : "The default settings are restored. They apply from the next patient question."
      );
    } catch (err) {
      setError(errorMessage(err, "Could not restore the default settings. Please try again."));
    } finally {
      setRestoring(false);
      setConfirmingRestore(false);
    }
  }

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

  const groups = [...new Set(settings.map((s) => groupName(s.key)))];

  return (
    <>
      <div className="mb-6 flex justify-end">
        <Button variant="secondary" disabled={loading || restoring} onClick={() => setConfirmingRestore(true)}>
          Restore defaults
        </Button>
      </div>
      {notice && (
        <div className="mb-6">
          <Alert variant="success" message={notice} />
        </div>
      )}
      {error && <Alert message={error} />}
      {loading ? (
        <p className="mt-8 text-center text-sm text-stone-500">Loading settings...</p>
      ) : (
        groups.map((group) => (
          <section key={group} className="mb-8">
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-stone-500">{groupTitles[group] ?? group}</h2>
            <div className="divide-y divide-stone-100 overflow-hidden rounded-2xl border border-stone-200 bg-white shadow-sm">
              {settings
                .filter((s) => groupName(s.key) === group)
                .map((setting) => (
                  <div key={setting.key} className="grid gap-3 px-6 py-5 sm:grid-cols-[1fr_14rem_auto] sm:items-start">
                    <div className="min-w-0">
                      <p className="font-medium text-stone-900">{setting.label}</p>
                      <p className="mt-1 text-sm text-stone-500">{setting.description}</p>
                    </div>
                    <p
                      className={`break-words rounded-lg bg-stone-50 px-3 py-2 font-mono text-sm text-stone-700 ${
                        setting.kind === "longtext" ? "line-clamp-4 whitespace-pre-line" : ""
                      }`}
                    >
                      {displayValue(setting)}
                    </p>
                    <Button variant="ghost" size="sm" onClick={() => setEditing(setting)}>
                      Edit
                    </Button>
                  </div>
                ))}
            </div>
          </section>
        ))
      )}

      {editing && (
        <SettingForm
          key={editing.key}
          setting={editing}
          onSaved={() => {
            setEditing(null);
            setNotice("");
            setVersion((v) => v + 1);
          }}
          onCancel={() => setEditing(null)}
        />
      )}

      {confirmingRestore && (
        <ConfirmDialog
          title="Restore default AI settings"
          message={
            "Set every AI assistant setting back to its default, including the system prompt? Your changes on this page will be lost." +
            (embeddingChanges
              ? " The embedding settings will change too, so the stored knowledge must be uploaded again and synced afterwards."
              : "")
          }
          confirmLabel="Restore defaults"
          busy={restoring}
          busyLabel="Restoring..."
          onConfirm={restoreDefaults}
          onCancel={() => setConfirmingRestore(false)}
        />
      )}
    </>
  );
}
