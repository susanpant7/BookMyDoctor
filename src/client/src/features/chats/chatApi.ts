import { api } from "../../shared/api";

export type ChatMessage = {
  id: string;
  patientId: string;
  sender: "patient" | "assistant";
  text: string;
  sentAt: string;
};

export function getMessages() {
  return api<ChatMessage[]>("/api/chats");
}
