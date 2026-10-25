import { CalendarPlus } from "lucide-react";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useSession } from "../../shared/session";
import { getDoctors, type Doctor } from "../doctors/doctorApi";
import { getAppointments, type Appointment } from "./appointmentStorage";

export function AppointmentsPage() {
  const { session } = useSession();
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!session) {
      return;
    }
    setAppointments(getAppointments(session.userId));
    getDoctors()
      .then(setDoctors)
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "Could not load doctors."));
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
              <p>{new Date(appointment.scheduledAt).toLocaleString()}</p>
              <p className="muted">
                {appointment.reason} · {appointment.status}
              </p>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
