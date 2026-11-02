import { useEffect, useState } from "react";
import { Check, Circle, LoaderCircle, X } from "lucide-react";
import { loadDefaultDoctors } from "@/features/doctors/doctorsApi";
import { loadDefaultKnowledge } from "@/features/knowledge/knowledgeApi";
import { cleanUpData, restoreDefaultHospital, restoreDefaultSettings } from "@/features/settings/settingsApi";
import { loadDefaultSpecialties } from "@/features/specialties/specialtiesApi";
import { loadDefaultPatients } from "@/features/users/usersApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import ConfirmDialog from "@/shared/components/ConfirmDialog";

type Added = { added: number; skipped: number };

// A failure whose message is written for the admin (unlike other errors, which errorMessage hides).
class StepError extends Error {}

const failureText = (err: unknown, fallback: string) => (err instanceof StepError ? err.message : errorMessage(err, fallback));

// One kind of default data. `run` loads it and returns what to tell the admin.
type Module = {
  id: string;
  title: string;
  description: string;
  button: string;
  // Replaces what is there (asks first), instead of only adding what is missing.
  replaces?: boolean;
  // Asks for a password the loaded accounts sign in with, starting with this one.
  password?: { label: string; initial: string };
  // Left out of "Load all default data" (loaded on its own, later).
  separate?: boolean;
  run: (password: string) => Promise<string>;
};

const addedText = ({ added, skipped }: Added, one: string, many: string) =>
  added === 0
    ? `The default ${many} already exist.`
    : `Added ${added} default ${added === 1 ? one : many}${skipped > 0 ? ` (${skipped} already existed)` : ""}.`;

const modules: Module[] = [
  {
    id: "hospital",
    title: "Hospital details",
    description: "Contact details, opening hours, parking, facilities, test prices and public holidays. Replaces the current details.",
    button: "Load default hospital data",
    replaces: true,
    run: async () => {
      await restoreDefaultHospital();
      return "The default hospital details are loaded.";
    }
  },
  {
    id: "ai",
    title: "AI assistant settings",
    description:
      "The system prompt and the search settings. Replaces every setting; your saved models are kept. No chat or embedding model is selected: they are chosen on the Settings page. If an embedding model was selected, the stored knowledge is deleted.",
    button: "Load default AI assistant data",
    replaces: true,
    run: async () => {
      await restoreDefaultSettings();
      return "The default AI assistant settings are loaded.";
    }
  },
  {
    id: "specialties",
    title: "Specialties",
    description: "Cardiology, Dermatology, Pediatrics, Orthopedics and General Practice. Ones that already exist are skipped.",
    button: "Load default specialties",
    run: async () => addedText(await loadDefaultSpecialties(), "specialty", "specialties")
  },
  {
    id: "doctors",
    title: "Doctors",
    description:
      "One doctor per default specialty, with fees and weekly hours. Each signs in with their username (muturog, chhaalarog, baalrog, haadjorni, saamanya) and the password below. Needs the default specialties first.",
    button: "Load default doctors",
    password: { label: "Password for the default doctors", initial: "doctor" },
    run: async (password) => addedText(await loadDefaultDoctors(password), "doctor", "doctors")
  },
  {
    id: "patients",
    title: "Demo patients",
    description: "Hari Bahadur (hb@gmail.com) and Madan Bahadur (mb@gmail.com), who sign in with the password below.",
    button: "Load demo patients",
    password: { label: "Password for the demo patients", initial: "user" },
    run: async (password) => addedText(await loadDefaultPatients(password), "demo patient", "demo patients")
  },
  {
    id: "knowledge",
    title: "Knowledge files",
    description:
      "The bundled Markdown files the assistant answers from (booking, payments, FAQ and more), and the sections made from the doctors, specialties and hospital details. Needs an embedding model selected on the Settings page.",
    button: "Load default knowledge files",
    separate: true,
    run: async () => {
      const results = await loadDefaultKnowledge();
      // Usually every file fails for the same reason (e.g. the embedding service not running), so each reason is shown once.
      const reasons = [...new Set(results.map((result) => result.error).filter(Boolean))];
      if (reasons.length > 0) {
        const failed = results.filter((result) => result.error).length;
        throw new StepError(`${failed} of ${results.length} knowledge files were not loaded. ${reasons.join(" ")}`);
      }
      return `Loaded ${results.length} knowledge files.`;
    }
  }
];

type StepState = "waiting" | "running" | "done" | "failed";
type Step = { id: string; title: string; state: StepState };

const loadAllModules = modules.filter((m) => !m.separate);
const allSteps = [{ id: "cleanup", title: "Clean up all data" }, ...loadAllModules.map((m) => ({ id: m.id, title: m.title }))];

// Loading and clearing the default data for every module, in one place.
export default function ManageAppData({ onChanged }: { onChanged: () => void }) {
  const [passwords, setPasswords] = useState<Record<string, string>>(() =>
    Object.fromEntries(modules.filter((m) => m.password).map((m) => [m.id, m.password!.initial]))
  );
  const passwordFor = (module: Module) => passwords[module.id] ?? "";
  const missingPassword = (module: Module) => module.password !== undefined && passwordFor(module).length === 0;
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [steps, setSteps] = useState<Step[]>([]);
  const [confirming, setConfirming] = useState<"all" | "cleanup" | Module | null>(null);

  // Cleaning up or loading may have changed the knowledge.
  useEffect(() => {
    if (!busy) onChanged();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [busy]);

  async function runOne(module: Module) {
    setConfirming(null);
    setError("");
    setNotice("");
    setSteps([]);
    setBusy(true);
    try {
      setNotice(await module.run(passwordFor(module)));
    } catch (err) {
      setError(failureText(err, `Could not load the ${module.title.toLowerCase()}. Please try again.`));
    } finally {
      setBusy(false);
    }
  }

  async function cleanUp() {
    setConfirming(null);
    setError("");
    setNotice("");
    setSteps([]);
    setBusy(true);
    try {
      await cleanUpData();
      setNotice("All data is cleaned up.");
    } catch (err) {
      setError(errorMessage(err, "Could not clean up the data. Please try again."));
    } finally {
      setBusy(false);
    }
  }

  // Cleans up, then loads every module in order, stopping at the first one that fails.
  async function loadAll() {
    setConfirming(null);
    setError("");
    setNotice("");
    setBusy(true);
    const progress: Step[] = allSteps.map((step) => ({ ...step, state: "waiting" }));
    const mark = (index: number, state: StepState) => {
      progress[index] = { ...progress[index], state };
      setSteps([...progress]);
    };
    setSteps([...progress]);

    const actions = [() => cleanUpData(), ...loadAllModules.map((module) => () => module.run(passwordFor(module)))];
    for (const [index, action] of actions.entries()) {
      mark(index, "running");
      try {
        await action();
        mark(index, "done");
      } catch (err) {
        mark(index, "failed");
        setError(failureText(err, `${progress[index].title} did not work. Please try again.`));
        setBusy(false);
        return;
      }
    }
    setNotice(
      "The default data is loaded, without the knowledge files. No chat or embedding model is selected: choose them on the Settings page (AI assistant tab). After an embedding model is selected, load the knowledge files on the Knowledge page or below."
    );
    setBusy(false);
  }

  return (
    <div className="space-y-6">
      <section className="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm sm:p-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0 max-w-2xl">
            <h2 className="font-semibold text-stone-900">Load all default data</h2>
            <p className="mt-1 text-sm text-stone-500">
              Cleans up all data first, then loads the default hospital details, AI assistant settings, specialties, doctors
              and demo patients. The knowledge files and the chat and embedding models are not loaded: they come later, after
              a model is selected.
            </p>
          </div>
          <Button disabled={busy || modules.some(missingPassword)} onClick={() => setConfirming("all")}>
            Load all default data
          </Button>
        </div>

        {steps.length > 0 && (
          <ol className="mt-5 space-y-2">
            {steps.map((step) => (
              <li key={step.id} className="flex items-center gap-2 text-sm text-stone-700">
                <StepIcon state={step.state} />
                {step.title}
              </li>
            ))}
          </ol>
        )}
      </section>

      {notice && <Alert variant="success" message={notice} />}
      {error && <Alert message={error} />}

      <section>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-stone-500">Load default data for one part</h2>
        <div className="divide-y divide-stone-100 overflow-hidden rounded-2xl border border-stone-200 bg-white shadow-sm">
          {modules.map((module) => (
            <div key={module.id} className="grid gap-3 px-6 py-5 sm:grid-cols-[1fr_auto] sm:items-center">
              <div className="min-w-0">
                <p className="font-medium text-stone-900">{module.title}</p>
                <p className="mt-1 text-sm text-stone-500">{module.description}</p>
                {module.password && (
                  <label className="mt-3 flex max-w-xs flex-col gap-1 text-sm font-medium text-stone-700">
                    {module.password.label}
                    <input
                      type="text"
                      autoComplete="off"
                      value={passwordFor(module)}
                      onChange={(e) => setPasswords({ ...passwords, [module.id]: e.target.value })}
                      className="rounded-lg border-0 px-3 py-2 text-sm font-normal shadow-sm ring-1 ring-inset ring-stone-300"
                    />
                  </label>
                )}
              </div>
              <Button
                variant="secondary"
                disabled={busy || missingPassword(module)}
                onClick={() => (module.replaces ? setConfirming(module) : void runOne(module))}
              >
                {module.button}
              </Button>
            </div>
          ))}
        </div>
      </section>

      <section className="rounded-2xl border border-red-200 bg-white p-4 shadow-sm sm:p-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0 max-w-2xl">
            <h2 className="font-semibold text-red-700">Clean up data</h2>
            <p className="mt-1 text-sm text-stone-500">
              Deletes all data, as on a fresh start. Your saved chat and embedding models are kept, and you stay signed in as
              the admin.
            </p>
          </div>
          <Button variant="danger" disabled={busy} onClick={() => setConfirming("cleanup")}>
            Clean up data
          </Button>
        </div>
      </section>

      {confirming === "all" && (
        <ConfirmDialog
          title="Load all default data"
          message="This first deletes everything (specialties, doctors, patients, appointments, chats, knowledge files, hospital details and AI settings), then loads the defaults (without the knowledge files and without a chat or embedding model). Your own changes cannot be recovered. Patients who registered themselves will have to register again."
          confirmLabel="Load all default data"
          busy={busy}
          busyLabel="Loading..."
          onConfirm={loadAll}
          onCancel={() => setConfirming(null)}
        />
      )}

      {confirming === "cleanup" && (
        <ConfirmDialog
          title="Clean up all data"
          message="Delete everything: specialties, doctors, patients, appointments, chats, knowledge files, the hospital details and the AI settings? Your saved chat and embedding models are kept. This cannot be undone. Patients will have to register again."
          confirmLabel="Clean up data"
          busy={busy}
          busyLabel="Cleaning up..."
          onConfirm={cleanUp}
          onCancel={() => setConfirming(null)}
        />
      )}

      {confirming !== null && typeof confirming === "object" && (
        <ConfirmDialog
          title={confirming.button}
          message={`Replace the current ${confirming.title.toLowerCase()} with the defaults? Your changes will be lost, and the assistant uses the defaults from the next question.`}
          confirmLabel={confirming.button}
          busy={busy}
          busyLabel="Loading..."
          onConfirm={() => runOne(confirming)}
          onCancel={() => setConfirming(null)}
        />
      )}
    </div>
  );
}

function StepIcon({ state }: { state: StepState }) {
  switch (state) {
    case "running":
      return <LoaderCircle className="size-4 animate-spin text-brand-600" />;
    case "done":
      return <Check className="size-4 text-emerald-600" />;
    case "failed":
      return <X className="size-4 text-red-600" />;
    default:
      return <Circle className="size-4 text-stone-300" />;
  }
}
