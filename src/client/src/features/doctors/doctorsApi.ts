import { apiFetch } from "@/shared/api/apiClient";
import type { LoadDefaultsResult } from "@/features/specialties/specialtiesApi";

export type Doctor = {
  id: string;
  name: string;
  specialtyId: string;
  specialty: string;
  experienceYears: number;
  consultationFee: number;
  followUpFee: number;
  languages: string[];
  bio: string;
};

// day: 0 = Sunday … 6 = Saturday. Times are "HH:mm" (the server also sends seconds) in hospital time.
export type ScheduleBlock = {
  day: number;
  start: string;
  end: string;
  slotMinutes: number;
};

export type DoctorInput = {
  name: string;
  specialtyId: string;
  experienceYears: number;
  consultationFee: number;
  followUpFee: number;
  languages: string[];
  bio: string;
  schedule: ScheduleBlock[];
};

export const getDoctors = (specialtyId?: string) =>
  apiFetch<Doctor[]>(`/doctors${specialtyId ? `?specialtyId=${specialtyId}` : ""}`);

export const getSchedule = (id: string) => apiFetch<ScheduleBlock[]>(`/doctors/${id}/schedule`);

export const createDoctor = (input: DoctorInput) =>
  apiFetch<Doctor>("/doctors", { method: "POST", body: JSON.stringify(input) });

export const updateDoctor = (id: string, input: DoctorInput) =>
  apiFetch<Doctor>(`/doctors/${id}`, { method: "PUT", body: JSON.stringify(input) });

export const deleteDoctor = (id: string) => apiFetch<void>(`/doctors/${id}`, { method: "DELETE" });

export const loadDefaultDoctors = () => apiFetch<LoadDefaultsResult>("/doctors/load-defaults", { method: "POST" });

export const dayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

// Monday first, as a week is read at the hospital.
export const weekOrder = [1, 2, 3, 4, 5, 6, 0];

export const shortTime = (time: string) => time.slice(0, 5);
