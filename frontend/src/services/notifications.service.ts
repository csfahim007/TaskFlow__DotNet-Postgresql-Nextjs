import apiClient, { PagedResult } from '@/lib/api-client';

export interface NotificationItem {
  id: string;
  type: string;
  title: string;
  message: string;
  projectId: string | null;
  taskId: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationsResponse {
  notifications: PagedResult<NotificationItem>;
  unreadCount: number;
}

export const notificationsApi = {
  getNotifications: (page = 1, pageSize = 20, unreadOnly = false) =>
    apiClient.get<NotificationsResponse>('/notifications', {
      params: { page, pageSize, unreadOnly },
    }).then(r => r.data),

  markAsRead: (id: string) =>
    apiClient.post(`/notifications/${id}/read`).then(r => r.data),

  markAllAsRead: () =>
    apiClient.post('/notifications/read-all').then(r => r.data),
};
