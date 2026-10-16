"use client";

import { useEffect, useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import { usersApi, UserItem } from "@/services/users.service";
import { useAuthStore } from "@/stores/auth-store";
import { Icon } from "@/components/icons";

const ROLES = ["Admin", "ProjectManager", "Member", "Viewer"];

export default function AdminUsersPage() {
  const router = useRouter();
  const currentUser = useAuthStore((s) => s.user);

  const [users, setUsers] = useState<UserItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [roleFilter, setRoleFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("");
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [message, setMessage] = useState<{ text: string; type: "success" | "error" } | null>(null);

  useEffect(() => {
    if (currentUser && currentUser.role !== "Admin") {
      router.replace("/dashboard");
    }
  }, [currentUser, router]);

  const fetchUsers = useCallback(async () => {
    setLoading(true);
    try {
      const isActive =
        statusFilter === "active" ? true : statusFilter === "inactive" ? false : undefined;
      const res = await usersApi.getUsers(page, 15, roleFilter || undefined, isActive);
      setUsers(res.items);
      setTotalPages(res.totalPages || 1);
    } catch (err) {
      console.error(err);
      setMessage({ text: "Failed to load users.", type: "error" });
    } finally {
      setLoading(false);
    }
  }, [page, roleFilter, statusFilter]);

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  const handleRoleChange = async (userId: string, newRole: string) => {
    setActionLoading(userId);
    try {
      await usersApi.changeRole(userId, newRole);
      setUsers((prev) =>
        prev.map((u) => (u.id === userId ? { ...u, role: newRole } : u))
      );
      setMessage({ text: "Role updated successfully.", type: "success" });
    } catch {
      setMessage({ text: "Failed to update role.", type: "error" });
    } finally {
      setActionLoading(null);
    }
  };

  const handleToggleActive = async (userId: string, currentActive: boolean) => {
    setActionLoading(userId);
    try {
      await usersApi.setActive(userId, !currentActive);
      setUsers((prev) =>
        prev.map((u) => (u.id === userId ? { ...u, isActive: !currentActive } : u))
      );
      setMessage({
        text: `User ${!currentActive ? "activated" : "deactivated"} successfully.`,
        type: "success",
      });
    } catch {
      setMessage({ text: "Failed to update user status.", type: "error" });
    } finally {
      setActionLoading(null);
    }
  };

  return (
    <div className="animate-fade-in" style={{ maxWidth: 1100, margin: "0 auto" }}>
      <div className="page-header" style={{ marginBottom: 20 }}>
        <div>
          <h1 className="page-title">User Administration & RBAC</h1>
          <p className="page-subtitle">Manage organization accounts, permissions, and roles</p>
        </div>
      </div>

      {message && (
        <div
          style={{
            padding: "10px 14px",
            borderRadius: 8,
            marginBottom: 16,
            fontSize: 13,
            background: message.type === "success" ? "rgba(16, 185, 129, 0.15)" : "rgba(239, 68, 68, 0.15)",
            color: message.type === "success" ? "#10b981" : "#f87171",
            border: `1px solid ${message.type === "success" ? "rgba(16, 185, 129, 0.3)" : "rgba(239, 68, 68, 0.3)"}`,
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <span>{message.text}</span>
          <button
            onClick={() => setMessage(null)}
            style={{ background: "none", border: "none", color: "inherit", cursor: "pointer" }}
            aria-label="Dismiss message"
          >
            <Icon name="x" />
          </button>
        </div>
      )}

      {/* Filters */}
      <div style={{ display: "flex", gap: 12, marginBottom: 20, flexWrap: "wrap" }}>
        <select
          className="select"
          style={{ maxWidth: 180 }}
          value={roleFilter}
          onChange={(e) => {
            setRoleFilter(e.target.value);
            setPage(1);
          }}
        >
          <option value="">All Roles</option>
          {ROLES.map((r) => (
            <option key={r} value={r}>
              {r}
            </option>
          ))}
        </select>

        <select
          className="select"
          style={{ maxWidth: 180 }}
          value={statusFilter}
          onChange={(e) => {
            setStatusFilter(e.target.value);
            setPage(1);
          }}
        >
          <option value="">All Statuses</option>
          <option value="active">Active Only</option>
          <option value="inactive">Inactive Only</option>
        </select>
      </div>

      {/* Table */}
      <div className="card" style={{ padding: 0, overflow: "hidden" }}>
        <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "left" }}>
          <thead>
            <tr style={{ background: "var(--bg-elevated)", borderBottom: "1px solid var(--border)" }}>
              <th style={{ padding: "12px 16px", fontSize: 12, fontWeight: 600, color: "var(--text-secondary)" }}>
                User
              </th>
              <th style={{ padding: "12px 16px", fontSize: 12, fontWeight: 600, color: "var(--text-secondary)" }}>
                System Role
              </th>
              <th style={{ padding: "12px 16px", fontSize: 12, fontWeight: 600, color: "var(--text-secondary)" }}>
                Status
              </th>
              <th style={{ padding: "12px 16px", fontSize: 12, fontWeight: 600, color: "var(--text-secondary)" }}>
                Last Login
              </th>
              <th style={{ padding: "12px 16px", fontSize: 12, fontWeight: 600, color: "var(--text-secondary)" }}>
                Actions
              </th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={5} style={{ padding: 32, textAlign: "center", color: "var(--text-muted)" }}>
                  <div className="spinner" style={{ margin: "0 auto 8px" }} />
                  Loading accounts…
                </td>
              </tr>
            ) : users.length === 0 ? (
              <tr>
                <td colSpan={5} style={{ padding: 32, textAlign: "center", color: "var(--text-muted)" }}>
                  No accounts found.
                </td>
              </tr>
            ) : (
              users.map((u) => {
                const isSelf = u.id === currentUser?.userId;
                return (
                  <tr
                    key={u.id}
                    style={{
                      borderBottom: "1px solid var(--border)",
                      transition: "background 0.15s",
                    }}
                  >
                    <td style={{ padding: "12px 16px" }}>
                      <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                        <div
                          style={{
                            width: 32,
                            height: 32,
                            borderRadius: "50%",
                            background: "linear-gradient(135deg, var(--brand-500), var(--brand-700))",
                            color: "#fff",
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                            fontSize: 12,
                            fontWeight: 700,
                            flexShrink: 0,
                          }}
                        >
                          {u.firstName?.[0]?.toUpperCase() ?? "U"}
                        </div>
                        <div>
                          <div style={{ fontWeight: 600, color: "var(--text-primary)", fontSize: 13 }}>
                            {u.fullName} {isSelf && <span style={{ color: "var(--brand-400)", fontSize: 11 }}>(You)</span>}
                          </div>
                          <div style={{ fontSize: 12, color: "var(--text-muted)" }}>{u.email}</div>
                        </div>
                      </div>
                    </td>

                    <td style={{ padding: "12px 16px" }}>
                      <select
                        className="select"
                        style={{ padding: "4px 8px", fontSize: 12, maxWidth: 140 }}
                        value={u.role}
                        disabled={isSelf || actionLoading === u.id}
                        onChange={(e) => handleRoleChange(u.id, e.target.value)}
                      >
                        {ROLES.map((r) => (
                          <option key={r} value={r}>
                            {r}
                          </option>
                        ))}
                      </select>
                    </td>

                    <td style={{ padding: "12px 16px" }}>
                      <span
                        className={`badge ${u.isActive ? "badge-done" : "badge-cancelled"}`}
                        style={{ fontSize: 11 }}
                      >
                        {u.isActive ? "Active" : "Deactivated"}
                      </span>
                    </td>

                    <td style={{ padding: "12px 16px", fontSize: 12, color: "var(--text-muted)" }}>
                      {u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : "Never"}
                    </td>

                    <td style={{ padding: "12px 16px" }}>
                      {!isSelf && (
                        <button
                          className={`btn btn-sm ${u.isActive ? "btn-danger" : "btn-secondary"}`}
                          style={{ padding: "4px 10px", fontSize: 11 }}
                          disabled={actionLoading === u.id}
                          onClick={() => handleToggleActive(u.id, u.isActive)}
                        >
                          {u.isActive ? "Deactivate" : "Activate"}
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>

        {totalPages > 1 && (
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "12px 16px" }}>
            <button
              className="btn btn-secondary btn-sm"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1}
            >
              <Icon name="chevronLeft" size={14} /> Previous
            </button>
            <span style={{ fontSize: 12, color: "var(--text-muted)" }}>
              Page {page} of {totalPages}
            </span>
            <button
              className="btn btn-secondary btn-sm"
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page === totalPages}
            >
              Next <Icon name="chevronRight" size={14} />
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
