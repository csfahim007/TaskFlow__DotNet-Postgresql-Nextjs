import apiClient from '@/lib/api-client';

export interface Task {
  id: string;
  taskNumber: string;
  title: string;
  description: string | null;
  status: string;
  priority: string;
  dueDate: string | null;
  storyPoints: number | null;
  projectId: string;
  projectKey: string;
  assigneeId: string | null;
  assigneeName: string | null;
  createdAt: string;
}

export interface TaskHistoryEntry {
  action: string;
  oldValue: string | null;
  newValue: string | null;
  userEmail: string | null;
  at: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface CreateTaskRequest {
  title: string;
  description?: string;
  projectId: string;
  priority: string;
  dueDate?: string;
  storyPoints?: number;
  assigneeId?: string;
}

export interface UpdateTaskRequest {
  title: string;
  description?: string;
  priority: string;
  dueDate?: string;
  storyPoints?: number;
}

export const tasksApi = {
  getTasks: (projectId: string, params?: { page?: number; pageSize?: number; search?: string; status?: string; priority?: string; assigneeId?: string; overdue?: boolean }) =>
    apiClient.get<PagedResult<Task>>('/tasks', { params: { projectId, ...params } }).then(r => r.data),

  getMyTasks: (params?: { page?: number; pageSize?: number; status?: string; openOnly?: boolean }) =>
    apiClient.get<PagedResult<Task>>('/tasks/mine', { params }).then(r => r.data),

  getTask: (taskId: string) =>
    apiClient.get<Task>(`/tasks/${taskId}`).then(r => r.data),

  getHistory: (taskId: string) =>
    apiClient.get<TaskHistoryEntry[]>(`/tasks/${taskId}/history`).then(r => r.data),

  createTask: (data: CreateTaskRequest) =>
    apiClient.post<Task>('/tasks', data).then(r => r.data),

  updateTask: (taskId: string, data: UpdateTaskRequest) =>
    apiClient.put<Task>(`/tasks/${taskId}`, data).then(r => r.data),

  updateStatus: (taskId: string, status: string) =>
    apiClient.patch<Task>(`/tasks/${taskId}/status`, { status }).then(r => r.data),

  assign: (taskId: string, assigneeId: string | null) =>
    apiClient.patch<Task>(`/tasks/${taskId}/assignee`, { assigneeId }).then(r => r.data),

  deleteTask: (taskId: string) =>
    apiClient.delete(`/tasks/${taskId}`).then(r => r.data),
};
