"use client";

import { useEffect, useState, useCallback, use } from "react";
import { useRouter } from "next/navigation";
import { tasksApi, Task, CreateTaskRequest, TaskHistoryEntry } from "@/services/tasks.service";
import { projectsApi, Member } from "@/services/projects.service";
import { commentsApi, Comment } from "@/services/comments.service";
import { useAuthStore } from "@/stores/auth-store";
import { useSignalR } from "@/lib/use-signalr";
import { CalendarView } from "@/components/calendar/CalendarView";

/* ────────────────────────────────────────────
   Types & constants
──────────────────────────────────────────── */
const STATUSES = [
  { key: "Todo", label: "To Do", dot: "var(--brand-400)" },
  { key: "InProgress", label: "In Progress", dot: "#f59e0b" },
  { key: "InReview", label: "In Review", dot: "#a78bfa" },
  { key: "Done", label: "Done", dot: "#10b981" },
  { key: "Cancelled", label: "Cancelled", dot: "#6b7280" },
];

const PRIORITIES = ["Low", "Medium", "High", "Critical"];
const PROJECT_ROLES = ["ProjectManager", "Member", "Viewer"];

function badgeClass(status: string) {
  const map: Record<string, string> = {
    Todo: "badge-todo",
    InProgress: "badge-inprogress",
    InReview: "badge-inreview",
    Done: "badge-done",
    Cancelled: "badge-cancelled",
  };
  return map[status] ?? "badge-todo";
}

function priorityClass(p: string) {
  const map: Record<string, string> = {
    Low: "badge-low",
    Medium: "badge-medium",
    High: "badge-high",
    Critical: "badge-critical",
  };
  return map[p] ?? "badge-medium";
}

function isOverdue(dueDate: string | null) {
  if (!dueDate) return false;
  return new Date(dueDate) < new Date();
}

/* ────────────────────────────────────────────
   Project Members Modal
──────────────────────────────────────────── */
function ProjectMembersModal({
  projectId,
  members,
  onClose,
  onMembersUpdated,
}: {
  projectId: string;
  members: Member[];
  onClose: () => void;
  onMembersUpdated: () => void;
}) {
  const currentUser = useAuthStore((s) => s.user);
  const [email, setEmail] = useState("");
  const [role, setRole] = useState("Member");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const handleAddMember = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim()) return;
    setError(null);
    setSuccess(null);
    setLoading(true);
    try {
      await projectsApi.addMember(projectId, { email: email.trim(), role });
      setEmail("");
      setSuccess("Member added successfully!");
      onMembersUpdated();
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ||
        "Failed to add member.";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const handleRoleChange = async (userId: string, newRole: string) => {
    try {
      await projectsApi.changeMemberRole(projectId, userId, newRole);
      onMembersUpdated();
    } catch (err) {
      console.error(err);
    }
  };

  const handleRemoveMember = async (userId: string) => {
    if (!confirm("Are you sure you want to remove this member?")) return;
    try {
      await projectsApi.removeMember(projectId, userId);
      onMembersUpdated();
    } catch (err) {
      console.error(err);
    }
  };

  return (
    <div className="modal-backdrop" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal animate-fade-in" style={{ maxWidth: 540 }}>
        <div className="flex-between" style={{ marginBottom: 16 }}>
          <h2 style={{ fontSize: 17, fontWeight: 700, color: "var(--text-primary)" }}>
            Project Members ({members.length})
          </h2>
          <button className="btn btn-ghost btn-sm" onClick={onClose}>
            ✕
          </button>
        </div>

        {/* Add Member Form */}
        <form onSubmit={handleAddMember} style={{ marginBottom: 20 }}>
          <label className="label">Invite Team Member</label>
          <div style={{ display: "flex", gap: 8 }}>
            <input
              type="email"
              className="input"
              placeholder="user@example.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
            <select
              className="select"
              style={{ width: 150 }}
              value={role}
              onChange={(e) => setRole(e.target.value)}
            >
              {PROJECT_ROLES.map((r) => (
                <option key={r} value={r}>
                  {r}
                </option>
              ))}
            </select>
            <button type="submit" className="btn btn-primary" disabled={loading} style={{ whiteSpace: "nowrap" }}>
              {loading ? "Adding…" : "Add"}
            </button>
          </div>
          {error && <p className="error-text" style={{ marginTop: 6 }}>{error}</p>}
          {success && <p style={{ fontSize: 12, color: "#10b981", marginTop: 6 }}>{success}</p>}
        </form>

        <hr className="divider" style={{ margin: "16px 0" }} />

        {/* Members List */}
        <div style={{ display: "flex", flexDirection: "column", gap: 10, maxHeight: 300, overflowY: "auto" }}>
          {members.map((m) => {
            const isSelf = m.userId === currentUser?.userId;
            return (
              <div
                key={m.userId}
                style={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  padding: "8px 12px",
                  background: "var(--bg-elevated)",
                  borderRadius: 8,
                  border: "1px solid var(--border)",
                }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                  <div
                    style={{
                      width: 28,
                      height: 28,
                      borderRadius: "50%",
                      background: "linear-gradient(135deg, var(--brand-500), var(--brand-700))",
                      color: "#fff",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      fontSize: 11,
                      fontWeight: 700,
                    }}
                  >
                    {m.fullName[0]?.toUpperCase() ?? "U"}
                  </div>
                  <div>
                    <div style={{ fontSize: 13, fontWeight: 600, color: "var(--text-primary)" }}>
                      {m.fullName} {isSelf && <span style={{ color: "var(--brand-400)", fontSize: 11 }}>(You)</span>}
                      {m.isOwner && (
                        <span style={{ marginLeft: 6, fontSize: 10, color: "#f59e0b", fontWeight: 700 }}>
                          OWNER
                        </span>
                      )}
                    </div>
                    <div style={{ fontSize: 11, color: "var(--text-muted)" }}>{m.email}</div>
                  </div>
                </div>

                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <select
                    className="select"
                    style={{ padding: "3px 6px", fontSize: 11, width: 130 }}
                    value={m.role}
                    disabled={m.isOwner}
                    onChange={(e) => handleRoleChange(m.userId, e.target.value)}
                  >
                    {PROJECT_ROLES.map((r) => (
                      <option key={r} value={r}>
                        {r}
                      </option>
                    ))}
                  </select>

                  {!m.isOwner && !isSelf && (
                    <button
                      className="btn btn-ghost btn-sm"
                      style={{ color: "#f87171", padding: "4px 8px" }}
                      onClick={() => handleRemoveMember(m.userId)}
                      title="Remove member"
                    >
                      ✕
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

/* ────────────────────────────────────────────
   Create task modal
──────────────────────────────────────────── */
function CreateTaskModal({
  projectId,
  defaultStatus,
  initialDueDate,
  members,
  onClose,
  onCreated,
}: {
  projectId: string;
  defaultStatus: string;
  initialDueDate?: string;
  members: Member[];
  onClose: () => void;
  onCreated: (t: Task) => void;
}) {
  const [form, setForm] = useState<CreateTaskRequest>({
    title: "",
    description: "",
    projectId,
    priority: "Medium",
    dueDate: initialDueDate || "",
    storyPoints: undefined,
    assigneeId: undefined,
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const setDatePreset = (daysAhead: number) => {
    const d = new Date();
    d.setDate(d.getDate() + daysAhead);
    d.setHours(18, 0, 0, 0);
    const pad = (n: number) => n.toString().padStart(2, "0");
    const formatted = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    setForm((f) => ({ ...f, dueDate: formatted }));
  };

  const clearDate = () => setForm((f) => ({ ...f, dueDate: "" }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.title.trim()) {
      setError("Title is required.");
      return;
    }
    setError(null);
    setLoading(true);
    try {
      const payload: CreateTaskRequest = {
        ...form,
        dueDate: form.dueDate || undefined,
        storyPoints: form.storyPoints || undefined,
        assigneeId: form.assigneeId || undefined,
      };
      const t = await tasksApi.createTask(payload);
      onCreated(t);
      onClose();
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ||
        "Failed to create task.";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="modal-backdrop" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal animate-fade-in" style={{ maxWidth: 520 }}>
        <div className="flex-between" style={{ marginBottom: 20 }}>
          <h2 style={{ fontSize: 17, fontWeight: 700, color: "var(--text-primary)" }}>
            New task · {defaultStatus.replace("InProgress", "In Progress").replace("InReview", "In Review")}
          </h2>
          <button className="btn btn-ghost btn-sm" onClick={onClose} id="btn-close-task-modal">
            ✕
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label className="label" htmlFor="task-title">
              Title
            </label>
            <input
              id="task-title"
              type="text"
              className="input"
              placeholder="What needs to be done?"
              value={form.title}
              onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
              required
              autoFocus
            />
          </div>

          <div className="form-group">
            <label className="label" htmlFor="task-desc">
              Description
            </label>
            <textarea
              id="task-desc"
              className="input"
              placeholder="Optional details…"
              rows={3}
              value={form.description}
              onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
            />
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 16 }}>
            <div>
              <label className="label" htmlFor="task-priority">
                Priority
              </label>
              <select
                id="task-priority"
                className="select"
                value={form.priority}
                onChange={(e) => setForm((f) => ({ ...f, priority: e.target.value }))}
              >
                {PRIORITIES.map((p) => (
                  <option key={p} value={p}>
                    {p}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="label" htmlFor="task-points">
                Story points
              </label>
              <input
                id="task-points"
                type="number"
                className="input"
                placeholder="e.g. 5"
                min={1}
                max={100}
                value={form.storyPoints ?? ""}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    storyPoints: e.target.value ? Number(e.target.value) : undefined,
                  }))
                }
              />
            </div>
          </div>

          <div className="form-group">
            <label className="label">Assignee</label>
            <select
              className="select"
              value={form.assigneeId ?? ""}
              onChange={(e) =>
                setForm((f) => ({ ...f, assigneeId: e.target.value || undefined }))
              }
            >
              <option value="">Unassigned</option>
              {members.map((m) => (
                <option key={m.userId} value={m.userId}>
                  {m.fullName} ({m.role})
                </option>
              ))}
            </select>
          </div>

          <div className="form-group">
            <div className="flex-between" style={{ marginBottom: 6 }}>
              <label className="label" htmlFor="task-due" style={{ margin: 0 }}>
                📅 Due date & Calendar Preset
              </label>
              {form.dueDate && (
                <button
                  type="button"
                  onClick={clearDate}
                  style={{ background: "none", border: "none", color: "#f87171", fontSize: 11, cursor: "pointer" }}
                >
                  Clear date
                </button>
              )}
            </div>
            <input
              id="task-due"
              type="datetime-local"
              className="input"
              value={form.dueDate ?? ""}
              onChange={(e) => setForm((f) => ({ ...f, dueDate: e.target.value }))}
            />
            <div className="date-preset-chips">
              <button type="button" className="date-preset-chip" onClick={() => setDatePreset(0)}>
                Today
              </button>
              <button type="button" className="date-preset-chip" onClick={() => setDatePreset(1)}>
                Tomorrow
              </button>
              <button type="button" className="date-preset-chip" onClick={() => setDatePreset(3)}>
                In 3 Days
              </button>
              <button type="button" className="date-preset-chip" onClick={() => setDatePreset(7)}>
                Next Week
              </button>
            </div>
          </div>

          {error && <p className="error-text" style={{ marginBottom: 12 }}>{error}</p>}

          <div style={{ display: "flex", gap: 10, justifyContent: "flex-end", marginTop: 8 }}>
            <button type="button" className="btn btn-secondary" onClick={onClose}>
              Cancel
            </button>
            <button id="btn-create-task-submit" type="submit" className="btn btn-primary" disabled={loading}>
              {loading ? <span className="spinner spinner-sm" /> : null}
              {loading ? "Creating…" : "Create task"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ────────────────────────────────────────────
   Task detail modal (view, edit, comments, audit history)
──────────────────────────────────────────── */
function TaskDetailModal({
  task,
  members,
  onClose,
  onTaskUpdated,
  onTaskDeleted,
}: {
  task: Task;
  members: Member[];
  onClose: () => void;
  onTaskUpdated: (t: Task) => void;
  onTaskDeleted: (taskId: string) => void;
}) {
  const [activeTab, setActiveTab] = useState<"details" | "comments" | "history">("details");
  const [isEditing, setIsEditing] = useState(false);
  const [editForm, setEditForm] = useState({
    title: task.title,
    description: task.description || "",
    priority: task.priority,
    dueDate: task.dueDate ? task.dueDate.substring(0, 16) : "",
    storyPoints: task.storyPoints ?? "",
  });
  const [updating, setUpdating] = useState(false);

  // Comments
  const [comments, setComments] = useState<Comment[]>([]);
  const [newComment, setNewComment] = useState("");
  const [commentLoading, setCommentLoading] = useState(false);

  // History
  const [history, setHistory] = useState<TaskHistoryEntry[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  const loadComments = useCallback(async () => {
    try {
      const c = await commentsApi.getComments(task.id);
      setComments(c);
    } catch {
      setComments([]);
    }
  }, [task.id]);

  const loadHistory = useCallback(async () => {
    setHistoryLoading(true);
    try {
      const h = await tasksApi.getHistory(task.id);
      setHistory(h);
    } catch {
      setHistory([]);
    } finally {
      setHistoryLoading(false);
    }
  }, [task.id]);

  useEffect(() => {
    loadComments();
  }, [loadComments]);

  useEffect(() => {
    if (activeTab === "history") loadHistory();
  }, [activeTab, loadHistory]);

  const handleStatusChange = async (status: string) => {
    setUpdating(true);
    try {
      const updated = await tasksApi.updateStatus(task.id, status);
      onTaskUpdated(updated);
    } finally {
      setUpdating(false);
    }
  };

  const handleAssignChange = async (assigneeId: string) => {
    setUpdating(true);
    try {
      const updated = await tasksApi.assign(task.id, assigneeId || null);
      onTaskUpdated(updated);
    } finally {
      setUpdating(false);
    }
  };

  const handleSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    setUpdating(true);
    try {
      const updated = await tasksApi.updateTask(task.id, {
        title: editForm.title.trim(),
        description: editForm.description || undefined,
        priority: editForm.priority,
        dueDate: editForm.dueDate || undefined,
        storyPoints: editForm.storyPoints ? Number(editForm.storyPoints) : undefined,
      });
      onTaskUpdated(updated);
      setIsEditing(false);
    } finally {
      setUpdating(false);
    }
  };

  const handleDelete = async () => {
    if (!confirm("Are you sure you want to delete this task? This cannot be undone.")) return;
    try {
      await tasksApi.deleteTask(task.id);
      onTaskDeleted(task.id);
      onClose();
    } catch (err) {
      console.error(err);
    }
  };

  const handleAddComment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newComment.trim()) return;
    setCommentLoading(true);
    try {
      const c = await commentsApi.addComment(task.id, newComment.trim());
      setComments((prev) => [c, ...prev]);
      setNewComment("");
    } finally {
      setCommentLoading(false);
    }
  };

  const handleDeleteComment = async (commentId: string) => {
    try {
      await commentsApi.deleteComment(commentId);
      setComments((prev) => prev.filter((c) => c.id !== commentId));
    } catch (err) {
      console.error(err);
    }
  };

  return (
    <div className="modal-backdrop" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal animate-fade-in" style={{ maxWidth: 620 }}>
        {/* Top bar */}
        <div className="flex-between" style={{ marginBottom: 14 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span style={{ fontSize: 12, fontWeight: 700, color: "var(--text-muted)", fontFamily: "monospace" }}>
              {task.taskNumber}
            </span>
            <span className={`badge ${badgeClass(task.status)}`}>
              {task.status.replace("InProgress", "In Progress").replace("InReview", "In Review")}
            </span>
            <span className={`badge ${priorityClass(task.priority)}`}>
              {task.priority}
            </span>
          </div>
          <button className="btn btn-ghost btn-sm" onClick={onClose}>
            ✕
          </button>
        </div>

        {/* Tab Headers */}
        <div style={{ display: "flex", gap: 8, borderBottom: "1px solid var(--border)", marginBottom: 16 }}>
          <button
            className={`btn btn-ghost btn-sm ${activeTab === "details" ? "active" : ""}`}
            style={{
              borderRadius: "4px 4px 0 0",
              borderBottom: activeTab === "details" ? "2px solid var(--brand-500)" : "none",
              color: activeTab === "details" ? "var(--brand-400)" : "var(--text-secondary)",
            }}
            onClick={() => setActiveTab("details")}
          >
            Overview
          </button>
          <button
            className={`btn btn-ghost btn-sm ${activeTab === "comments" ? "active" : ""}`}
            style={{
              borderRadius: "4px 4px 0 0",
              borderBottom: activeTab === "comments" ? "2px solid var(--brand-500)" : "none",
              color: activeTab === "comments" ? "var(--brand-400)" : "var(--text-secondary)",
            }}
            onClick={() => setActiveTab("comments")}
          >
            Comments ({comments.length})
          </button>
          <button
            className={`btn btn-ghost btn-sm ${activeTab === "history" ? "active" : ""}`}
            style={{
              borderRadius: "4px 4px 0 0",
              borderBottom: activeTab === "history" ? "2px solid var(--brand-500)" : "none",
              color: activeTab === "history" ? "var(--brand-400)" : "var(--text-secondary)",
            }}
            onClick={() => setActiveTab("history")}
          >
            Audit Trail
          </button>
        </div>

        {/* Tab: Details */}
        {activeTab === "details" && (
          <div>
            {isEditing ? (
              <form onSubmit={handleSaveEdit}>
                <div className="form-group">
                  <label className="label">Title</label>
                  <input
                    type="text"
                    className="input"
                    value={editForm.title}
                    onChange={(e) => setEditForm((f) => ({ ...f, title: e.target.value }))}
                    required
                  />
                </div>
                <div className="form-group">
                  <label className="label">Description</label>
                  <textarea
                    className="input"
                    rows={3}
                    value={editForm.description}
                    onChange={(e) => setEditForm((f) => ({ ...f, description: e.target.value }))}
                  />
                </div>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10, marginBottom: 12 }}>
                  <div>
                    <label className="label">Priority</label>
                    <select
                      className="select"
                      value={editForm.priority}
                      onChange={(e) => setEditForm((f) => ({ ...f, priority: e.target.value }))}
                    >
                      {PRIORITIES.map((p) => (
                        <option key={p} value={p}>
                          {p}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="label">Story Points</label>
                    <input
                      type="number"
                      className="input"
                      value={editForm.storyPoints}
                      onChange={(e) => setEditForm((f) => ({ ...f, storyPoints: e.target.value }))}
                    />
                  </div>
                </div>
                <div className="form-group">
                  <label className="label">Due Date</label>
                  <input
                    type="datetime-local"
                    className="input"
                    value={editForm.dueDate}
                    onChange={(e) => setEditForm((f) => ({ ...f, dueDate: e.target.value }))}
                  />
                </div>
                <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
                  <button type="button" className="btn btn-secondary btn-sm" onClick={() => setIsEditing(false)}>
                    Cancel
                  </button>
                  <button type="submit" className="btn btn-primary btn-sm" disabled={updating}>
                    Save Changes
                  </button>
                </div>
              </form>
            ) : (
              <div>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 10 }}>
                  <h2 style={{ fontSize: 18, fontWeight: 700, color: "var(--text-primary)", lineHeight: 1.4 }}>
                    {task.title}
                  </h2>
                  <button
                    className="btn btn-secondary btn-sm"
                    style={{ padding: "3px 8px", fontSize: 11 }}
                    onClick={() => setIsEditing(true)}
                  >
                    ✎ Edit
                  </button>
                </div>

                {task.description && (
                  <p style={{ fontSize: 13, color: "var(--text-secondary)", lineHeight: 1.7, marginBottom: 16 }}>
                    {task.description}
                  </p>
                )}

                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16, marginBottom: 20 }}>
                  <div>
                    <label className="label">Assignee</label>
                    <select
                      className="select"
                      style={{ fontSize: 12 }}
                      value={task.assigneeId ?? ""}
                      disabled={updating}
                      onChange={(e) => handleAssignChange(e.target.value)}
                    >
                      <option value="">Unassigned</option>
                      {members.map((m) => (
                        <option key={m.userId} value={m.userId}>
                          {m.fullName}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="label">Due Date</label>
                    <div style={{ fontSize: 13, color: isOverdue(task.dueDate) ? "#f87171" : "var(--text-secondary)", marginTop: 6 }}>
                      {task.dueDate ? `📅 ${new Date(task.dueDate).toLocaleString()}` : "No due date"}
                    </div>
                  </div>
                </div>

                <hr className="divider" style={{ margin: "16px 0" }} />

                <div style={{ marginBottom: 16 }}>
                  <label className="label">Transition Status</label>
                  <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
                    {STATUSES.filter((s) => s.key !== task.status).map((s) => (
                      <button
                        key={s.key}
                        className="btn btn-secondary btn-sm"
                        disabled={updating}
                        onClick={() => handleStatusChange(s.key)}
                      >
                        {s.label}
                      </button>
                    ))}
                  </div>
                </div>

                <div style={{ display: "flex", justifyContent: "flex-end" }}>
                  <button
                    className="btn btn-danger btn-sm"
                    style={{ fontSize: 11 }}
                    onClick={handleDelete}
                  >
                    Delete Task
                  </button>
                </div>
              </div>
            )}
          </div>
        )}

        {/* Tab: Comments */}
        {activeTab === "comments" && (
          <div>
            <form onSubmit={handleAddComment} style={{ marginBottom: 16 }}>
              <textarea
                className="input"
                placeholder="Write a comment…"
                rows={2}
                value={newComment}
                onChange={(e) => setNewComment(e.target.value)}
                style={{ marginBottom: 8 }}
              />
              <div style={{ display: "flex", justifyContent: "flex-end" }}>
                <button
                  type="submit"
                  className="btn btn-primary btn-sm"
                  disabled={commentLoading || !newComment.trim()}
                >
                  {commentLoading ? "Posting…" : "Post Comment"}
                </button>
              </div>
            </form>

            <div style={{ display: "flex", flexDirection: "column", gap: 10, maxHeight: 260, overflowY: "auto" }}>
              {comments.length === 0 ? (
                <div style={{ padding: 24, textAlign: "center", color: "var(--text-muted)", fontSize: 13 }}>
                  No comments yet. Be the first to share an update!
                </div>
              ) : (
                comments.map((c) => (
                  <div
                    key={c.id}
                    style={{
                      padding: "10px 12px",
                      background: "var(--bg-elevated)",
                      borderRadius: 8,
                      border: "1px solid var(--border)",
                    }}
                  >
                    <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
                      <span style={{ fontSize: 12, fontWeight: 600, color: "var(--text-primary)" }}>
                        {c.authorName}
                      </span>
                      <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                        <span style={{ fontSize: 10, color: "var(--text-muted)" }}>
                          {new Date(c.createdAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                        </span>
                        <button
                          onClick={() => handleDeleteComment(c.id)}
                          style={{ background: "none", border: "none", color: "var(--text-muted)", cursor: "pointer", fontSize: 11 }}
                          title="Delete comment"
                        >
                          ✕
                        </button>
                      </div>
                    </div>
                    <div style={{ fontSize: 13, color: "var(--text-secondary)", lineHeight: 1.5 }}>
                      {c.content}
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        )}

        {/* Tab: History */}
        {activeTab === "history" && (
          <div>
            {historyLoading ? (
              <div style={{ padding: 24, textAlign: "center", color: "var(--text-muted)" }}>
                Loading audit trail…
              </div>
            ) : history.length === 0 ? (
              <div style={{ padding: 24, textAlign: "center", color: "var(--text-muted)", fontSize: 13 }}>
                No audit entries recorded yet.
              </div>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 8, maxHeight: 280, overflowY: "auto" }}>
                {history.map((h, i) => (
                  <div
                    key={i}
                    style={{
                      padding: "8px 12px",
                      background: "var(--bg-elevated)",
                      borderRadius: 6,
                      fontSize: 12,
                      border: "1px solid var(--border)",
                    }}
                  >
                    <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 2 }}>
                      <span style={{ fontWeight: 600, color: "var(--brand-400)" }}>{h.action}</span>
                      <span style={{ fontSize: 10, color: "var(--text-muted)" }}>
                        {new Date(h.at).toLocaleString()}
                      </span>
                    </div>
                    <div style={{ color: "var(--text-secondary)", fontSize: 11 }}>
                      by <span style={{ color: "var(--text-primary)" }}>{h.userEmail}</span>
                    </div>
                    {(h.oldValue || h.newValue) && (
                      <div style={{ marginTop: 4, fontSize: 11, color: "var(--text-muted)" }}>
                        {h.oldValue && <span>From: {h.oldValue} </span>}
                        {h.newValue && <span>To: {h.newValue}</span>}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

/* ────────────────────────────────────────────
   Kanban column with Drag-and-Drop
──────────────────────────────────────────── */
function KanbanColumn({
  status,
  tasks,
  onAddTask,
  onCardClick,
  onDropTask,
}: {
  status: (typeof STATUSES)[number];
  tasks: Task[];
  onAddTask: (status: string) => void;
  onCardClick: (task: Task) => void;
  onDropTask: (taskId: string, targetStatus: string) => void;
}) {
  const [isOver, setIsOver] = useState(false);

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsOver(true);
  };

  const handleDragLeave = () => {
    setIsOver(false);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsOver(false);
    const taskId = e.dataTransfer.getData("text/plain");
    if (taskId) {
      onDropTask(taskId, status.key);
    }
  };

  return (
    <div
      className="kanban-column"
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
      style={{
        border: isOver ? "2px dashed var(--brand-500)" : "1px solid var(--border)",
        background: isOver ? "rgba(99, 102, 241, 0.05)" : undefined,
        transition: "border 0.15s, background 0.15s",
      }}
    >
      <div className="kanban-column-header">
        <div className="kanban-column-title">
          <span className="kanban-column-dot" style={{ background: status.dot }} />
          {status.label}
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <span className="kanban-column-count">{tasks.length}</span>
          <button
            className="btn btn-ghost btn-sm"
            style={{ padding: "4px 6px", fontSize: 16, lineHeight: 1 }}
            onClick={() => onAddTask(status.key)}
            title={`Add task to ${status.label}`}
          >
            +
          </button>
        </div>
      </div>
      <div className="kanban-cards">
        {tasks.map((task) => (
          <div
            key={task.id}
            className="kanban-card"
            draggable
            onDragStart={(e) => {
              e.dataTransfer.setData("text/plain", task.id);
            }}
            onClick={() => onCardClick(task)}
          >
            <div className="kanban-card-number">{task.taskNumber}</div>
            <div className="kanban-card-title">{task.title}</div>
            <div style={{ display: "flex", flexWrap: "wrap", gap: 4, marginBottom: 10 }}>
              <span className={`badge ${priorityClass(task.priority)}`}>{task.priority}</span>
              {task.assigneeName && (
                <span
                  style={{
                    background: "var(--bg-hover)",
                    color: "var(--text-muted)",
                    padding: "2px 7px",
                    borderRadius: 999,
                    fontSize: 11,
                  }}
                >
                  {task.assigneeName.split(" ")[0]}
                </span>
              )}
            </div>
            <div className="kanban-card-footer">
              {task.storyPoints ? (
                <span className="kanban-card-sp">{task.storyPoints} SP</span>
              ) : (
                <span />
              )}
              {task.dueDate && (
                <span className={`kanban-card-due${isOverdue(task.dueDate) ? " overdue" : ""}`}>
                  {new Date(task.dueDate).toLocaleDateString(undefined, { month: "short", day: "numeric" })}
                </span>
              )}
            </div>
          </div>
        ))}
        {tasks.length === 0 && (
          <div
            className="empty-state"
            style={{ padding: "24px 12px", cursor: "pointer" }}
            onClick={() => onAddTask(status.key)}
          >
            <div style={{ fontSize: 11, color: "var(--text-muted)" }}>+ Add task</div>
          </div>
        )}
      </div>
    </div>
  );
}

/* ────────────────────────────────────────────
   Main board page
──────────────────────────────────────────── */
export default function ProjectBoardPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id: projectId } = use(params);
  const router = useRouter();
  const currentUser = useAuthStore((s) => s.user);

  const [tasks, setTasks] = useState<Task[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [filterPriority, setFilterPriority] = useState("");
  const [filterAssignee, setFilterAssignee] = useState("");
  const [dateFilter, setDateFilter] = useState("");
  const [viewMode, setViewMode] = useState<"board" | "calendar">("board");
  const [createFor, setCreateFor] = useState<string | null>(null);
  const [calendarInitialDate, setCalendarInitialDate] = useState<string | undefined>(undefined);
  const [selectedTask, setSelectedTask] = useState<Task | null>(null);
  const [showMembersModal, setShowMembersModal] = useState(false);

  const fetchMembers = useCallback(async () => {
    try {
      const m = await projectsApi.getMembers(projectId);
      setMembers(m);
    } catch {
      // ignore
    }
  }, [projectId]);

  const fetchTasks = useCallback(async () => {
    setLoading(true);
    try {
      const r = await tasksApi.getTasks(projectId, { pageSize: 200 });
      setTasks(r.items);
      await fetchMembers();
    } catch {
      router.push("/dashboard/projects");
    } finally {
      setLoading(false);
    }
  }, [projectId, router, fetchMembers]);

  useEffect(() => {
    fetchTasks();
  }, [fetchTasks]);

  const upsertTask = useCallback((task: Task) => {
    setTasks((prev) => {
      const idx = prev.findIndex((x) => x.id.toLowerCase() === task.id.toLowerCase());
      if (idx >= 0) {
        const next = [...prev];
        next[idx] = task;
        return next;
      }
      return [task, ...prev];
    });
  }, []);

  const removeTaskState = useCallback((taskId: string) => {
    setTasks((prev) => prev.filter((x) => x.id.toLowerCase() !== taskId.toLowerCase()));
  }, []);

  // Real-time updates via SignalR
  useSignalR(projectId, {
    onTaskCreated: (t) => {
      if (t && (t as Task).id) upsertTask(t as Task);
    },
    onTaskUpdated: (t) => {
      if (t && (t as Task).id) upsertTask(t as Task);
    },
    onTaskStatusUpdated: (t) => {
      if (t && (t as Task).id) upsertTask(t as Task);
    },
    onTaskDeleted: (payload) => {
      if (payload?.taskId) removeTaskState(payload.taskId);
    },
  });

  const handleDropTask = async (taskId: string, targetStatus: string) => {
    // Optimistic update
    setTasks((prev) =>
      prev.map((t) => (t.id.toLowerCase() === taskId.toLowerCase() ? { ...t, status: targetStatus } : t))
    );
    try {
      await tasksApi.updateStatus(taskId, targetStatus);
    } catch (err) {
      console.error(err);
      fetchTasks();
    }
  };

  const filtered = tasks.filter((t) => {
    const q = search.toLowerCase();
    const matchSearch =
      !q || t.title.toLowerCase().includes(q) || t.taskNumber.toLowerCase().includes(q);
    const matchPriority = !filterPriority || t.priority === filterPriority;

    let matchAssignee = true;
    if (filterAssignee === "me") {
      matchAssignee = t.assigneeId === currentUser?.userId;
    } else if (filterAssignee === "unassigned") {
      matchAssignee = !t.assigneeId;
    } else if (filterAssignee) {
      matchAssignee = t.assigneeId === filterAssignee;
    }

    let matchDate = true;
    if (dateFilter === "overdue") {
      matchDate = !!t.dueDate && isOverdue(t.dueDate) && t.status !== "Done";
    } else if (dateFilter === "today") {
      if (!t.dueDate) matchDate = false;
      else {
        const td = new Date(t.dueDate);
        const today = new Date();
        matchDate = td.toDateString() === today.toDateString();
      }
    } else if (dateFilter === "week") {
      if (!t.dueDate) matchDate = false;
      else {
        const td = new Date(t.dueDate);
        const now = new Date();
        const in7Days = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000);
        matchDate = td >= now && td <= in7Days;
      }
    } else if (dateFilter === "has_date") {
      matchDate = !!t.dueDate;
    } else if (dateFilter === "no_date") {
      matchDate = !t.dueDate;
    }

    return matchSearch && matchPriority && matchAssignee && matchDate;
  });

  const tasksByStatus = (status: string) => filtered.filter((t) => t.status === status);

  const totalSP = tasks.reduce((sum, t) => sum + (t.storyPoints ?? 0), 0);
  const doneSP = tasks.filter((t) => t.status === "Done").reduce((sum, t) => sum + (t.storyPoints ?? 0), 0);

  return (
    <div className="animate-fade-in" style={{ display: "flex", flexDirection: "column", height: "100%" }}>
      {/* Header */}
      <div className="page-header" style={{ marginBottom: 16 }}>
        <div>
          <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4 }}>
            <button
              className="btn btn-ghost btn-sm"
              style={{ padding: "4px 8px", fontSize: 12 }}
              onClick={() => router.push("/dashboard/projects")}
            >
              ← Projects
            </button>
          </div>
          <h1 className="page-title" style={{ fontSize: 18 }}>
            {tasks[0]?.projectKey ?? "Board"}
          </h1>
          <p className="page-subtitle">
            {tasks.length} tasks · {doneSP}/{totalSP} SP done
          </p>
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
          {/* Member avatars & Manage members */}
          <div
            style={{ display: "flex", alignItems: "center", gap: 6, cursor: "pointer" }}
            onClick={() => setShowMembersModal(true)}
            title="Manage project members"
          >
            <div style={{ display: "flex", marginLeft: 4 }}>
              {members.slice(0, 3).map((m, idx) => (
                <div
                  key={m.userId}
                  style={{
                    width: 26,
                    height: 26,
                    borderRadius: "50%",
                    background: "var(--brand-600)",
                    color: "#fff",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    fontSize: 10,
                    fontWeight: 700,
                    border: "2px solid var(--bg-surface)",
                    marginLeft: idx > 0 ? -6 : 0,
                  }}
                >
                  {m.fullName[0]?.toUpperCase() ?? "U"}
                </div>
              ))}
            </div>
            <button className="btn btn-secondary btn-sm" style={{ padding: "4px 8px", fontSize: 12 }}>
              👥 Members ({members.length})
            </button>
          </div>

          {/* View Toggle */}
          <div className="view-toggle">
            <button
              id="toggle-view-board"
              type="button"
              className={`view-toggle-btn ${viewMode === "board" ? "active" : ""}`}
              onClick={() => setViewMode("board")}
            >
              <span>☷</span> Board
            </button>
            <button
              id="toggle-view-calendar"
              type="button"
              className={`view-toggle-btn ${viewMode === "calendar" ? "active" : ""}`}
              onClick={() => setViewMode("calendar")}
            >
              <span>📅</span> Calendar
            </button>
          </div>

          <button
            id="btn-add-task"
            className="btn btn-primary"
            onClick={() => {
              setCalendarInitialDate(undefined);
              setCreateFor("Todo");
            }}
          >
            + New task
          </button>
        </div>
      </div>

      {/* Filters */}
      <div style={{ display: "flex", gap: 10, marginBottom: 16, flexWrap: "wrap", alignItems: "center" }}>
        <input
          id="board-search"
          type="search"
          className="input"
          placeholder="Search tasks…"
          style={{ maxWidth: 200 }}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <select
          id="board-filter-priority"
          className="select"
          style={{ maxWidth: 140 }}
          value={filterPriority}
          onChange={(e) => setFilterPriority(e.target.value)}
        >
          <option value="">All priorities</option>
          {PRIORITIES.map((p) => (
            <option key={p} value={p}>
              {p}
            </option>
          ))}
        </select>
        <select
          className="select"
          style={{ maxWidth: 160 }}
          value={filterAssignee}
          onChange={(e) => setFilterAssignee(e.target.value)}
        >
          <option value="">All Assignees</option>
          <option value="me">Assigned to Me</option>
          <option value="unassigned">Unassigned</option>
          {members.map((m) => (
            <option key={m.userId} value={m.userId}>
              {m.fullName}
            </option>
          ))}
        </select>
        <select
          id="board-filter-date"
          className="select"
          style={{ maxWidth: 150 }}
          value={dateFilter}
          onChange={(e) => setDateFilter(e.target.value)}
        >
          <option value="">All dates</option>
          <option value="overdue">⚠ Overdue</option>
          <option value="today">Due Today</option>
          <option value="week">Due This Week</option>
          <option value="has_date">Has Due Date</option>
          <option value="no_date">No Due Date</option>
        </select>
      </div>

      {/* Main Content Area */}
      {viewMode === "calendar" ? (
        <CalendarView
          tasks={filtered}
          onTaskClick={(task) => setSelectedTask(task)}
          onDateClick={(date) => {
            const pad = (n: number) => n.toString().padStart(2, "0");
            const dStr = `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T17:00`;
            setCalendarInitialDate(dStr);
            setCreateFor("Todo");
          }}
        />
      ) : loading ? (
        <div className="kanban-board">
          {STATUSES.map((s) => (
            <div key={s.key} className="kanban-column">
              <div className="kanban-column-header">
                <div className="kanban-column-title">
                  <span className="kanban-column-dot" style={{ background: s.dot }} />
                  {s.label}
                </div>
              </div>
              <div className="kanban-cards">
                {[1, 2].map((i) => (
                  <div key={i} className="kanban-card" style={{ cursor: "default" }}>
                    <div className="skeleton" style={{ height: 10, width: "50%", marginBottom: 8 }} />
                    <div className="skeleton" style={{ height: 14, width: "90%", marginBottom: 6 }} />
                    <div className="skeleton" style={{ height: 10, width: "40%" }} />
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="kanban-board">
          {STATUSES.map((s) => (
            <KanbanColumn
              key={s.key}
              status={s}
              tasks={tasksByStatus(s.key)}
              onAddTask={(status) => {
                setCalendarInitialDate(undefined);
                setCreateFor(status);
              }}
              onCardClick={(task) => setSelectedTask(task)}
              onDropTask={handleDropTask}
            />
          ))}
        </div>
      )}

      {/* Modals */}
      {createFor && (
        <CreateTaskModal
          projectId={projectId}
          defaultStatus={createFor}
          initialDueDate={calendarInitialDate}
          members={members}
          onClose={() => {
            setCreateFor(null);
            setCalendarInitialDate(undefined);
          }}
          onCreated={(t) => upsertTask(t)}
        />
      )}

      {selectedTask && (
        <TaskDetailModal
          task={selectedTask}
          members={members}
          onClose={() => setSelectedTask(null)}
          onTaskUpdated={(t) => {
            upsertTask(t);
            setSelectedTask(t);
          }}
          onTaskDeleted={(id) => {
            removeTaskState(id);
            setSelectedTask(null);
          }}
        />
      )}

      {showMembersModal && (
        <ProjectMembersModal
          projectId={projectId}
          members={members}
          onClose={() => setShowMembersModal(false)}
          onMembersUpdated={fetchMembers}
        />
      )}
    </div>
  );
}
