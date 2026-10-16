import apiClient, { PagedResult } from '@/lib/api-client';

export interface UserItem {
  id: string;
  email: string;
  fullName: string;
  firstName: string;
  lastName: string;
  role: string;
  isActive: boolean;
  lastLoginAt?: string | null;
  createdAt: string;
}

export const usersApi = {
  getUsers: (page = 1, pageSize = 20, role?: string, isActive?: boolean) =>
    apiClient
      .get<PagedResult<UserItem>>('/users', {
        params: { page, pageSize, role, isActive },
      })
      .then((r) => r.data),

  changeRole: (userId: string, role: string) =>
    apiClient.put(`/users/${userId}/role`, { role }).then((r) => r.data),

  setActive: (userId: string, isActive: boolean) =>
    apiClient.put(`/users/${userId}/active`, { isActive }).then((r) => r.data),
};
