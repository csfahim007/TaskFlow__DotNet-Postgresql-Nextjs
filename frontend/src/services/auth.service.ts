import apiClient, { REFRESH_KEY } from '@/lib/api-client';

export interface AuthResponse {
  token: string;
  refreshToken: string;
  expiresIn: number;
  email: string;
  fullName: string;
  role: string;
  userId: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export const authApi = {
  login: (data: LoginRequest) =>
    apiClient.post<AuthResponse>('/auth/login', data).then(r => r.data),

  register: (data: RegisterRequest) =>
    apiClient.post<AuthResponse>('/auth/register', data).then(r => r.data),

  /** Revokes the refresh token + current access token server-side (best effort). */
  logout: () =>
    apiClient
      .post('/auth/logout', { refreshToken: typeof window !== 'undefined' ? localStorage.getItem(REFRESH_KEY) : null })
      .catch(() => undefined),

  changePassword: (data: ChangePasswordRequest) =>
    apiClient.post('/auth/change-password', data).then(r => r.data),
};
