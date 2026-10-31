import { apiFetch } from "@/shared/api/apiClient";

export type Specialty = {
  id: string;
  name: string;
  description: string;
  doctorCount: number;
};

export type LoadDefaultsResult = {
  added: number;
  skipped: number;
};

export type SpecialtyInput = {
  name: string;
  description: string;
};

export const getSpecialties = () => apiFetch<Specialty[]>("/specialties");

export const createSpecialty = (input: SpecialtyInput) =>
  apiFetch<Specialty>("/specialties", { method: "POST", body: JSON.stringify(input) });

export const updateSpecialty = (id: string, input: SpecialtyInput) =>
  apiFetch<Specialty>(`/specialties/${id}`, { method: "PUT", body: JSON.stringify(input) });

export const deleteSpecialty = (id: string) => apiFetch<void>(`/specialties/${id}`, { method: "DELETE" });

export const loadDefaultSpecialties = () => apiFetch<LoadDefaultsResult>("/specialties/load-defaults", { method: "POST" });
