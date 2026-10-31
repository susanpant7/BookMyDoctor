import { apiFetch } from "@/shared/api/apiClient";

// One section of a knowledge file, or a doctor profile (source "doctors").
export type KnowledgeChunk = {
  id: string;
  source: string;
  title: string;
  text: string;
};

export type UploadFileResult = {
  fileName: string;
  sections: number;
  error: string | null;
};

export type KnowledgeSearchResult = {
  score: number;
  id: string;
  source: string;
  title: string;
  text: string;
};

export const DOCTORS_SOURCE = "doctors";

export const getChunks = () => apiFetch<KnowledgeChunk[]>("/knowledge/chunks");

export function uploadKnowledge(files: File[]) {
  const form = new FormData();
  files.forEach((file) => form.append("files", file));
  return apiFetch<UploadFileResult[]>("/knowledge/upload", { method: "POST", body: form });
}

export const loadDefaultKnowledge = () => apiFetch<UploadFileResult[]>("/knowledge/load-defaults", { method: "POST" });

export const syncDoctorProfiles = () => apiFetch<void>("/knowledge/sync-doctors", { method: "POST" });

export const deleteKnowledgeSource = (source: string) =>
  apiFetch<{ deleted: number }>(`/knowledge/sources/${encodeURIComponent(source)}`, { method: "DELETE" });

export const searchKnowledge = (query: string) =>
  apiFetch<KnowledgeSearchResult[]>(`/knowledge/search?q=${encodeURIComponent(query)}&k=5`);
