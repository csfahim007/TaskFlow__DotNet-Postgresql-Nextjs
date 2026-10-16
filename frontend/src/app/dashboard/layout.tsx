"use client";

import { useEffect, useState, useRef } from "react";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import { useAuthStore } from "@/stores/auth-store";
import { authApi } from "@/services/auth.service";
import { notificationsApi, NotificationItem } from "@/services/notifications.service";
import { useSignalR } from "@/lib/use-signalr";
import { BrandLogo, Icon, type IconName } from "@/components/icons";

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const { isAuthenticated, hasHydrated, user, logout } = useAuthStore();

  // Notifications
  const [notifications, setNotifications] = useState<NotificationItem[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [showNotifPopover, setShowNotifPopover] = useState(false);
  const notifRef = useRef<HTMLDivElement>(null);

  // Change Password Modal
  const [showPasswordModal, setShowPasswordModal] = useState(false);
  const [pwdForm, setPwdForm] = useState({ currentPassword: "", newPassword: "", confirmPassword: "" });
  const [pwdLoading, setPwdLoading] = useState(false);
  const [pwdMsg, setPwdMsg] = useState<{ text: string; error?: boolean } | null>(null);

  useEffect(() => {
    if (hasHydrated && !isAuthenticated) router.replace("/login");
  }, [hasHydrated, isAuthenticated, router]);

  // Load initial notifications
  const loadNotifications = async () => {
    try {
      const res = await notificationsApi.getNotifications(1, 20);
      setNotifications(res.notifications.items);
      setUnreadCount(res.unreadCount);
    } catch {
      // ignore
    }
  };

  useEffect(() => {
    if (isAuthenticated) loadNotifications();
  }, [isAuthenticated]);

  // Click outside to close notifications popover
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (notifRef.current && !notifRef.current.contains(event.target as Node)) {
        setShowNotifPopover(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  // Listen to global SignalR notifications
  useSignalR(null, {
    onNotificationReceived: (notif) => {
      if (notif) {
        setNotifications((prev) => [notif, ...prev]);
        setUnreadCount((c) => c + 1);
      }
    },
  });

  const handleMarkAllRead = async () => {
    try {
      await notificationsApi.markAllAsRead();
      setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
      setUnreadCount(0);
    } catch (e) {
      console.error(e);
    }
  };

  const handleMarkRead = async (n: NotificationItem) => {
    if (!n.isRead) {
      try {
        await notificationsApi.markAsRead(n.id);
        setNotifications((prev) =>
          prev.map((item) => (item.id === n.id ? { ...item, isRead: true } : item))
        );
        setUnreadCount((c) => Math.max(0, c - 1));
      } catch (e) {
        console.error(e);
      }
    }
    setShowNotifPopover(false);
    if (n.projectId) {
      router.push(`/dashboard/projects/${n.projectId}`);
    }
  };

  const handleChangePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (pwdForm.newPassword !== pwdForm.confirmPassword) {
      setPwdMsg({ text: "Passwords do not match.", error: true });
      return;
    }
    setPwdLoading(true);
    setPwdMsg(null);
    try {
      await authApi.changePassword(pwdForm);
      setPwdMsg({ text: "Password changed successfully! Please log in again." });
      setTimeout(() => {
        logout();
        router.replace("/login");
      }, 1500);
    } catch (err: unknown) {
      const data = (err as {
        response?: {
          data?: {
            message?: string;
            errors?: Record<string, string[]>;
          };
        };
      })?.response?.data;
      const validationMessage = data?.errors
        ? Object.values(data.errors).flat().join(" ")
        : undefined;
      const msg = data?.message || validationMessage || "Failed to change password.";
      setPwdMsg({ text: msg, error: true });
    } finally {
      setPwdLoading(false);
    }
  };

  if (!hasHydrated || !isAuthenticated) return null;

  const handleLogout = () => {
    logout();
    router.replace("/login");
  };

  const navItems: { href: string; label: string; icon: IconName }[] = [
    { href: "/dashboard", label: "Dashboard", icon: "home" },
    { href: "/dashboard/my-tasks", label: "My Tasks", icon: "checkSquare" },
    { href: "/dashboard/projects", label: "Projects", icon: "folder" },
  ];

  if (user?.role === "Admin") {
    navItems.push({ href: "/dashboard/admin/users", label: "Admin Users", icon: "shield" });
  }

  return (
    <div style={{ display: "flex", minHeight: "100vh" }}>
      {/* Sidebar */}
      <aside className="sidebar">
        <div className="sidebar-logo">
          <BrandLogo />
        </div>

        <nav className="sidebar-nav">
          {navItems.map(({ href, label, icon }) => {
            const active =
              pathname === href || (href !== "/dashboard" && pathname.startsWith(href));
            return (
              <Link key={href} href={href} className={`sidebar-nav-item${active ? " active" : ""}`}>
                <Icon name={icon} color={active ? "var(--brand-400)" : "currentColor"} />
                {label}
              </Link>
            );
          })}
        </nav>

        <div className="sidebar-footer">
          {/* User info */}
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 10,
              padding: "8px 12px",
              marginBottom: 4,
            }}
          >
            <div
              style={{
                width: 28,
                height: 28,
                borderRadius: "50%",
                background: "linear-gradient(135deg, var(--brand-500), var(--brand-700))",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                fontSize: 12,
                fontWeight: 700,
                color: "#fff",
                flexShrink: 0,
              }}
            >
              {user?.fullName?.[0]?.toUpperCase() ?? "U"}
            </div>
            <div style={{ overflow: "hidden" }}>
              <div
                style={{
                  fontSize: 12,
                  fontWeight: 600,
                  color: "var(--text-primary)",
                  whiteSpace: "nowrap",
                  overflow: "hidden",
                  textOverflow: "ellipsis",
                }}
              >
                {user?.fullName}
              </div>
              <div
                style={{
                  fontSize: 11,
                  color: "var(--text-muted)",
                  whiteSpace: "nowrap",
                  overflow: "hidden",
                  textOverflow: "ellipsis",
                }}
              >
                {user?.role}
              </div>
            </div>
          </div>

          <button
            className="sidebar-nav-item"
            onClick={() => setShowPasswordModal(true)}
            style={{ marginBottom: 2 }}
          >
            <Icon name="key" size={15} />
            Change Password
          </button>

          <button className="sidebar-nav-item" onClick={handleLogout} id="btn-logout">
            <Icon name="logOut" />
            Sign out
          </button>
        </div>
      </aside>

      {/* Main Area with Header */}
      <div style={{ flex: 1, display: "flex", flexDirection: "column", minWidth: 0 }}>
        {/* Top Header */}
        <header
          style={{
            height: 56,
            borderBottom: "1px solid var(--border)",
            background: "var(--bg-surface)",
            display: "flex",
            alignItems: "center",
            justifyContent: "flex-end",
            padding: "0 32px",
            gap: 16,
          }}
        >
          {/* Notification Bell */}
          <div ref={notifRef} style={{ position: "relative" }}>
            <button
              onClick={() => setShowNotifPopover(!showNotifPopover)}
              style={{
                background: "var(--bg-elevated)",
                border: "1px solid var(--border)",
                borderRadius: 8,
                width: 36,
                height: 36,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                cursor: "pointer",
                color: unreadCount > 0 ? "var(--brand-400)" : "var(--text-secondary)",
                position: "relative",
              }}
              title="Notifications"
            >
              <Icon name="bell" size={18} />
              {unreadCount > 0 && (
                <span
                  style={{
                    position: "absolute",
                    top: -4,
                    right: -4,
                    background: "#ef4444",
                    color: "#fff",
                    borderRadius: 999,
                    fontSize: 10,
                    fontWeight: 700,
                    minWidth: 16,
                    height: 16,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    padding: "0 4px",
                  }}
                >
                  {unreadCount > 9 ? "9+" : unreadCount}
                </span>
              )}
            </button>

            {/* Notifications Popover */}
            {showNotifPopover && (
              <div
                className="animate-fade-in"
                style={{
                  position: "absolute",
                  top: 44,
                  right: 0,
                  width: 340,
                  maxHeight: 400,
                  background: "var(--bg-surface)",
                  border: "1px solid var(--border)",
                  borderRadius: 10,
                  boxShadow: "var(--shadow-lg)",
                  zIndex: 100,
                  display: "flex",
                  flexDirection: "column",
                  overflow: "hidden",
                }}
              >
                <div
                  style={{
                    padding: "10px 14px",
                    borderBottom: "1px solid var(--border)",
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                  }}
                >
                  <span style={{ fontSize: 13, fontWeight: 700, color: "var(--text-primary)" }}>
                    Notifications {unreadCount > 0 && `(${unreadCount})`}
                  </span>
                  {unreadCount > 0 && (
                    <button
                      onClick={handleMarkAllRead}
                      style={{
                        background: "none",
                        border: "none",
                        fontSize: 11,
                        color: "var(--brand-400)",
                        cursor: "pointer",
                        fontWeight: 600,
                      }}
                    >
                      Mark all as read
                    </button>
                  )}
                </div>

                <div style={{ overflowY: "auto", flex: 1, maxHeight: 340 }}>
                  {notifications.length === 0 ? (
                    <div style={{ padding: 24, textAlign: "center", color: "var(--text-muted)", fontSize: 12 }}>
                      No notifications yet.
                    </div>
                  ) : (
                    notifications.map((n) => (
                      <div
                        key={n.id}
                        onClick={() => handleMarkRead(n)}
                        style={{
                          padding: "10px 14px",
                          borderBottom: "1px solid var(--border-subtle)",
                          cursor: "pointer",
                          background: n.isRead ? "transparent" : "rgba(99, 102, 241, 0.08)",
                          transition: "background 0.15s",
                        }}
                        onMouseEnter={(e) => {
                          (e.currentTarget as HTMLElement).style.background = "var(--bg-hover)";
                        }}
                        onMouseLeave={(e) => {
                          (e.currentTarget as HTMLElement).style.background = n.isRead
                            ? "transparent"
                            : "rgba(99, 102, 241, 0.08)";
                        }}
                      >
                        <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 2 }}>
                          <span
                            style={{
                              fontSize: 12,
                              fontWeight: n.isRead ? 500 : 700,
                              color: "var(--text-primary)",
                            }}
                          >
                            {n.title}
                          </span>
                          <span style={{ fontSize: 10, color: "var(--text-muted)" }}>
                            {new Date(n.createdAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                          </span>
                        </div>
                        <div style={{ fontSize: 12, color: "var(--text-secondary)", lineHeight: 1.4 }}>
                          {n.message}
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </div>
            )}
          </div>
        </header>

        {/* Page Content */}
        <main
          style={{
            flex: 1,
            padding: "28px 32px",
            overflowY: "auto",
            minWidth: 0,
          }}
        >
          {children}
        </main>
      </div>

      {/* Change Password Modal */}
      {showPasswordModal && (
        <div
          className="modal-backdrop"
          onClick={(e) => e.target === e.currentTarget && setShowPasswordModal(false)}
        >
          <div className="modal animate-fade-in" style={{ maxWidth: 440 }}>
            <div className="flex-between" style={{ marginBottom: 16 }}>
              <h2 style={{ fontSize: 16, fontWeight: 700, color: "var(--text-primary)" }}>
                Change Password
              </h2>
              <button
                className="btn btn-ghost btn-sm"
                onClick={() => setShowPasswordModal(false)}
                aria-label="Close change password dialog"
              >
                <Icon name="x" />
              </button>
            </div>

            <form onSubmit={handleChangePassword}>
              <div className="form-group">
                <label className="label">Current Password</label>
                <input
                  type="password"
                  className="input"
                  required
                  value={pwdForm.currentPassword}
                  onChange={(e) => setPwdForm((f) => ({ ...f, currentPassword: e.target.value }))}
                />
              </div>

              <div className="form-group">
                <label className="label">New Password</label>
                <input
                  type="password"
                  className="input"
                  required
                  minLength={8}
                  maxLength={128}
                  pattern="(?=.*[A-Z])(?=.*[0-9]).{8,128}"
                  title="Use 8–128 characters, including at least one uppercase letter and one number."
                  value={pwdForm.newPassword}
                  onChange={(e) => setPwdForm((f) => ({ ...f, newPassword: e.target.value }))}
                />
                <small style={{ color: "var(--text-muted)" }}>
                  Use 8–128 characters, including at least one uppercase letter and one number.
                </small>
              </div>

              <div className="form-group">
                <label className="label">Confirm New Password</label>
                <input
                  type="password"
                  className="input"
                  required
                  minLength={8}
                  maxLength={128}
                  value={pwdForm.confirmPassword}
                  onChange={(e) => setPwdForm((f) => ({ ...f, confirmPassword: e.target.value }))}
                />
              </div>

              {pwdMsg && (
                <p
                  style={{
                    fontSize: 12,
                    marginBottom: 12,
                    color: pwdMsg.error ? "#f87171" : "#10b981",
                  }}
                >
                  {pwdMsg.text}
                </p>
              )}

              <div style={{ display: "flex", gap: 10, justifyContent: "flex-end", marginTop: 8 }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setShowPasswordModal(false)}
                >
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary" disabled={pwdLoading}>
                  {pwdLoading ? "Saving…" : "Save Password"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
