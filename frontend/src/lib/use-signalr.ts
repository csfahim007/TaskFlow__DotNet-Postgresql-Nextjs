import { useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:8080';

export interface SignalRHandlers {
  onTaskCreated?: (task: any) => void;
  onTaskUpdated?: (task: any) => void;
  onTaskStatusUpdated?: (task: any) => void;
  onTaskDeleted?: (payload: { taskId: string; projectId: string }) => void;
  onCommentAdded?: (comment: any) => void;
  onProjectCreated?: (project: any) => void;
  onNotificationReceived?: (notification: any) => void;
}

export function useSignalR(
  projectId: string | null,
  handlers: SignalRHandlers
) {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const handlersRef = useRef(handlers);
  handlersRef.current = handlers;

  useEffect(() => {
    const token = typeof window !== 'undefined' ? localStorage.getItem('taskflow_token') : null;
    if (!token) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE || window.location.origin}/hubs/taskflow`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on('TaskCreated', (task) => handlersRef.current.onTaskCreated?.(task));
    connection.on('TaskUpdated', (task) => handlersRef.current.onTaskUpdated?.(task));
    connection.on('TaskStatusUpdated', (task) => handlersRef.current.onTaskStatusUpdated?.(task));
    connection.on('TaskDeleted', (payload) => handlersRef.current.onTaskDeleted?.(payload));
    connection.on('CommentAdded', (comment) => handlersRef.current.onCommentAdded?.(comment));
    connection.on('ProjectCreated', (project) => handlersRef.current.onProjectCreated?.(project));
    connection.on('NotificationReceived', (notif) => handlersRef.current.onNotificationReceived?.(notif));

    connection
      .start()
      .then(async () => {
        if (projectId) {
          await connection.invoke('JoinProject', projectId).catch(() => {});
        }
      })
      .catch(() => {});

    connectionRef.current = connection;

    return () => {
      if (projectId && connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke('LeaveProject', projectId).catch(() => {}).finally(() => {
          connection.stop();
        });
      } else {
        connection.stop();
      }
    };
  }, [projectId]);
}
