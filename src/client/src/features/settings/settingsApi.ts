import { apiFetch } from "@/shared/api/apiClient";

export type Setting = {
  key: string;
  label: string;
  description: string;
  value: string;
  defaultValue: string;
  kind: "text" | "longtext" | "integer" | "number" | "boolean";
};

// A service that can run the chat model, from the notes on the Settings page.
export type AiProvider = {
  name: string;
  baseUrl: string;
  exampleModel: string;
  needsApiKey: boolean;
  freeTier: string;
  signUpUrl: string;
};

// A service that can run the embedding model, with the prefixes its model expects.
export type EmbeddingProvider = {
  name: string;
  baseUrl: string;
  exampleModel: string;
  queryPrefix: string;
  documentPrefix: string;
  needsApiKey: boolean;
  freeTier: string;
  signUpUrl: string;
};

// The has… flags say whether the keys (AI_API_KEY, EMBEDDING_API_KEY) are set in the server's .env file. The keys
// themselves are never sent.
export type AiProvidersInfo = {
  apiKeyName: string;
  hasApiKey: boolean;
  providers: AiProvider[];
  embeddingApiKeyName: string;
  hasEmbeddingApiKey: boolean;
  embeddingProviders: EmbeddingProvider[];
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

export const getAiProviders = () => apiFetch<AiProvidersInfo>("/settings/ai-providers");

// A chat model the admin saved to switch to later.
export type SavedChatModel = { id: string; name: string; baseUrl: string; model: string; notes: string };
export type SaveChatModelRequest = Omit<SavedChatModel, "id">;

export const getChatModels = () => apiFetch<SavedChatModel[]>("/settings/chat-models");

export const addChatModel = (request: SaveChatModelRequest) =>
  apiFetch<SavedChatModel>("/settings/chat-models", { method: "POST", body: JSON.stringify(request) });

export const updateChatModel = (id: string, request: SaveChatModelRequest) =>
  apiFetch<SavedChatModel>(`/settings/chat-models/${id}`, { method: "PUT", body: JSON.stringify(request) });

export const deleteChatModel = (id: string) => apiFetch<void>(`/settings/chat-models/${id}`, { method: "DELETE" });

export const updateSetting = (key: string, value: string) =>
  apiFetch<Setting>(`/settings/${encodeURIComponent(key)}`, { method: "PUT", body: JSON.stringify({ value }) });

export const restoreDefaultSettings = () => apiFetch<Setting[]>("/settings/restore-defaults", { method: "POST" });

// Deletes all data (doctors, patients, appointments, chats, knowledge) and starts again from the defaults.
export const cleanUpData = () => apiFetch<void>("/settings/clean-up", { method: "POST" });

export const getHospital =() => apiFetch<HospitalDetails>("/hospital");

export const restoreDefaultHospital = () => apiFetch<HospitalDetails>("/hospital/restore-defaults", { method: "POST" });

export const saveHospital = (details: HospitalDetails) =>
  apiFetch<HospitalDetails>("/hospital", { method: "PUT", body: JSON.stringify(details) });
