import { apiFetch } from "@/shared/api/apiClient";

export type Setting = {
  key: string;
  label: string;
  description: string;
  value: string;
  defaultValue: string;
  kind: "text" | "longtext" | "integer" | "number" | "boolean";
};

export type AiModelKind = "chat" | "embedding";

// A chat or embedding model to switch to on the Settings page. "Use" copies its address, model and key name (and
// for an embedding model the prefixes) into the settings the assistant reads. apiKeyName is the name of the .env
// variable with the service's key, never the key; empty for Ollama.
export type AiModel = {
  id: string;
  kind: AiModelKind;
  name: string;
  baseUrl: string;
  model: string;
  apiKeyName: string;
  queryPrefix: string;
  documentPrefix: string;
  notes: string;
};
export type SaveAiModelRequest = Omit<AiModel, "id">;

// The key variables the models in use read and whether they are set, and which key names on the Settings page are
// set in the server's .env file. The keys themselves are never sent.
export type AiKeysInfo = {
  chatKeyNameInUse: string;
  chatKeyInUseSet: boolean;
  embeddingKeyNameInUse: string;
  embeddingKeyInUseSet: boolean;
  setKeyNames: string[];
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

export const getAiKeys = () => apiFetch<AiKeysInfo>("/settings/ai-keys");

export const getAiModels = (kind: AiModelKind) => apiFetch<AiModel[]>(`/settings/models?kind=${kind}`);

export const addAiModel = (request: SaveAiModelRequest) =>
  apiFetch<AiModel>("/settings/models", { method: "POST", body: JSON.stringify(request) });

export const updateAiModel = (id: string, request: SaveAiModelRequest) =>
  apiFetch<AiModel>(`/settings/models/${id}`, { method: "PUT", body: JSON.stringify(request) });

export const deleteAiModel = (id: string) => apiFetch<void>(`/settings/models/${id}`, { method: "DELETE" });

// A known service, listed below the saved models. It can't be changed; "Edit" saves a copy.
export type AiService = Omit<AiModel, "id" | "kind">;

// A model to list, test or switch to.
export type ModelChoice = Pick<AiModel, "kind" | "baseUrl" | "model" | "apiKeyName" | "queryPrefix" | "documentPrefix">;

// Another model of the same service. An embedding service's prefixes belong to its own model, so another model gets none.
export function withModel(choice: ModelChoice, model: string): ModelChoice {
  const ownPrefixes = choice.kind === "embedding" && model === choice.model;
  return {
    ...choice,
    model,
    queryPrefix: ownPrefixes ? choice.queryPrefix : "",
    documentPrefix: ownPrefixes ? choice.documentPrefix : ""
  };
}

export const getAiServices = (kind: AiModelKind) => apiFetch<AiService[]>(`/settings/models/services?kind=${kind}`);

// Asks the service at a known address which models it offers for our key.
export const getAvailableModels = (kind: AiModelKind, baseUrl: string, keyName: string) =>
  apiFetch<{ baseUrl: string; models: string[] }>(
    `/settings/models/available?kind=${kind}&baseUrl=${encodeURIComponent(baseUrl)}&keyName=${encodeURIComponent(keyName)}`
  );

// Sends one tiny request to the model; nothing is saved. Resolves with how long it took to answer.
export const testAiModel = (choice: ModelChoice) =>
  apiFetch<{ milliseconds: number }>("/settings/models/test", { method: "POST", body: JSON.stringify(choice) });

// Chat: tests the model, then uses it. Embedding: embeds all the knowledge with it, then uses it. On a failure
// nothing is changed.
export const switchAiModel = (choice: ModelChoice) =>
  apiFetch<{ reembedded: number }>("/settings/models/use", { method: "POST", body: JSON.stringify(choice) });

export const updateSetting = (key: string, value: string) =>
  apiFetch<Setting>(`/settings/${encodeURIComponent(key)}`, { method: "PUT", body: JSON.stringify({ value }) });

export const restoreDefaultSettings = () => apiFetch<Setting[]>("/settings/restore-defaults", { method: "POST" });

// Deletes all data (doctors, patients, appointments, chats, knowledge) and starts again from the defaults.
export const cleanUpData = () => apiFetch<void>("/settings/clean-up", { method: "POST" });

export const getHospital =() => apiFetch<HospitalDetails>("/hospital");

export const restoreDefaultHospital = () => apiFetch<HospitalDetails>("/hospital/restore-defaults", { method: "POST" });

export const saveHospital = (details: HospitalDetails) =>
  apiFetch<HospitalDetails>("/hospital", { method: "PUT", body: JSON.stringify(details) });
