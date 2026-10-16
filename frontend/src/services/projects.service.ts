import apiClient from '@/lib/api-client';

export interface Project {
  id: string;
  name: string;
  description: string | null;
  key: string;
  status: string;
  createdAt: string;
  ownerName: string;
}

export interface ProjectDetail {
  id: string;
  name: string;
  description: string | null;
  key: string;
  status: string;
  createdAt: string;
  ownerId: string;
  ownerName: string;
  myRole: string;
  memberCount: number;
  totalTasks: number;
  openTasks: number;
  overdueTasks: number;
}

export interface Member {
  userId: string;
  fullName: string;
  email: string;
  role: string;
  isOwner: boolean;
  joinedAt: string;
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

export interface CreateProjectRequest {
  name: string;
  description: string;
  key: string;
}

export const projectsApi = {
  getProjects: (page = 1, pageSize = 10, search?: string) =>
    apiClient.get<PagedResult<Project>>('/projects', {
      params: { page, pageSize, search },
    }).then(r => r.data),

  getProject: (projectId: string) =>
    apiClient.get<ProjectDetail>(`/projects/${projectId}`).then(r => r.data),

  createProject: (data: CreateProjectRequest) =>
    apiClient.post<Project>('/projects', data).then(r => r.data),

  updateProject: (projectId: string, data: { name: string; description?: string }) =>
    apiClient.put(`/projects/${projectId}`, data).then(r => r.data),

  setStatus: (projectId: string, status: string) =>
    apiClient.put(`/projects/${projectId}/status`, { status }).then(r => r.data),

  getMembers: (projectId: string) =>
    apiClient.get<Member[]>(`/projects/${projectId}/members`).then(r => r.data),

  addMember: (projectId: string, data: { email: string; role: string }) =>
    apiClient.post<Member>(`/projects/${projectId}/members`, data).then(r => r.data),

  changeMemberRole: (projectId: string, userId: string, role: string) =>
    apiClient.put(`/projects/${projectId}/members/${userId}/role`, { role }).then(r => r.data),

  removeMember: (projectId: string, userId: string) =>
    apiClient.delete(`/projects/${projectId}/members/${userId}`).then(r => r.data),
};
