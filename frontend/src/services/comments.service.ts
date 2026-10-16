import apiClient from '@/lib/api-client';

export interface Comment {
  id: string;
  content: string;
  taskId: string;
  authorId: string;
  authorName: string;
  createdAt: string;
  updatedAt?: string | null;
}

export const commentsApi = {
  getComments: (taskId: string) =>
    apiClient.get<Comment[]>('/comments', { params: { taskId } }).then(r => r.data),

  addComment: (taskId: string, content: string) =>
    apiClient.post<Comment>('/comments', { taskId, content }).then(r => r.data),

  editComment: (commentId: string, content: string) =>
    apiClient.put<Comment>(`/comments/${commentId}`, { content }).then(r => r.data),

  deleteComment: (commentId: string) =>
    apiClient.delete(`/comments/${commentId}`).then(r => r.data),
};
