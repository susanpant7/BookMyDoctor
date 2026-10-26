import { HubConnectionBuilder, LogLevel, type HubConnection } from "@microsoft/signalr";
import type { ChatMessage } from "./chatApi";

// Method names must match ChatHub on the server.
export const ChatHubEvents = {
  receiveMessage: "ReceiveMessage"
} as const;

export function createChatConnection(getToken: () => string): HubConnection {
  return new HubConnectionBuilder()
    // WebSockets can't send an Authorization header, so SignalR passes this as ?access_token=.
    .withUrl("/hubs/chat", { accessTokenFactory: getToken })
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
