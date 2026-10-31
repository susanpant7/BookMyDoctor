import { apiFetch } from "@/shared/api/apiClient";

export type Patient = {
  id: string;
  name: string;
  email: string;
};

export type LoadDefaultsResult = {
  added: number;
  skipped: number;
};

export type CreatePatientInput = {
  name: string;
  email: string;
  password: string;
};

export type UpdatePatientInput = {
  name: string;
  email: string;
  password?: string;
};

export const getPatients = () => apiFetch<Patient[]>("/users");

export const createPatient = (input: CreatePatientInput) =>
  apiFetch<Patient>("/users", { method: "POST", body: JSON.stringify(input) });

export const updatePatient = (id: string, input: UpdatePatientInput) =>
  apiFetch<Patient>(`/users/${id}`, { method: "PUT", body: JSON.stringify(input) });

export const deletePatient = (id: string) => apiFetch<void>(`/users/${id}`, { method: "DELETE" });

export const loadDefaultPatients = (password: string) =>
  apiFetch<LoadDefaultsResult>("/users/load-defaults", { method: "POST", body: JSON.stringify({ password }) });
