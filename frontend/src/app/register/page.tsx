"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { authApi } from "@/services/auth.service";
import { useAuthStore } from "@/stores/auth-store";
import { BrandLogo } from "@/components/icons";

export default function RegisterPage() {
  const router = useRouter();
  const setAuth = useAuthStore((s) => s.setAuth);

  const [form, setForm] = useState({
    firstName: "",
    lastName: "",
    email: "",
    password: "",
    confirmPassword: "",
  });
  const [loading, setLoading] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});

  const update = (field: string) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f, [field]: e.target.value }));

  const validate = () => {
    const errs: Record<string, string> = {};
    if (!form.firstName.trim()) errs.firstName = "Required";
    if (!form.lastName.trim()) errs.lastName = "Required";
    if (!form.email) errs.email = "Required";
    if (form.password.length < 8) errs.password = "At least 8 characters";
    if (!/[A-Z]/.test(form.password)) errs.password = "Must contain an uppercase letter";
    if (!/[0-9]/.test(form.password)) errs.password = "Must contain a digit";
    if (form.password !== form.confirmPassword) errs.confirmPassword = "Passwords do not match";
    return errs;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const errs = validate();
    if (Object.keys(errs).length) { setErrors(errs); return; }
    setErrors({});
    setLoading(true);
    try {
      const res = await authApi.register(form);
      setAuth(
        { userId: res.userId, email: res.email, fullName: res.fullName, role: res.role },
        res.token,
        res.refreshToken
      );
      router.push("/dashboard");
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ||
        "Registration failed. Please try again.";
      setErrors({ global: msg });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-page">
      <div className="auth-card">
        <BrandLogo className="auth-logo" />

        <h1
          style={{
            fontSize: 22,
            fontWeight: 700,
            color: "var(--text-primary)",
            marginBottom: 6,
          }}
        >
          Create your account
        </h1>
        <p style={{ color: "var(--text-muted)", fontSize: 13, marginBottom: 28 }}>
          Get started with TaskFlow for free
        </p>

        <form onSubmit={handleSubmit}>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px", marginBottom: 16 }}>
            <div>
              <label className="label" htmlFor="reg-firstname">First name</label>
              <input id="reg-firstname" type="text" className="input" placeholder="Jane" value={form.firstName} onChange={update("firstName")} required />
              {errors.firstName && <p className="error-text">{errors.firstName}</p>}
            </div>
            <div>
              <label className="label" htmlFor="reg-lastname">Last name</label>
              <input id="reg-lastname" type="text" className="input" placeholder="Smith" value={form.lastName} onChange={update("lastName")} required />
              {errors.lastName && <p className="error-text">{errors.lastName}</p>}
            </div>
          </div>

          <div className="form-group">
            <label className="label" htmlFor="reg-email">Email address</label>
            <input id="reg-email" type="email" className="input" placeholder="you@company.com" value={form.email} onChange={update("email")} required autoComplete="email" />
            {errors.email && <p className="error-text">{errors.email}</p>}
          </div>

          <div className="form-group">
            <label className="label" htmlFor="reg-password">Password</label>
            <input id="reg-password" type="password" className="input" placeholder="Min. 8 chars, 1 uppercase, 1 digit" value={form.password} onChange={update("password")} required />
            {errors.password && <p className="error-text">{errors.password}</p>}
          </div>

          <div className="form-group">
            <label className="label" htmlFor="reg-confirm">Confirm password</label>
            <input id="reg-confirm" type="password" className="input" placeholder="••••••••" value={form.confirmPassword} onChange={update("confirmPassword")} required />
            {errors.confirmPassword && <p className="error-text">{errors.confirmPassword}</p>}
          </div>

          {errors.global && (
            <p className="error-text" style={{ marginBottom: 12 }}>{errors.global}</p>
          )}

          <button
            id="register-submit"
            type="submit"
            className="btn btn-primary btn-lg"
            disabled={loading}
            style={{ width: "100%", marginTop: 4 }}
          >
            {loading ? <span className="spinner spinner-sm" /> : null}
            {loading ? "Creating account…" : "Create account"}
          </button>
        </form>

        <hr className="divider" />

        <p style={{ textAlign: "center", fontSize: 13, color: "var(--text-muted)" }}>
          Already have an account?{" "}
          <Link
            href="/login"
            style={{ color: "var(--brand-400)", fontWeight: 500, textDecoration: "none" }}
          >
            Sign in
          </Link>
        </p>
      </div>
    </div>
  );
}
