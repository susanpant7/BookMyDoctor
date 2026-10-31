import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "@/features/auth/useAuth";
import { getDoctors, type Doctor } from "@/features/doctors/doctorsApi";
import { getSpecialties, type Specialty } from "@/features/specialties/specialtiesApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Badge from "@/shared/components/Badge";
import { buttonClass } from "@/shared/components/buttonStyles";
import { SelectField } from "@/shared/components/Field";
import PageHeader from "@/shared/components/PageHeader";

// The public list of doctors, for patients and visitors.
export default function DoctorsPage() {
  const { isPatient } = useAuth();
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [specialties, setSpecialties] = useState<Specialty[]>([]);
  const [specialtyId, setSpecialtyId] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    getSpecialties()
      .then(setSpecialties)
      .catch((err) => setError(errorMessage(err, "Could not load the specialties.")));
  }, []);

  useEffect(() => {
    let ignore = false;
    getDoctors(specialtyId || undefined)
      .then((loaded) => {
        if (ignore) return;
        setDoctors(loaded);
        setError("");
      })
      .catch((err) => !ignore && setError(errorMessage(err, "Could not load the doctors.")))
      .finally(() => !ignore && setLoading(false));
    return () => {
      ignore = true;
    };
  }, [specialtyId]);

  return (
    <main className="mx-auto max-w-6xl px-6 py-10">
      <PageHeader
        title="Our doctors"
        description="Specialists you can book at BookMyDoctor Hospital."
        action={
          <div className="w-56">
            <SelectField label="Specialty" value={specialtyId} onChange={(e) => setSpecialtyId(e.target.value)}>
              <option value="">All specialties</option>
              {specialties.map((specialty) => (
                <option key={specialty.id} value={specialty.id}>
                  {specialty.name}
                </option>
              ))}
            </SelectField>
          </div>
        }
      />

      {error && (
        <div className="mt-6">
          <Alert message={error} />
        </div>
      )}

      {loading ? (
        <p className="mt-8 text-center text-sm text-stone-500">Loading doctors...</p>
      ) : doctors.length === 0 && !error ? (
        <p className="mt-8 text-center text-sm text-stone-500">No doctors found.</p>
      ) : (
        <div className="mt-8 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {doctors.map((doctor) => (
            <article key={doctor.id} className="flex flex-col rounded-2xl border border-stone-200 bg-white p-6 shadow-sm">
              <div className="flex items-start gap-3">
                <span className="flex size-11 shrink-0 items-center justify-center rounded-full bg-brand-100 text-base font-semibold text-brand-700">
                  {doctor.name[0]}
                </span>
                <div className="min-w-0">
                  <h2 className="font-semibold text-stone-900">{doctor.name}</h2>
                  <div className="mt-1">
                    <Badge name={doctor.specialty} />
                  </div>
                </div>
              </div>
              <p className="mt-4 line-clamp-3 flex-1 text-sm leading-6 text-stone-600">{doctor.bio}</p>
              <dl className="mt-4 grid grid-cols-2 gap-y-1 text-sm">
                <dt className="text-stone-500">Experience</dt>
                <dd className="text-right text-stone-900">{doctor.experienceYears} years</dd>
                <dt className="text-stone-500">New visit</dt>
                <dd className="text-right text-stone-900">${doctor.consultationFee}</dd>
                <dt className="text-stone-500">Follow-up</dt>
                <dd className="text-right text-stone-900">${doctor.followUpFee}</dd>
                <dt className="text-stone-500">Speaks</dt>
                <dd className="text-right text-stone-900">{doctor.languages.join(", ")}</dd>
              </dl>
            </article>
          ))}
        </div>
      )}

      {isPatient && (
        <div className="mt-10 text-center">
          <Link to="/book" className={buttonClass("primary", "lg")}>
            Book an appointment
          </Link>
        </div>
      )}
    </main>
  );
}
