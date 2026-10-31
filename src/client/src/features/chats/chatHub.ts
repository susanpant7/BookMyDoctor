import { HubConnectionBuilder, LogLevel, type HubConnection } from "@microsoft/signalr";
import type { ChatMessage } from "@/features/chats/chatApi";
import { API_URL } from "@/shared/api/apiClient";

// Method names must match ChatHub on the server.
export const ChatHubEvents = {
  receiveMessage: "ReceiveMessage",
  assistantTyping: "AssistantTyping",
  assistantDraft: "AssistantDraft",
  bookingOptions: "BookingOptions"
} as const;

// Free times the assistant just looked up; null means "hide the buttons".
export type BookingOptions = {
  doctorId: string;
  doctorName: string;
  day: string;
  slots: { startsAt: string; label: string }[];
};

export function createChatConnection(getToken: () => string): HubConnection {
  return new HubConnectionBuilder()
    // WebSockets can't send an Authorization header, so SignalR passes this as ?access_token=.
    .withUrl(`${API_URL}/hubs/chat`, { accessTokenFactory: getToken })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}

export function joinChat(connection: HubConnection) {
  return connection.invoke("JoinChat");
}

export function sendChatMessage(connection: HubConnection, text: string) {
  return connection.invoke("SendMessage", { text });
}

export function onReceiveMessage(connection: HubConnection, handler: (message: ChatMessage) => void) {
  connection.on(ChatHubEvents.receiveMessage, handler);
}

export function onAssistantTyping(connection: HubConnection, handler: (isTyping: boolean) => void) {
  connection.on(ChatHubEvents.assistantTyping, handler);
}

// The reply so far while it's being written; null when the draft should disappear.
// The final reply always arrives separately through "ReceiveMessage" and replaces the draft.
export function onAssistantDraft(connection: HubConnection, handler: (textSoFar: string | null) => void) {
  connection.on(ChatHubEvents.assistantDraft, handler);
}

export function onBookingOptions(connection: HubConnection, handler: (options: BookingOptions | null) => void) {
  connection.on(ChatHubEvents.bookingOptions, handler);
}

// Books directly on the server (no AI involved); the confirmation arrives as a chat message.
export function bookSlot(connection: HubConnection, request: { doctorId: string; startsAt: string; reason: string }) {
  return connection.invoke("BookSlot", request);
}
