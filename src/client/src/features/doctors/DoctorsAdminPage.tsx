import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import DoctorForm from "@/features/doctors/DoctorForm";
import { deleteDoctor, getDoctors, loadDefaultDoctors, type Doctor } from "@/features/doctors/doctorsApi";
import { getSpecialties, type Specialty } from "@/features/specialties/specialtiesApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Badge from "@/shared/components/Badge";
import Button from "@/shared/components/Button";
import ConfirmDialog from "@/shared/components/ConfirmDialog";
import PageHeader from "@/shared/components/PageHeader";

// The doctors page for the admin: add, edit and remove doctors and their weekly hours.
export default function DoctorsAdminPage() {
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [specialties, setSpecialties] = useState<Specialty[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Doctor | null>(null);
  const [deleting, setDeleting] = useState<Doctor | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [loadingDefaults, setLoadingDefaults] = useState(false);
  const [version, setVersion] = useState(0);
  const reload = () => setVersion((v) => v + 1);

  useEffect(() => {
    let ignore = false;
    Promise.all([getDoctors(), getSpecialties()])
      .then(([loadedDoctors, loadedSpecialties]) => {
        if (ignore) return;
        setDoctors(loadedDoctors);
        setSpecialties(loadedSpecialties);
        setError("");
      })
      .catch((err) => !ignore && setError(errorMessage(err, "Could not load the doctors.")))
      .finally(() => !ignore && setLoading(false));
    return () => {
      ignore = true;
    };
  }, [version]);

  function openForm(doctor: Doctor | null) {
    setEditing(doctor);
    setFormOpen(true);
  }

  async function handleLoadDefaults() {
    setError("");
    setNotice("");
    setLoadingDefaults(true);
    try {
      const { added, skipped } = await loadDefaultDoctors();
      setNotice(
        added === 0
          ? "The default doctors already exist."
          : `Added ${added} default ${added === 1 ? "doctor" : "doctors"}${skipped > 0 ? ` (${skipped} already existed)` : ""}.`
      );
      reload();
    } catch (err) {
      setError(errorMessage(err, "Could not load the default doctors."));
    } finally {
      setLoadingDefaults(false);
    }
  }

  async function confirmDelete() {
    if (!deleting) return;
    setDeleteBusy(true);
    setError("");
    try {
      await deleteDoctor(deleting.id);
      reload();
    } catch (err) {
      setError(errorMessage(err, "Could not delete the doctor."));
    } finally {
      setDeleteBusy(false);
      setDeleting(null);
    }
  }

  const noSpecialties = !loading && specialties.length === 0;

  return (
    <main className="mx-auto max-w-6xl px-6 py-10">
      <PageHeader
        title="Doctors"
        description="Add, edit and remove doctors, and set the hours patients can book them."
        action={
          <div className="flex gap-3">
            <Button variant="secondary" onClick={handleLoadDefaults} disabled={loadingDefaults || noSpecialties}>
              {loadingDefaults ? "Loading..." : "Load default doctors"}
            </Button>
            <Button onClick={() => openForm(null)} disabled={noSpecialties}>
              Add doctor
            </Button>
          </div>
        }
      />

      <div className="mt-6 space-y-3">
        {noSpecialties && (
          <Alert
            variant="warning"
            message={
              <>
                Doctors belong to a specialty, so create the specialties first on the{" "}
                <Link to="/specialties" className="font-semibold underline">
                  Specialties page
                </Link>
                .
              </>
            }
          />
        )}
        {notice && <Alert variant="success" message={notice} />}
        {error && <Alert message={error} />}
      </div>

      <div className="mt-8 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        {loading ? (
          <p className="px-6 py-12 text-center text-sm text-slate-500">Loading doctors...</p>
        ) : doctors.length === 0 ? (
          <p className="px-6 py-12 text-center text-sm text-slate-500">No doctors yet.</p>
        ) : (
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-6 py-3 font-medium">Doctor</th>
                <th className="px-6 py-3 font-medium">Specialty</th>
                <th className="hidden px-6 py-3 font-medium md:table-cell">Fees</th>
                <th className="hidden px-6 py-3 font-medium lg:table-cell">Languages</th>
                <th className="px-6 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {doctors.map((doctor) => (
                <tr key={doctor.id} className="hover:bg-slate-50/60">
                  <td className="px-6 py-4">
                    <p className="font-medium text-slate-900">{doctor.name}</p>
                    <p className="text-slate-500">{doctor.experienceYears} years of experience</p>
                  </td>
                  <td className="px-6 py-4">
                    <Badge name={doctor.specialty} />
                  </td>
                  <td className="hidden px-6 py-4 text-slate-600 md:table-cell">
                    ${doctor.consultationFee} / ${doctor.followUpFee}
                  </td>
                  <td className="hidden px-6 py-4 text-slate-600 lg:table-cell">{doctor.languages.join(", ")}</td>
                  <td className="px-6 py-4">
                    <div className="flex justify-end gap-1">
                      <Button variant="ghost" size="sm" onClick={() => openForm(doctor)}>
                        Edit
                      </Button>
                      <Button variant="dangerGhost" size="sm" onClick={() => setDeleting(doctor)}>
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
        <DoctorForm
          key={editing?.id ?? "new"}
          doctor={editing}
          specialties={specialties}
          onSaved={() => {
            setFormOpen(false);
            reload();
          }}
          onCancel={() => setFormOpen(false)}
        />
      )}

      {deleting && (
        <ConfirmDialog
          title="Delete doctor"
          message={`Are you sure you want to delete ${deleting.name}? Their weekly hours are deleted too. A doctor who has appointments cannot be deleted.`}
          confirmLabel="Delete"
          busy={deleteBusy}
          onConfirm={confirmDelete}
          onCancel={() => setDeleting(null)}
        />
      )}
    </main>
  );
}
