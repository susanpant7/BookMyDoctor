import { apiFetch } from "@/shared/api/apiClient";

export type ChatMessage = {
  id: string;
  patientId: string;
  sender: "patient" | "assistant";
  text: string;
  sentAt: string;
};

export const getMessages = () => apiFetch<ChatMessage[]>("/chats");
