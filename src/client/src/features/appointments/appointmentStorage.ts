const STORAGE_KEY = "bookmydoctor.appointments";

export type Appointment = {
  id: string;
  doctorId: string;
  patientId: string;
  scheduledAt: string;
  reason: string;
  status: string;
};

function readAll(): Appointment[] {
  const raw = localStorage.getItem(STORAGE_KEY);
  return raw ? (JSON.parse(raw) as Appointment[]) : [];
}

function writeAll(appointments: Appointment[]): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(appointments));
}

export function getAppointments(patientId: string): Appointment[] {
  return readAll()
    .filter((appointment) => appointment.patientId === patientId)
    .sort((left, right) => left.scheduledAt.localeCompare(right.scheduledAt));
}

export function bookAppointment(input: {
  doctorId: string;
  patientId: string;
  scheduledAt: string;
  reason: string;
}): Appointment {
  const appointment: Appointment = {
    id: crypto.randomUUID(),
    doctorId: input.doctorId,
    patientId: input.patientId,
    scheduledAt: input.scheduledAt,
    reason: input.reason.trim(),
    status: "Scheduled"
  };

  writeAll([...readAll(), appointment]);
  return appointment;
}
