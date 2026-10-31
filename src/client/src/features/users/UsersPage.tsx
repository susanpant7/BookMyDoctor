import { useEffect, useState } from "react";
import LoadDefaultsForm from "@/features/users/LoadDefaultsForm";
import UserForm from "@/features/users/UserForm";
import { deletePatient, getPatients, type LoadDefaultsResult, type Patient } from "@/features/users/usersApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import ConfirmDialog from "@/shared/components/ConfirmDialog";
import PageHeader from "@/shared/components/PageHeader";

export default function UsersPage() {
  const [patients, setPatients] = useState<Patient[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Patient | null>(null);
  const [deleting, setDeleting] = useState<Patient | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [loadDefaultsOpen, setLoadDefaultsOpen] = useState(false);
  const [version, setVersion] = useState(0);
  const reload = () => setVersion((v) => v + 1);

  useEffect(() => {
    let ignore = false;
    getPatients()
      .then((loaded) => {
        if (ignore) return;
        setPatients(loaded);
        setError("");
      })
      .catch((err) => !ignore && setError(errorMessage(err, "Could not load the patients.")))
      .finally(() => !ignore && setLoading(false));
    return () => {
      ignore = true;
    };
  }, [version]);

  function openForm(patient: Patient | null) {
    setEditing(patient);
    setFormOpen(true);
  }

  async function confirmDelete() {
    if (!deleting) return;
    setDeleteBusy(true);
    setError("");
    try {
      await deletePatient(deleting.id);
      reload();
    } catch (err) {
      setError(errorMessage(err, "Could not delete the patient."));
    } finally {
      setDeleteBusy(false);
      setDeleting(null);
    }
  }

  function handleDefaultsLoaded({ added, skipped }: LoadDefaultsResult) {
    setLoadDefaultsOpen(false);
    setNotice(
      added === 0
        ? "The demo patients already exist."
        : `Added ${added} demo ${added === 1 ? "patient" : "patients"}${skipped > 0 ? ` (${skipped} already existed)` : ""}.`
    );
    reload();
  }

  return (
    <main className="mx-auto max-w-6xl px-6 py-10">
      <PageHeader
        title="Patients"
        description="The people who can sign in to book appointments and chat with the assistant."
        action={
          <div className="flex gap-3">
            <Button variant="secondary" onClick={() => setLoadDefaultsOpen(true)}>
              Load demo patients
            </Button>
            <Button onClick={() => openForm(null)}>Add patient</Button>
          </div>
        }
      />

      <div className="mt-6 space-y-3">
        {notice && <Alert variant="success" message={notice} />}
        {error && <Alert message={error} />}
      </div>

      <div className="mt-8 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        {loading ? (
          <p className="px-6 py-12 text-center text-sm text-slate-500">Loading patients...</p>
        ) : patients.length === 0 ? (
          <p className="px-6 py-12 text-center text-sm text-slate-500">No patients yet. Patients can also register themselves.</p>
        ) : (
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-6 py-3 font-medium">Patient</th>
                <th className="px-6 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {patients.map((patient) => (
                <tr key={patient.id} className="hover:bg-slate-50/60">
                  <td className="px-6 py-4">
                    <div className="flex items-center gap-3">
                      <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-indigo-100 text-sm font-semibold text-indigo-700">
                        {patient.name[0]}
                      </span>
                      <div className="min-w-0">
                        <p className="font-medium text-slate-900">{patient.name}</p>
                        <p className="truncate text-slate-500">{patient.email}</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex justify-end gap-1">
                      <Button variant="ghost" size="sm" onClick={() => openForm(patient)}>
                        Edit
                      </Button>
                      <Button variant="dangerGhost" size="sm" onClick={() => setDeleting(patient)}>
                        Delete
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {formOpen && (
        <UserForm
          key={editing?.id ?? "new"}
          patient={editing}
          onSaved={() => {
            setFormOpen(false);
            reload();
          }}
          onCancel={() => setFormOpen(false)}
        />
      )}

      {loadDefaultsOpen && <LoadDefaultsForm onLoaded={handleDefaultsLoaded} onCancel={() => setLoadDefaultsOpen(false)} />}

      {deleting && (
        <ConfirmDialog
          title="Delete patient"
          message={`Are you sure you want to delete ${deleting.name} (${deleting.email})? Their chat history is deleted too, and this cannot be undone.`}
          confirmLabel="Delete"
          busy={deleteBusy}
          onConfirm={confirmDelete}
          onCancel={() => setDeleting(null)}
        />
      )}
    </main>
  );
}
