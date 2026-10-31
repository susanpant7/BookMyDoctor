import { useEffect, useState } from "react";
import HospitalSettings from "@/features/settings/HospitalSettings";
import SettingForm from "@/features/settings/SettingForm";
import { getSettings, type Setting } from "@/features/settings/settingsApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import PageHeader from "@/shared/components/PageHeader";
import Tabs from "@/shared/components/Tabs";

const tabs = [
  { id: "hospital", label: "Hospital" },
  { id: "ai", label: "AI assistant" }
];

const groupTitles: Record<string, string> = {
  chat: "Chat model",
  embedding: "Embeddings",
  retrieval: "Searching the knowledge"
};

const groupName = (key: string) => key.split(".")[0];

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
                    <p className="break-words rounded-lg bg-stone-50 px-3 py-2 font-mono text-sm text-stone-700">
                      {setting.value === "" ? "(empty)" : setting.value}
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
            setVersion((v) => v + 1);
          }}
          onCancel={() => setEditing(null)}
        />
      )}
    </>
  );
}
