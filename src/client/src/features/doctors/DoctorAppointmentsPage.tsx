import { useEffect, useState } from "react";
import { getDoctorAppointments, type DoctorAppointment } from "@/features/appointments/appointmentApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import PageHeader from "@/shared/components/PageHeader";

const statusStyles = {
  Scheduled: "bg-emerald-50 text-emerald-700",
  Completed: "bg-stone-100 text-stone-600",
  Cancelled: "bg-rose-50 text-rose-700"
};

// The signed-in doctor's appointments: upcoming ones first (soonest at the top), then past ones (latest at the top).
export default function DoctorAppointmentsPage() {
  const [appointments, setAppointments] = useState<DoctorAppointment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    getDoctorAppointments()
      .then(setAppointments)
      .catch((err) => setError(errorMessage(err, "Could not load your appointments.")))
      .finally(() => setLoading(false));
  }, []);

  const now = new Date();
  const upcoming = appointments.filter((a) => new Date(a.endsAt) >= now);
  const past = appointments.filter((a) => new Date(a.endsAt) < now).reverse();

  return (
    <main className="mx-auto max-w-3xl px-4 py-8 sm:px-6 sm:py-10">
      <PageHeader title="My appointments" description="The patients booked with you." />

      {error && (
        <div className="mt-6">
          <Alert message={error} />
        </div>
      )}

      {loading ? (
        <p className="mt-8 text-center text-sm text-stone-500">Loading...</p>
      ) : appointments.length === 0 && !error ? (
        <p className="mt-8 text-center text-sm text-stone-500">No appointments yet.</p>
      ) : (
        <>
          <AppointmentList title="Upcoming" appointments={upcoming} empty="No upcoming appointments." />
          {past.length > 0 && <AppointmentList title="Past" appointments={past} />}
        </>
      )}
    </main>
  );
}

function AppointmentList({ title, appointments, empty }: { title: string; appointments: DoctorAppointment[]; empty?: string }) {
  return (
    <section className="mt-8">
      <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-stone-500">
        {title} ({appointments.length})
      </h2>
      {appointments.length === 0 ? (
        <p className="text-sm text-stone-500">{empty}</p>
      ) : (
        <ul className="space-y-3">
          {appointments.map((appointment) => (
            <li key={appointment.id} className="rounded-2xl border border-stone-200 bg-white p-5 shadow-sm">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="font-semibold text-stone-900">{appointment.patientName}</p>
                  <p className="truncate text-sm text-stone-500">{appointment.patientEmail}</p>
                  <p className="mt-1 text-sm text-stone-600">
                    {new Date(appointment.startsAt).toLocaleString(undefined, { dateStyle: "full", timeStyle: "short" })}
                    {" – "}
                    {new Date(appointment.endsAt).toLocaleTimeString(undefined, { timeStyle: "short" })}
                  </p>
                </div>
                <span className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-medium ${statusStyles[appointment.status]}`}>
                  {appointment.status}
                </span>
              </div>
              <p className="mt-2 text-sm text-stone-500">
                {appointment.reason} · {appointment.reference}
              </p>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
