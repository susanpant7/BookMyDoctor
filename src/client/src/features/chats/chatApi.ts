import { api } from "../../shared/api";

export const BOOKING_CHAT_ID = "99999999-9999-9999-9999-999999999999";

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
