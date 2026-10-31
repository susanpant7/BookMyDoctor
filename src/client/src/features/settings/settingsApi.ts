import { apiFetch } from "@/shared/api/apiClient";

export type Setting = {
  key: string;
  label: string;
  description: string;
  value: string;
  defaultValue: string;
  kind: "text" | "longtext" | "integer" | "number" | "boolean";
};

export type HospitalInfo = {
  name: string;
  address: string;
  phone: string;
  facilities: string[];
  parking: string;
  publicTransport: string;
};

// day: 0 = Sunday … 6 = Saturday. Times are "HH:mm" (the server also sends seconds).
export type HospitalHours = { day: number; opens: string; closes: string };
export type ServicePrice = { id: string; name: string; price: number };

export type HospitalDetails = {
  info: HospitalInfo;
  hours: HospitalHours[];
  servicePrices: ServicePrice[];
};

export const getSettings = () => apiFetch<Setting[]>("/settings");

export const updateSetting = (key: string, value: string) =>
  apiFetch<Setting>(`/settings/${encodeURIComponent(key)}`, { method: "PUT", body: JSON.stringify({ value }) });

export const restoreDefaultSettings = () => apiFetch<Setting[]>("/settings/restore-defaults", { method: "POST" });

export const getHospital = () => apiFetch<HospitalDetails>("/hospital");

export const restoreDefaultHospital = () => apiFetch<HospitalDetails>("/hospital/restore-defaults", { method: "POST" });

export const saveHospital = (details: HospitalDetails) =>
  apiFetch<HospitalDetails>("/hospital", { method: "PUT", body: JSON.stringify(details) });
