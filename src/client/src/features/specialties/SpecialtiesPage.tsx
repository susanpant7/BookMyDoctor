import { useEffect, useState } from "react";
import SpecialtyForm from "@/features/specialties/SpecialtyForm";
import {
  deleteSpecialty,
  getSpecialties,
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
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Specialty | null>(null);
  const [deleting, setDeleting] = useState<Specialty | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
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
    <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6 sm:py-10">
      <PageHeader
        title="Specialties"
        description="The kinds of care the hospital offers. Every doctor belongs to one."
        action={<Button onClick={() => openForm(null)}>Add specialty</Button>}
      />

      <div className="mt-6 space-y-3">
        {error && <Alert message={error} />}
      </div>

      <div className="mt-8 overflow-hidden rounded-2xl border border-stone-200 bg-white shadow-sm">
        {loading ? (
          <p className="px-6 py-12 text-center text-sm text-stone-500">Loading specialties...</p>
        ) : specialties.length === 0 ? (
          <p className="px-6 py-12 text-center text-sm text-stone-500">No specialties yet. Add one, or load the defaults in Settings → Manage App Data.</p>
        ) : (
          <table className="w-full text-left text-sm">
            <thead className="bg-stone-50 text-xs uppercase tracking-wide text-stone-500">
              <tr>
                <th className="px-4 py-3 sm:px-6 font-medium">Specialty</th>
                <th className="hidden px-4 py-3 sm:px-6 font-medium md:table-cell">Description</th>
                <th className="hidden px-4 py-3 sm:px-6 font-medium sm:table-cell">Doctors</th>
                <th className="px-4 py-3 sm:px-6" />
              </tr>
            </thead>
            <tbody className="divide-y divide-stone-100">
              {specialties.map((specialty) => (
                <tr key={specialty.id} className="hover:bg-stone-50/60">
                  <td className="px-4 py-4 sm:px-6">
                    <p className="font-medium text-stone-900">{specialty.name}</p>
                    <p className="text-stone-500 sm:hidden">
                      {specialty.doctorCount} {specialty.doctorCount === 1 ? "doctor" : "doctors"}
                    </p>
                  </td>
                  <td className="hidden px-4 py-4 sm:px-6 text-stone-600 md:table-cell">{specialty.description}</td>
                  <td className="hidden px-4 py-4 sm:px-6 text-stone-600 sm:table-cell">{specialty.doctorCount}</td>
                  <td className="px-4 py-4 sm:px-6">
                    <div className="flex flex-col items-end gap-1 sm:flex-row sm:justify-end">
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
