import { apiFetch } from "@/shared/api/apiClient";

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

export const getMyAppointments = () => apiFetch<Appointment[]>("/appointments");

// An appointment as the signed-in doctor sees it, with the patient.
export type DoctorAppointment = {
  id: string;
  reference: string;
  startsAt: string;
  endsAt: string;
  reason: string;
  status: Appointment["status"];
  patientName: string;
  patientEmail: string;
};

export const getDoctorAppointments = () => apiFetch<DoctorAppointment[]>("/doctor/appointments");

// Same format as BookingReference.For on the server, e.g. "MHA-61D3D31B".
export function bookingReference(appointmentId: string) {
  return `MHA-${appointmentId.replaceAll("-", "").slice(0, 8).toUpperCase()}`;
}
