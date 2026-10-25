import { api } from "../../shared/api";

export type Specialty = {
  id: string;
  name: string;
  description: string;
};

export function getSpecialties() {
  return api<Specialty[]>("/api/specialties");
}
