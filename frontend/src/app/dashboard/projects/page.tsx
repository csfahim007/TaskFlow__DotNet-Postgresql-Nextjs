"use client";

import { useEffect, useState, useCallback } from "react";
import { projectsApi, Project, CreateProjectRequest } from "@/services/projects.service";
import Link from "next/link";
import { Icon } from "@/components/icons";

/* ── Modal ── */
function CreateProjectModal({
  onClose,
  onCreated,
}: {
  onClose: () => void;
  onCreated: (p: Project) => void;
}) {
  const [form, setForm] = useState<CreateProjectRequest>({ name: "", description: "", key: "" });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const update = (field: keyof CreateProjectRequest) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>
  ) => setForm((f) => ({ ...f, [field]: e.target.value }));

  // Auto-generate key from name
  const handleNameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const name = e.target.value;
    const autoKey = name
      .toUpperCase()
      .replace(/[^A-Z0-9]/g, "")
      .slice(0, 6);
    setForm((f) => ({ ...f, name, key: f.key || autoKey }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.name.trim() || !form.key.trim()) { setError("Name and key are required."); return; }
    if (form.key.trim().length < 2) { setError("Project key must be at least 2 characters (e.g. TF, PROJ)."); return; }
    setError(null);
    setLoading(true);
    try {
      const p = await projectsApi.createProject({
        ...form,
        key: form.key.trim().toUpperCase(),
      });
      onCreated(p);
      onClose();
    } catch (err: unknown) {
      const resData = (err as { response?: { data?: { message?: string; error?: string; errors?: Record<string, string[]> } } })?.response?.data;
      let msg = "Failed to create project.";
      if (resData?.errors) {
        msg = Object.values(resData.errors).flat().join(". ");
      } else if (resData?.message) {
        msg = resData.message;
      } else if (resData?.error) {
        msg = resData.error;
      }
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="modal-backdrop" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal animate-fade-in">
        <div className="flex-between" style={{ marginBottom: 20 }}>
          <h2 style={{ fontSize: 17, fontWeight: 700, color: "var(--text-primary)" }}>
            New project
          </h2>
          <button className="btn btn-ghost btn-sm" onClick={onClose} id="btn-close-project-modal" aria-label="Close project dialog">
            <Icon name="x" />
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label className="label" htmlFor="proj-name">Project name</label>
            <input id="proj-name" type="text" className="input" placeholder="e.g. Marketing Campaign" value={form.name} onChange={handleNameChange} required />
          </div>

          <div className="form-group">
            <label className="label" htmlFor="proj-key">Project key</label>
            <input
              id="proj-key"
              type="text"
              className="input"
              placeholder="e.g. MKTG"
              value={form.key}
              onChange={update("key")}
              minLength={2}
              maxLength={10}
              style={{ fontFamily: "monospace", letterSpacing: "0.1em" }}
              required
            />
            <p style={{ fontSize: 11, color: "var(--text-muted)", marginTop: 4 }}>
              Used as task prefix (e.g. MKTG-001). 2 to 10 letters.
            </p>
          </div>

          <div className="form-group">
            <label className="label" htmlFor="proj-desc">Description</label>
            <textarea
              id="proj-desc"
              className="input"
              placeholder="What is this project about?"
              value={form.description}
              onChange={update("description")}
            />
          </div>

          {error && <p className="error-text" style={{ marginBottom: 12 }}>{error}</p>}

          <div style={{ display: "flex", gap: 10, justifyContent: "flex-end", marginTop: 4 }}>
            <button type="button" className="btn btn-secondary" onClick={onClose}>
              Cancel
            </button>
            <button id="btn-create-project-submit" type="submit" className="btn btn-primary" disabled={loading}>
              {loading ? <span className="spinner spinner-sm" /> : null}
              {loading ? "Creating…" : "Create project"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ── Main page ── */
export default function ProjectsPage() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [showModal, setShowModal] = useState(false);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const fetchProjects = useCallback(async () => {
    setLoading(true);
    try {
      const r = await projectsApi.getProjects(page, 12, search || undefined);
      setProjects(r.items);
      setTotalPages(r.totalPages);
    } finally {
      setLoading(false);
    }
  }, [page, search]);

  useEffect(() => { fetchProjects(); }, [fetchProjects]);

  // Debounce search
  useEffect(() => { setPage(1); }, [search]);

  const handleCreated = (p: Project) => {
    setProjects((prev) => [p, ...prev]);
  };

  return (
    <div className="animate-fade-in">
      {/* Header */}
      <div className="page-header">
        <div>
          <h1 className="page-title">Projects</h1>
          <p className="page-subtitle">{projects.length} project{projects.length !== 1 ? "s" : ""} in your workspace</p>
        </div>
        <button
          id="btn-new-project"
          className="btn btn-primary"
          onClick={() => setShowModal(true)}
        >
          <Icon name="plus" size={16} /> New project
        </button>
      </div>

      {/* Search */}
      <div style={{ marginBottom: 24, maxWidth: 360 }}>
        <input
          id="projects-search"
          type="search"
          className="input"
          placeholder="Search projects…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>

      {/* Grid */}
      {loading ? (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))", gap: 16 }}>
          {[1, 2, 3, 4, 5, 6].map((i) => (
            <div key={i} className="card">
              <div className="skeleton" style={{ height: 14, width: "50%", marginBottom: 12 }} />
              <div className="skeleton" style={{ height: 16, width: "80%", marginBottom: 8 }} />
              <div className="skeleton" style={{ height: 10, width: "100%", marginBottom: 4 }} />
              <div className="skeleton" style={{ height: 10, width: "70%" }} />
            </div>
          ))}
        </div>
      ) : projects.length === 0 ? (
        <div className="card">
          <div className="empty-state">
            <div className="empty-state-icon"><Icon name="folder" /></div>
            <div className="empty-state-title">
              {search ? "No projects match your search" : "No projects yet"}
            </div>
            <div className="empty-state-desc">
              {search
                ? "Try a different search term."
                : "Create your first project to start managing tasks."}
            </div>
            {!search && (
              <button
                className="btn btn-primary btn-sm"
                style={{ marginTop: 4 }}
                onClick={() => setShowModal(true)}
              >
                <Icon name="plus" size={14} /> New project
              </button>
            )}
          </div>
        </div>
      ) : (
        <>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))",
              gap: 16,
              marginBottom: 24,
            }}
          >
            {projects.map((p) => (
              <Link
                key={p.id}
                href={`/dashboard/projects/${p.id}`}
                style={{ textDecoration: "none" }}
              >
                <div
                  className="card"
                  style={{ cursor: "pointer", transition: "border-color 0.15s, transform 0.15s", height: "100%" }}
                  onMouseEnter={(e) => {
                    (e.currentTarget as HTMLDivElement).style.borderColor = "var(--brand-500)";
                    (e.currentTarget as HTMLDivElement).style.transform = "translateY(-2px)";
                  }}
                  onMouseLeave={(e) => {
                    (e.currentTarget as HTMLDivElement).style.borderColor = "var(--border)";
                    (e.currentTarget as HTMLDivElement).style.transform = "";
                  }}
                >
                  <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", marginBottom: 12 }}>
                    <span
                      style={{
                        background: "rgba(99,102,241,.15)",
                        color: "var(--brand-400)",
                        padding: "3px 8px",
                        borderRadius: 6,
                        fontSize: 11,
                        fontWeight: 700,
                        letterSpacing: "0.04em",
                        fontFamily: "monospace",
                      }}
                    >
                      {p.key}
                    </span>
                    <span
                      className={`badge ${p.status === "Active" ? "badge-done" : p.status === "OnHold" ? "badge-inprogress" : "badge-cancelled"}`}
                    >
                      {p.status}
                    </span>
                  </div>
                  <div style={{ fontSize: 15, fontWeight: 600, color: "var(--text-primary)", marginBottom: 8 }}>
                    {p.name}
                  </div>
                  <div
                    style={{
                      fontSize: 12,
                      color: "var(--text-muted)",
                      lineHeight: 1.6,
                      overflow: "hidden",
                      display: "-webkit-box",
                      WebkitLineClamp: 2,
                      WebkitBoxOrient: "vertical",
                      marginBottom: 14,
                    }}
                  >
                    {p.description || "No description provided."}
                  </div>
                  <div style={{ fontSize: 11, color: "var(--text-muted)" }}>
                    Owner: <span style={{ color: "var(--text-secondary)" }}>{p.ownerName}</span>
                    &nbsp;·&nbsp;
                    {new Date(p.createdAt).toLocaleDateString()}
                  </div>
                </div>
              </Link>
            ))}
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div style={{ display: "flex", gap: 8, justifyContent: "center" }}>
              <button
                className="btn btn-secondary btn-sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
              >
                <Icon name="chevronLeft" size={14} /> Prev
              </button>
              <span style={{ lineHeight: "30px", fontSize: 13, color: "var(--text-muted)" }}>
                {page} / {totalPages}
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
        </>
      )}

      {showModal && (
        <CreateProjectModal onClose={() => setShowModal(false)} onCreated={handleCreated} />
      )}
    </div>
  );
}
