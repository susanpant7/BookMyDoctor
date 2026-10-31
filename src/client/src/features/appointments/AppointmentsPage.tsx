import { CalendarPlus } from "lucide-react";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { bookingReference, getMyAppointments, type Appointment } from "@/features/appointments/appointmentApi";
import { getDoctors, type Doctor } from "@/features/doctors/doctorsApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import { buttonClass } from "@/shared/components/buttonStyles";
import PageHeader from "@/shared/components/PageHeader";

const statusStyles = {
  Scheduled: "bg-emerald-50 text-emerald-700",
  Completed: "bg-slate-100 text-slate-600",
  Cancelled: "bg-rose-50 text-rose-700"
};

export default function AppointmentsPage() {
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    Promise.all([getMyAppointments(), getDoctors()])
      .then(([mine, allDoctors]) => {
        setAppointments(mine);
        setDoctors(allDoctors);
      })
      .catch((err) => setError(errorMessage(err, "Could not load your appointments.")))
      .finally(() => setLoading(false));
  }, []);

  return (
    <main className="mx-auto max-w-3xl px-6 py-10">
      <PageHeader
        title="Your appointments"
        action={
          <Link to="/book" className={buttonClass("primary")}>
            <CalendarPlus className="size-4" aria-hidden="true" />
            Book
          </Link>
        }
      />

      {error && (
        <div className="mt-6">
          <Alert message={error} />
        </div>
      )}

      {loading ? (
        <p className="mt-8 text-center text-sm text-slate-500">Loading...</p>
      ) : appointments.length === 0 && !error ? (
        <p className="mt-8 text-center text-sm text-slate-500">No appointments yet.</p>
      ) : (
        <ul className="mt-8 space-y-3">
          {appointments.map((appointment) => {
            const doctor = doctors.find((item) => item.id === appointment.doctorId);
            return (
              <li key={appointment.id} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="font-semibold text-slate-900">{doctor?.name ?? "Doctor"}</p>
                    <p className="text-sm text-slate-600">
                      {new Date(appointment.startsAt).toLocaleString(undefined, { dateStyle: "full", timeStyle: "short" })}
                    </p>
                  </div>
                  <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${statusStyles[appointment.status]}`}>
                    {appointment.status}
                  </span>
                </div>
                <p className="mt-2 text-sm text-slate-500">
                  {appointment.reason} · {bookingReference(appointment.id)}
                </p>
              </li>
            );
          })}
        </ul>
      )}
    </main>
  );
}
