import apiClient from '@/lib/api-client';

export interface DashboardStats {
  projectCount: number;
  myOpenTasks: number;
  myOverdueTasks: number;
  myDueThisWeek: number;
  myCompletedLast7Days: number;
  myTasksByStatus: Record<string, number>;
  myTasksByPriority: Record<string, number>;
  unreadNotifications: number;
}

export const dashboardApi = {
  getStats: () => apiClient.get<DashboardStats>('/dashboard/stats').then(r => r.data),
};
