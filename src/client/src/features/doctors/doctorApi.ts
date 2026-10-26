import { api } from "../../shared/api";

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

export function getDoctors(specialtyId?: string) {
  const query = specialtyId ? `?specialtyId=${specialtyId}` : "";
  return api<Doctor[]>(`/api/doctors${query}`);
}
