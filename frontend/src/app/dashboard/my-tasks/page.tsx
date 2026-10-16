"use client";

import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { tasksApi, Task } from "@/services/tasks.service";
import { commentsApi, Comment } from "@/services/comments.service";
import { useAuthStore } from "@/stores/auth-store";

const STATUS_OPTIONS = [
  { key: "all_open", label: "All Active" },
  { key: "Todo", label: "To Do" },
  { key: "InProgress", label: "In Progress" },
  { key: "InReview", label: "In Review" },
  { key: "Done", label: "Completed" },
];

function priorityBadgeClass(p: string) {
  const map: Record<string, string> = {
    Low: "badge-low",
    Medium: "badge-medium",
    High: "badge-high",
    Critical: "badge-critical",
  };
  return map[p] ?? "badge-medium";
}

function statusBadgeClass(s: string) {
  const map: Record<string, string> = {
    Todo: "badge-todo",
    InProgress: "badge-inprogress",
    InReview: "badge-inreview",
    Done: "badge-done",
    Cancelled: "badge-cancelled",
  };
  return map[s] ?? "badge-todo";
}

export default function MyTasksPage() {
  const user = useAuthStore((s) => s.user);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterStatus, setFilterStatus] = useState("all_open");
  const [search, setSearch] = useState("");
  const [selectedTask, setSelectedTask] = useState<Task | null>(null);

  // Comment state inside task modal
  const [comments, setComments] = useState<Comment[]>([]);
  const [commentText, setCommentText] = useState("");
  const [commentLoading, setCommentLoading] = useState(false);

  const fetchTasks = useCallback(async () => {
    setLoading(true);
    try {
      const openOnly = filterStatus === "all_open";
      const statusParam = filterStatus !== "all_open" ? filterStatus : undefined;
      const res = await tasksApi.getMyTasks({
        pageSize: 100,
        openOnly,
        status: statusParam,
      });
      setTasks(res.items);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, [filterStatus]);

  useEffect(() => {
    fetchTasks();
  }, [fetchTasks]);

  const loadComments = async (taskId: string) => {
    try {
      const c = await commentsApi.getComments(taskId);
      setComments(c);
    } catch {
      setComments([]);
    }
  };

  const handleOpenModal = (task: Task) => {
    setSelectedTask(task);
    loadComments(task.id);
  };

  const handleStatusChange = async (taskId: string, newStatus: string) => {
    try {
      await tasksApi.updateStatus(taskId, newStatus);
      setTasks((prev) =>
        prev.map((t) => (t.id === taskId ? { ...t, status: newStatus } : t))
      );
      if (selectedTask?.id === taskId) {
        setSelectedTask((prev) => (prev ? { ...prev, status: newStatus } : null));
      }
    } catch (err) {
      console.error(err);
    }
  };

  const handleAddComment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedTask || !commentText.trim()) return;
    setCommentLoading(true);
    try {
      const c = await commentsApi.addComment(selectedTask.id, commentText.trim());
      setComments((prev) => [c, ...prev]);
      setCommentText("");
    } finally {
      setCommentLoading(false);
    }
  };

  const filteredTasks = tasks.filter((t) => {
    const q = search.toLowerCase();
    return (
      !q ||
      t.title.toLowerCase().includes(q) ||
      t.taskNumber.toLowerCase().includes(q) ||
      t.projectKey.toLowerCase().includes(q)
    );
  });

  return (
    <div className="animate-fade-in" style={{ maxWidth: 1100, margin: "0 auto" }}>
      <div className="page-header" style={{ marginBottom: 20 }}>
        <div>
          <h1 className="page-title">My Tasks</h1>
          <p className="page-subtitle">Personal assignments across all projects</p>
        </div>
      </div>

      {/* Filters */}
      <div style={{ display: "flex", gap: 12, marginBottom: 20, flexWrap: "wrap", alignItems: "center" }}>
        <input
          type="search"
          className="input"
          placeholder="Filter my tasks…"
          style={{ maxWidth: 260 }}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />

        <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
          {STATUS_OPTIONS.map((opt) => (
            <button
              key={opt.key}
              className={`btn btn-sm ${filterStatus === opt.key ? "btn-primary" : "btn-secondary"}`}
              onClick={() => setFilterStatus(opt.key)}
            >
              {opt.label}
            </button>
          ))}
        </div>
      </div>

      {/* List */}
      <div className="card" style={{ padding: 0, overflow: "hidden" }}>
        {loading ? (
          <div style={{ padding: 36, textAlign: "center", color: "var(--text-muted)" }}>
            <div className="spinner" style={{ margin: "0 auto 8px" }} />
            Loading assigned tasks…
          </div>
        ) : filteredTasks.length === 0 ? (
          <div style={{ padding: 48, textAlign: "center", color: "var(--text-muted)" }}>
            <div style={{ fontSize: 28, marginBottom: 8 }}>✅</div>
            <div style={{ fontSize: 15, fontWeight: 600, color: "var(--text-secondary)" }}>
              No tasks found
            </div>
            <div style={{ fontSize: 13, marginTop: 4 }}>
              {search ? "Try clearing your search query." : "You have no pending tasks in this category."}
            </div>
          </div>
        ) : (
          <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "left" }}>
            <thead>
              <tr style={{ background: "var(--bg-elevated)", borderBottom: "1px solid var(--border)" }}>
                <th style={{ padding: "12px 16px", fontSize: 12, fontWeight: 600, color: "var(--text-secondary)" }}>
                  Task
                </th>
                <th style={{ padding: "12px 16px", fontSize: 12, fontWeight: 600, color: "var(--text-secondary)" }}>
                  Project
                </th>
                <th style={{ padding: "12px 16px", fontSize: 12, fontWeight: 600, color: "var(--text-secondary)" }}>
                  Priority
                </th>
                <th style={{ padding: "12px 16px", fontSize: 12, fontWeight: 600, color: "var(--text-secondary)" }}>
                  Due Date
                </th>
                <th style={{ padding: "12px 16px", fontSize: 12, fontWeight: 600, color: "var(--text-secondary)" }}>
                  Status
                </th>
              </tr>
            </thead>
            <tbody>
              {filteredTasks.map((t) => {
                const isOverdue = t.dueDate && new Date(t.dueDate) < new Date() && t.status !== "Done";
                return (
                  <tr
                    key={t.id}
                    onClick={() => handleOpenModal(t)}
                    style={{
                      borderBottom: "1px solid var(--border)",
                      cursor: "pointer",
                      transition: "background 0.15s",
                    }}
                    onMouseEnter={(e) => {
                      (e.currentTarget as HTMLElement).style.background = "var(--bg-hover)";
                    }}
                    onMouseLeave={(e) => {
                      (e.currentTarget as HTMLElement).style.background = "transparent";
                    }}
                  >
                    <td style={{ padding: "12px 16px" }}>
                      <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                        <span
                          style={{
                            fontFamily: "monospace",
                            fontSize: 11,
                            fontWeight: 700,
                            color: "var(--brand-400)",
                          }}
                        >
                          {t.taskNumber}
                        </span>
                        <span style={{ fontWeight: 600, color: "var(--text-primary)", fontSize: 13 }}>
                          {t.title}
                        </span>
                      </div>
                    </td>

                    <td style={{ padding: "12px 16px" }}>
                      <Link
                        href={`/dashboard/projects/${t.projectId}`}
                        onClick={(e) => e.stopPropagation()}
                        style={{
                          fontSize: 12,
                          color: "var(--brand-400)",
                          textDecoration: "none",
                          fontWeight: 600,
                        }}
                      >
                        {t.projectKey}
                      </Link>
                    </td>

                    <td style={{ padding: "12px 16px" }}>
                      <span className={`badge ${priorityBadgeClass(t.priority)}`}>
                        {t.priority}
                      </span>
                    </td>

                    <td style={{ padding: "12px 16px" }}>
                      {t.dueDate ? (
                        <span
                          style={{
                            fontSize: 12,
                            color: isOverdue ? "#f87171" : "var(--text-muted)",
                            fontWeight: isOverdue ? 600 : 400,
                          }}
                        >
                          {isOverdue ? "⚠ " : "📅 "}
                          {new Date(t.dueDate).toLocaleDateString(undefined, {
                            month: "short",
                            day: "numeric",
                          })}
                        </span>
                      ) : (
                        <span style={{ fontSize: 12, color: "var(--text-muted)" }}>—</span>
                      )}
                    </td>

                    <td style={{ padding: "12px 16px" }}>
                      <select
                        className="select"
                        style={{ padding: "4px 8px", fontSize: 12, maxWidth: 130 }}
                        value={t.status}
                        onClick={(e) => e.stopPropagation()}
                        onChange={(e) => handleStatusChange(t.id, e.target.value)}
                      >
                        <option value="Todo">To Do</option>
                        <option value="InProgress">In Progress</option>
                        <option value="InReview">In Review</option>
                        <option value="Done">Done</option>
                        <option value="Cancelled">Cancelled</option>
                      </select>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

      {/* Task Details & Comments Modal */}
      {selectedTask && (
        <div
          className="modal-backdrop"
          onClick={(e) => e.target === e.currentTarget && setSelectedTask(null)}
        >
          <div className="modal animate-fade-in" style={{ maxWidth: 600 }}>
            <div className="flex-between" style={{ marginBottom: 14 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                <span
                  style={{
                    fontSize: 12,
                    fontWeight: 700,
                    color: "var(--brand-400)",
                    fontFamily: "monospace",
                  }}
                >
                  {selectedTask.taskNumber}
                </span>
                <span className={`badge ${statusBadgeClass(selectedTask.status)}`}>
                  {selectedTask.status}
                </span>
              </div>
              <button
                className="btn btn-ghost btn-sm"
                onClick={() => setSelectedTask(null)}
              >
                ✕
              </button>
            </div>

            <h2 style={{ fontSize: 18, fontWeight: 700, color: "var(--text-primary)", marginBottom: 10 }}>
              {selectedTask.title}
            </h2>

            {selectedTask.description && (
              <p style={{ fontSize: 13, color: "var(--text-secondary)", lineHeight: 1.6, marginBottom: 16 }}>
                {selectedTask.description}
              </p>
            )}

            <div style={{ display: "flex", gap: 12, marginBottom: 18, flexWrap: "wrap", fontSize: 12 }}>
              <span className={`badge ${priorityBadgeClass(selectedTask.priority)}`}>
                {selectedTask.priority}
              </span>
              {selectedTask.dueDate && (
                <span style={{ color: "var(--text-muted)" }}>
                  📅 Due: {new Date(selectedTask.dueDate).toLocaleDateString()}
                </span>
              )}
              {selectedTask.storyPoints && (
                <span style={{ color: "var(--text-muted)" }}>
                  🎯 {selectedTask.storyPoints} Story Points
                </span>
              )}
            </div>

            <div style={{ display: "flex", gap: 8, marginBottom: 20 }}>
              <Link
                href={`/dashboard/projects/${selectedTask.projectId}`}
                className="btn btn-secondary btn-sm"
              >
                Go to Project Board →
              </Link>
            </div>

            <hr className="divider" style={{ margin: "16px 0" }} />

            {/* Comments Stream */}
            <h3 style={{ fontSize: 14, fontWeight: 700, marginBottom: 12 }}>Comments</h3>
            <form onSubmit={handleAddComment} style={{ marginBottom: 16 }}>
              <textarea
                className="input"
                placeholder="Write a comment…"
                rows={2}
                value={commentText}
                onChange={(e) => setCommentText(e.target.value)}
                style={{ marginBottom: 8 }}
              />
              <div style={{ display: "flex", justifyContent: "flex-end" }}>
                <button
                  type="submit"
                  className="btn btn-primary btn-sm"
                  disabled={commentLoading || !commentText.trim()}
                >
                  {commentLoading ? "Posting…" : "Post Comment"}
                </button>
              </div>
            </form>

            <div style={{ display: "flex", flexDirection: "column", gap: 10, maxHeight: 220, overflowY: "auto" }}>
              {comments.length === 0 ? (
                <p style={{ fontSize: 12, color: "var(--text-muted)", textAlign: "center" }}>
                  No comments yet.
                </p>
              ) : (
                comments.map((c) => (
                  <div
                    key={c.id}
                    style={{
                      padding: "8px 12px",
                      background: "var(--bg-elevated)",
                      borderRadius: 8,
                      border: "1px solid var(--border)",
                    }}
                  >
                    <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
                      <span style={{ fontSize: 12, fontWeight: 600, color: "var(--text-primary)" }}>
                        {c.authorName}
                      </span>
                      <span style={{ fontSize: 10, color: "var(--text-muted)" }}>
                        {new Date(c.createdAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                      </span>
                    </div>
                    <div style={{ fontSize: 13, color: "var(--text-secondary)" }}>{c.content}</div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
