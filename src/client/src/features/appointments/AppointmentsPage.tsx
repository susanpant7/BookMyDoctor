import { CalendarPlus } from "lucide-react";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useSession } from "../../shared/session";
import { getDoctors, type Doctor } from "../doctors/doctorApi";
import { bookingReference, getMyAppointments, type Appointment } from "./appointmentApi";

export function AppointmentsPage() {
  const { session } = useSession();
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!session) {
      return;
    }
    Promise.all([getMyAppointments(), getDoctors()])
      .then(([myAppointments, allDoctors]) => {
        setAppointments(myAppointments);
        setDoctors(allDoctors);
      })
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "Could not load appointments."));
  }, [session?.userId]);

  if (!session) {
    return <p>Sign in to see your appointments.</p>;
  }

  if (error) {
    return <p className="error">{error}</p>;
  }

  return (
    <section>
      <div className="page-heading">
        <h1>Your appointments</h1>
        <Link to="/book" className="button-link">
          <CalendarPlus size={16} aria-hidden="true" />
          Book
        </Link>
      </div>
      {appointments.length === 0 ? <p className="muted">None yet.</p> : null}
      <ul className="list">
        {appointments.map((appointment) => {
          const doctor = doctors.find((item) => item.id === appointment.doctorId);
          return (
            <li key={appointment.id} className="card">
              <strong>{doctor?.name ?? "Doctor"}</strong>
              <p>{new Date(appointment.startsAt).toLocaleString(undefined, { dateStyle: "full", timeStyle: "short" })}</p>
              <p className="muted">
                {appointment.reason} · {appointment.status} · {bookingReference(appointment.id)}
              </p>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
