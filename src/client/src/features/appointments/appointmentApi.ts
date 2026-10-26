import { api } from "../../shared/api";

export type Appointment = {
  id: string;
  doctorId: string;
  patientId: string;
  startsAt: string;
  endsAt: string;
  reason: string;
  status: "Scheduled" | "Completed" | "Cancelled";
  bookedAt: string;
};

export function getMyAppointments() {
  return api<Appointment[]>("/api/appointments");
}

// Same format as BookingReference.For on the server, e.g. "BMD-61D3D31B".
export function bookingReference(appointmentId: string) {
  return `BMD-${appointmentId.replaceAll("-", "").slice(0, 8).toUpperCase()}`;
}
