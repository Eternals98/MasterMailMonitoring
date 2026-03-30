import { FormEvent, useState } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";

interface NavigationState {
  from?: {
    pathname?: string;
  };
}

export function LoginPage(): JSX.Element {
  const { isAuthenticated, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const state = location.state as NavigationState | null;

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);

  if (isAuthenticated) {
    return <Navigate to="/" replace />;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setError(null);

    const result = login({ username, password });
    if (!result.ok) {
      setError(result.message ?? "No fue posible iniciar sesion.");
      return;
    }

    const redirectTo = state?.from?.pathname || "/";
    navigate(redirectTo, { replace: true });
  }

  return (
    <main className="auth-shell">
      <section className="auth-panel">
        <p className="auth-eyebrow">MailMonitor</p>
        <h1 className="auth-title">Iniciar sesion</h1>
        <p className="auth-subtitle">
          Accede al panel de monitoreo para gestionar companias, configuracion y reportes.
        </p>

        <form className="auth-form" onSubmit={(event) => void handleSubmit(event)} noValidate>
          <label htmlFor="username">Usuario</label>
          <input
            id="username"
            value={username}
            onChange={(event) => setUsername(event.target.value)}
            autoComplete="username"
            placeholder="admin"
          />

          <label htmlFor="password">Clave</label>
          <input
            id="password"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            autoComplete="current-password"
            placeholder="********"
          />

          {error ? <p className="auth-error">{error}</p> : null}

          <button className="btn primary auth-submit" type="submit">
            Entrar
          </button>
        </form>
      </section>
    </main>
  );
}
