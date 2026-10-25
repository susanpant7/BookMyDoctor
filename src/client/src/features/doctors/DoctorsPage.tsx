import { useEffect, useState } from "react";
import { getSpecialties, type Specialty } from "../specialties/specialtyApi";
import { getDoctors, type Doctor } from "./doctorApi";

export function DoctorsPage() {
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [specialties, setSpecialties] = useState<Specialty[]>([]);
  const [specialtyId, setSpecialtyId] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getSpecialties()
      .then(setSpecialties)
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "Could not load specialties."));
  }, []);

  useEffect(() => {
    getDoctors(specialtyId || undefined)
      .then(setDoctors)
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "Could not load doctors."));
  }, [specialtyId]);

  if (error) {
    return <p className="error">{error}</p>;
  }

  return (
    <section>
      <h1>Doctors</h1>
      <label className="filter">
        Specialty
        <select value={specialtyId} onChange={(event) => setSpecialtyId(event.target.value)}>
          <option value="">All</option>
          {specialties.map((specialty) => (
            <option key={specialty.id} value={specialty.id}>
              {specialty.name}
            </option>
          ))}
        </select>
      </label>
      <div className="grid">
        {doctors.map((doctor) => (
          <article key={doctor.id} className="card">
            <h2>{doctor.name}</h2>
            <p>{doctor.specialty}</p>
            <p className="muted">
              {doctor.location} · {doctor.experienceYears} years · ${doctor.consultationFee}
            </p>
          </article>
        ))}
      </div>
    </section>
  );
}
