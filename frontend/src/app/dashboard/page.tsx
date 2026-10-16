"use client";

import { useEffect, useState, useMemo } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { projectsApi, Project } from "@/services/projects.service";
import { tasksApi, Task } from "@/services/tasks.service";
import { useAuthStore } from "@/stores/auth-store";

export default function DashboardPage() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);

  const [projects, setProjects] = useState<Project[]>([]);
  const [allTasks, setAllTasks] = useState<Task[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function loadDashboardData() {
      setLoading(true);
      try {
        const projRes = await projectsApi.getProjects(1, 10);
        setProjects(projRes.items);

        // Fetch tasks for up to the first 4 projects to populate focus stream and analytics
        const taskPromises = projRes.items.slice(0, 4).map((p) =>
          tasksApi.getTasks(p.id, { pageSize: 50 }).catch(() => ({ items: [] }))
        );
        const taskResults = await Promise.all(taskPromises);
        const combinedTasks = taskResults.flatMap((r) => r.items);
        setAllTasks(combinedTasks);
      } catch (err) {
        console.error("Dashboard data load error:", err);
      } finally {
        setLoading(false);
      }
    }

    loadDashboardData();
  }, []);

  const greeting = () => {
    const h = new Date().getHours();
    if (h < 12) return "Good morning";
    if (h < 18) return "Good afternoon";
    return "Good evening";
  };

  // Metrics computation
  const metrics = useMemo(() => {
    const total = allTasks.length;
    if (total === 0) {
      return { total: 0, done: 0, inProgress: 0, todo: 0, overdue: 0, percentDone: 0, totalSP: 0, doneSP: 0 };
    }
    const done = allTasks.filter((t) => t.status === "Done").length;
    const inProgress = allTasks.filter((t) => t.status === "InProgress" || t.status === "InReview").length;
    const todo = allTasks.filter((t) => t.status === "Todo").length;
    const overdue = allTasks.filter(
      (t) => t.status !== "Done" && t.dueDate && new Date(t.dueDate) < new Date()
    ).length;
    const percentDone = Math.round((done / total) * 100);

    const totalSP = allTasks.reduce((acc, t) => acc + (t.storyPoints || 0), 0);
    const doneSP = allTasks
      .filter((t) => t.status === "Done")
      .reduce((acc, t) => acc + (t.storyPoints || 0), 0);

    return { total, done, inProgress, todo, overdue, percentDone, totalSP, doneSP };
  }, [allTasks]);

  // Focus queue: pending tasks sorted by due date
  const focusTasks = useMemo(() => {
    return allTasks
      .filter((t) => t.status !== "Done" && t.status !== "Cancelled")
      .sort((a, b) => {
        if (!a.dueDate) return 1;
        if (!b.dueDate) return -1;
        return new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime();
      })
      .slice(0, 5);
  }, [allTasks]);

  return (
    <div className="animate-fade-in" style={{ maxWidth: 1200, margin: "0 auto", paddingBottom: 40 }}>
      {/* ── Minimal Executive Header ── */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "flex-start",
          marginBottom: 28,
          flexWrap: "wrap",
          gap: 16,
        }}
      >
        <div>
          <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 6 }}>
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
                fontWeight: 700,
                fontSize: 13,
              }}
            >
              {user?.fullName?.charAt(0) || "U"}
            </div>
            <div>
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <span style={{ fontSize: 15, fontWeight: 600, color: "var(--text-primary)" }}>
                  {user?.fullName || "User"}
                </span>
                <span
                  style={{
                    fontSize: 11,
                    padding: "1px 7px",
                    borderRadius: 999,
                    background: "rgba(99, 102, 241, 0.15)",
                    color: "var(--brand-400)",
                    fontWeight: 600,
                  }}
                >
                  {user?.role || "Member"}
                </span>
                <span
                  style={{
                    width: 7,
                    height: 7,
                    borderRadius: "50%",
                    backgroundColor: "#10b981",
                    display: "inline-block",
                  }}
                  title="Live synchronization active"
                />
              </div>
            </div>
          </div>
          <h1 style={{ fontSize: 26, fontWeight: 700, letterSpacing: "-0.02em", margin: "2px 0 4px" }}>
            {greeting()}, {user?.fullName?.split(" ")[0]}
          </h1>
          <p style={{ color: "var(--text-muted)", fontSize: 13, margin: 0 }}>
            Here is your current team sprint velocity and actionable execution priorities.
          </p>
        </div>

        <div style={{ display: "flex", gap: 10, alignItems: "center" }}>
          <Link href="/dashboard/projects" className="btn btn-primary" style={{ padding: "8px 16px" }}>
            + New Project
          </Link>
        </div>
      </div>

      {/* ── Sprint Velocity & Progress Radar (Replaces box numbers) ── */}
      <div
        className="card"
        style={{
          background: "linear-gradient(180deg, var(--bg-surface) 0%, rgba(19, 21, 30, 0.95) 100%)",
          borderColor: "var(--border)",
          padding: "22px 24px",
          marginBottom: 28,
        }}
      >
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 16,
            flexWrap: "wrap",
            gap: 12,
          }}
        >
          <div>
            <div style={{ fontSize: 12, fontWeight: 600, color: "var(--brand-400)", textTransform: "uppercase", letterSpacing: "0.06em" }}>
              Execution Radar
            </div>
            <div style={{ fontSize: 18, fontWeight: 700, color: "var(--text-primary)", marginTop: 2 }}>
              Workspace Sprint Velocity
            </div>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: 18, fontSize: 13 }}>
            <div>
              <span style={{ color: "var(--text-muted)", marginRight: 6 }}>Story Points:</span>
              <strong style={{ color: "var(--text-primary)" }}>
                {metrics.doneSP} / {metrics.totalSP} SP
              </strong>
            </div>
            <div style={{ width: 1, height: 16, background: "var(--border)" }} />
            <div>
              <span style={{ color: "var(--text-muted)", marginRight: 6 }}>Progress:</span>
              <strong style={{ color: "#10b981" }}>{metrics.percentDone}% Complete</strong>
            </div>
            {metrics.overdue > 0 && (
              <>
                <div style={{ width: 1, height: 16, background: "var(--border)" }} />
                <div style={{ color: "#f87171", fontWeight: 600, display: "flex", alignItems: "center", gap: 4 }}>
                  <span>⚠</span> {metrics.overdue} Overdue
                </div>
              </>
            )}
          </div>
        </div>

        {/* Multi-Segmented Continuous Progress Bar */}
        <div
          style={{
            height: 10,
            borderRadius: 6,
            background: "var(--bg-elevated)",
            overflow: "hidden",
            display: "flex",
            marginBottom: 14,
            border: "1px solid rgba(255,255,255,0.05)",
          }}
        >
          <div
            style={{
              width: `${metrics.percentDone}%`,
              background: "linear-gradient(90deg, #059669, #10b981)",
              transition: "width 0.6s ease",
            }}
            title={`${metrics.done} Completed`}
          />
          <div
            style={{
              width: `${metrics.total ? (metrics.inProgress / metrics.total) * 100 : 0}%`,
              background: "linear-gradient(90deg, #d97706, #f59e0b)",
              transition: "width 0.6s ease",
            }}
            title={`${metrics.inProgress} In Progress`}
          />
          <div
            style={{
              width: `${metrics.total ? (metrics.todo / metrics.total) * 100 : 0}%`,
              background: "linear-gradient(90deg, #4338ca, #6366f1)",
              transition: "width 0.6s ease",
            }}
            title={`${metrics.todo} To Do`}
          />
        </div>

        {/* Legend / Metrics breakdown */}
        <div style={{ display: "flex", flexWrap: "wrap", gap: 20, fontSize: 12 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
            <span style={{ width: 8, height: 8, borderRadius: "50%", background: "#10b981" }} />
            <span style={{ color: "var(--text-secondary)" }}>Done:</span>
            <span style={{ fontWeight: 600, color: "var(--text-primary)" }}>{metrics.done}</span>
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
            <span style={{ width: 8, height: 8, borderRadius: "50%", background: "#f59e0b" }} />
            <span style={{ color: "var(--text-secondary)" }}>In Progress / Review:</span>
            <span style={{ fontWeight: 600, color: "var(--text-primary)" }}>{metrics.inProgress}</span>
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
            <span style={{ width: 8, height: 8, borderRadius: "50%", background: "#6366f1" }} />
            <span style={{ color: "var(--text-secondary)" }}>To Do:</span>
            <span style={{ fontWeight: 600, color: "var(--text-primary)" }}>{metrics.todo}</span>
          </div>
          <div style={{ marginLeft: "auto", color: "var(--text-muted)" }}>
            Tracking {metrics.total} tasks across {projects.length} projects
          </div>
        </div>
      </div>

      {/* ── Main Two-Column Layout: Priority Focus Queue & Project Portfolio ── */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 24, alignItems: "start" }}>
        {/* Left Column: Priority Focus Tasks */}
        <div className="card" style={{ padding: "20px 22px" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
            <div>
              <h2 style={{ fontSize: 16, fontWeight: 700, margin: 0, color: "var(--text-primary)" }}>
                Priority Action Queue
              </h2>
              <p style={{ margin: "2px 0 0", fontSize: 12, color: "var(--text-muted)" }}>
                Next scheduled deliverables
              </p>
            </div>
            <span style={{ fontSize: 11, background: "var(--bg-elevated)", padding: "2px 8px", borderRadius: 4, color: "var(--text-muted)" }}>
              {focusTasks.length} pending
            </span>
          </div>

          {loading ? (
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              {[1, 2, 3].map((i) => (
                <div key={i} className="skeleton" style={{ height: 48, borderRadius: 8 }} />
              ))}
            </div>
          ) : focusTasks.length === 0 ? (
            <div style={{ padding: "32px 16px", textAlign: "center", color: "var(--text-muted)" }}>
              <div style={{ fontSize: 24, marginBottom: 8 }}>✨</div>
              <div style={{ fontSize: 14, fontWeight: 600, color: "var(--text-secondary)" }}>All caught up!</div>
              <div style={{ fontSize: 12 }}>No urgent tasks currently pending.</div>
            </div>
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {focusTasks.map((t) => {
                const isOverdue = t.dueDate && new Date(t.dueDate) < new Date();
                return (
                  <div
                    key={t.id}
                    onClick={() => router.push(`/dashboard/projects/${t.projectId}`)}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      padding: "10px 14px",
                      background: "var(--bg-elevated)",
                      border: "1px solid var(--border)",
                      borderRadius: 8,
                      cursor: "pointer",
                      transition: "border-color 0.15s, transform 0.1s",
                    }}
                    onMouseEnter={(e) => {
                      (e.currentTarget as HTMLDivElement).style.borderColor = "var(--brand-400)";
                    }}
                    onMouseLeave={(e) => {
                      (e.currentTarget as HTMLDivElement).style.borderColor = "var(--border)";
                    }}
                  >
                    <div style={{ display: "flex", alignItems: "center", gap: 10, minWidth: 0 }}>
                      <span
                        style={{
                          fontSize: 11,
                          fontFamily: "monospace",
                          color: "var(--brand-400)",
                          fontWeight: 700,
                          flexShrink: 0,
                        }}
                      >
                        {t.taskNumber}
                      </span>
                      <span
                        style={{
                          fontSize: 13,
                          fontWeight: 500,
                          color: "var(--text-primary)",
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                          whiteSpace: "nowrap",
                        }}
                      >
                        {t.title}
                      </span>
                    </div>

                    <div style={{ display: "flex", alignItems: "center", gap: 10, flexShrink: 0 }}>
                      {t.dueDate && (
                        <span
                          style={{
                            fontSize: 11,
                            color: isOverdue ? "#f87171" : "var(--text-muted)",
                            fontWeight: isOverdue ? 600 : 400,
                          }}
                        >
                          {isOverdue ? "⚠ " : "📅 "}
                          {new Date(t.dueDate).toLocaleDateString(undefined, { month: "short", day: "numeric" })}
                        </span>
                      )}
                      <span
                        style={{
                          fontSize: 11,
                          padding: "2px 6px",
                          borderRadius: 4,
                          background:
                            t.priority === "Critical"
                              ? "rgba(239, 68, 68, 0.2)"
                              : t.priority === "High"
                              ? "rgba(249, 115, 22, 0.2)"
                              : "rgba(99, 102, 241, 0.2)",
                          color:
                            t.priority === "Critical"
                              ? "#f87171"
                              : t.priority === "High"
                              ? "#fb923c"
                              : "var(--brand-300)",
                          fontWeight: 600,
                        }}
                      >
                        {t.priority}
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Right Column: Active Projects Portfolio */}
        <div className="card" style={{ padding: "20px 22px" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
            <div>
              <h2 style={{ fontSize: 16, fontWeight: 700, margin: 0, color: "var(--text-primary)" }}>
                Active Projects
              </h2>
              <p style={{ margin: "2px 0 0", fontSize: 12, color: "var(--text-muted)" }}>
                Workspaces in progress
              </p>
            </div>
            <Link
              href="/dashboard/projects"
              style={{ fontSize: 12, color: "var(--brand-400)", textDecoration: "none", fontWeight: 600 }}
            >
              All Projects →
            </Link>
          </div>

          {loading ? (
            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              {[1, 2].map((i) => (
                <div key={i} className="skeleton" style={{ height: 64, borderRadius: 8 }} />
              ))}
            </div>
          ) : projects.length === 0 ? (
            <div style={{ padding: "32px 16px", textAlign: "center", color: "var(--text-muted)" }}>
              <div style={{ fontSize: 24, marginBottom: 8 }}>📁</div>
              <div style={{ fontSize: 14, fontWeight: 600, color: "var(--text-secondary)" }}>No projects yet</div>
              <Link href="/dashboard/projects" className="btn btn-primary btn-sm" style={{ marginTop: 10 }}>
                Create First Project
              </Link>
            </div>
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              {projects.map((p) => {
                const projTasks = allTasks.filter((t) => t.projectId === p.id);
                const projDone = projTasks.filter((t) => t.status === "Done").length;
                const projPercent = projTasks.length ? Math.round((projDone / projTasks.length) * 100) : 0;

                return (
                  <div
                    key={p.id}
                    style={{
                      padding: "12px 14px",
                      background: "var(--bg-elevated)",
                      border: "1px solid var(--border)",
                      borderRadius: 8,
                      display: "flex",
                      flexDirection: "column",
                      gap: 8,
                    }}
                  >
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                      <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                        <span
                          style={{
                            background: "rgba(99, 102, 241, 0.15)",
                            color: "var(--brand-400)",
                            padding: "2px 6px",
                            borderRadius: 4,
                            fontSize: 11,
                            fontWeight: 700,
                            fontFamily: "monospace",
                          }}
                        >
                          {p.key}
                        </span>
                        <Link
                          href={`/dashboard/projects/${p.id}`}
                          style={{
                            fontSize: 14,
                            fontWeight: 600,
                            color: "var(--text-primary)",
                            textDecoration: "none",
                          }}
                        >
                          {p.name}
                        </Link>
                      </div>

                      <div style={{ display: "flex", gap: 6 }}>
                        <Link
                          href={`/dashboard/projects/${p.id}`}
                          className="btn btn-secondary btn-sm"
                          style={{ padding: "3px 8px", fontSize: 11 }}
                        >
                          ☷ Board
                        </Link>
                      </div>
                    </div>

                    {/* Progress line */}
                    <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                      <div
                        style={{
                          flex: 1,
                          height: 4,
                          borderRadius: 2,
                          background: "var(--bg-hover)",
                          overflow: "hidden",
                        }}
                      >
                        <div
                          style={{
                            width: `${projPercent}%`,
                            height: "100%",
                            background: "var(--brand-500)",
                          }}
                        />
                      </div>
                      <span style={{ fontSize: 11, color: "var(--text-muted)", flexShrink: 0 }}>
                        {projDone}/{projTasks.length} tasks ({projPercent}%)
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
