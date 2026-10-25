import { api } from "../../shared/api";

export type ChatMessage = {
  id: string;
  doctorId: string;
  patientId: string;
  sender: "patient" | "doctor";
  text: string;
  sentAt: string;
};

export function getMessages(doctorId: string, patientId: string) {
  return api<ChatMessage[]>(`/api/chats?doctorId=${doctorId}&patientId=${patientId}`);
}

export function sendMessage(input: {
  doctorId: string;
  patientId: string;
  sender: "patient" | "doctor";
  text: string;
}) {
  return api<ChatMessage>("/api/chats", {
    method: "POST",
    body: JSON.stringify(input)
  });
}
