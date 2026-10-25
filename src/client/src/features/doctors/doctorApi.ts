import { api } from "../../shared/api";

export type Doctor = {
  id: string;
  name: string;
  specialtyId: string;
  specialty: string;
  location: string;
  experienceYears: number;
  consultationFee: number;
};

export function getDoctors(specialtyId?: string) {
  const query = specialtyId ? `?specialtyId=${specialtyId}` : "";
  return api<Doctor[]>(`/api/doctors${query}`);
}
