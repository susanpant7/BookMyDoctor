import { useEffect, useState } from "react";
import SpecialtyForm from "@/features/specialties/SpecialtyForm";
import {
  deleteSpecialty,
  getSpecialties,
  loadDefaultSpecialties,
  type Specialty
} from "@/features/specialties/specialtiesApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import ConfirmDialog from "@/shared/components/ConfirmDialog";
import PageHeader from "@/shared/components/PageHeader";

export default function SpecialtiesPage() {
  const [specialties, setSpecialties] = useState<Specialty[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Specialty | null>(null);
  const [deleting, setDeleting] = useState<Specialty | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [loadingDefaults, setLoadingDefaults] = useState(false);
  const [version, setVersion] = useState(0);
  const reload = () => setVersion((v) => v + 1);

  useEffect(() => {
    let ignore = false;
    getSpecialties()
      .then((loaded) => {
        if (ignore) return;
        setSpecialties(loaded);
        setError("");
      })
      .catch((err) => !ignore && setError(errorMessage(err, "Could not load the specialties.")))
      .finally(() => !ignore && setLoading(false));
    return () => {
      ignore = true;
    };
  }, [version]);

  function openForm(specialty: Specialty | null) {
    setEditing(specialty);
    setFormOpen(true);
  }

  async function handleLoadDefaults() {
    setError("");
    setNotice("");
    setLoadingDefaults(true);
    try {
      const { added, skipped } = await loadDefaultSpecialties();
      setNotice(
        added === 0
          ? "The default specialties already exist."
          : `Added ${added} default ${added === 1 ? "specialty" : "specialties"}${skipped > 0 ? ` (${skipped} already existed)` : ""}.`
      );
      reload();
    } catch (err) {
      setError(errorMessage(err, "Could not load the default specialties."));
    } finally {
      setLoadingDefaults(false);
    }
  }

  async function confirmDelete() {
    if (!deleting) return;
    setDeleteBusy(true);
    setError("");
    try {
      await deleteSpecialty(deleting.id);
      reload();
    } catch (err) {
      setError(errorMessage(err, "Could not delete the specialty."));
    } finally {
      setDeleteBusy(false);
      setDeleting(null);
    }
  }

  return (
    <main className="mx-auto max-w-6xl px-6 py-10">
      <PageHeader
        title="Specialties"
        description="The kinds of care the hospital offers. Every doctor belongs to one."
        action={
          <div className="flex gap-3">
            <Button variant="secondary" onClick={handleLoadDefaults} disabled={loadingDefaults}>
              {loadingDefaults ? "Loading..." : "Load default specialties"}
            </Button>
            <Button onClick={() => openForm(null)}>Add specialty</Button>
          </div>
        }
      />

      <div className="mt-6 space-y-3">
        {notice && <Alert variant="success" message={notice} />}
        {error && <Alert message={error} />}
      </div>

      <div className="mt-8 overflow-hidden rounded-2xl border border-stone-200 bg-white shadow-sm">
        {loading ? (
          <p className="px-6 py-12 text-center text-sm text-stone-500">Loading specialties...</p>
        ) : specialties.length === 0 ? (
          <p className="px-6 py-12 text-center text-sm text-stone-500">No specialties yet.</p>
        ) : (
          <table className="w-full text-left text-sm">
            <thead className="bg-stone-50 text-xs uppercase tracking-wide text-stone-500">
              <tr>
                <th className="px-6 py-3 font-medium">Specialty</th>
                <th className="hidden px-6 py-3 font-medium md:table-cell">Description</th>
                <th className="px-6 py-3 font-medium">Doctors</th>
                <th className="px-6 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-stone-100">
              {specialties.map((specialty) => (
                <tr key={specialty.id} className="hover:bg-stone-50/60">
                  <td className="px-6 py-4 font-medium text-stone-900">{specialty.name}</td>
                  <td className="hidden px-6 py-4 text-stone-600 md:table-cell">{specialty.description}</td>
                  <td className="px-6 py-4 text-stone-600">{specialty.doctorCount}</td>
                  <td className="px-6 py-4">
                    <div className="flex justify-end gap-1">
                      <Button variant="ghost" size="sm" onClick={() => openForm(specialty)}>
                        Edit
                      </Button>
                      <Button variant="dangerGhost" size="sm" onClick={() => setDeleting(specialty)}>
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
        <SpecialtyForm
          key={editing?.id ?? "new"}
          specialty={editing}
          onSaved={() => {
            setFormOpen(false);
            reload();
          }}
          onCancel={() => setFormOpen(false)}
        />
      )}

      {deleting && (
        <ConfirmDialog
          title="Delete specialty"
          message={`Are you sure you want to delete ${deleting.name}? This cannot be undone.`}
          confirmLabel="Delete"
          busy={deleteBusy}
          onConfirm={confirmDelete}
          onCancel={() => setDeleting(null)}
        />
      )}
    </main>
  );
}
