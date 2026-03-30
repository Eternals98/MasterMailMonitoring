import { FormEvent, useCallback, useEffect, useRef, useState } from "react";
import { EmptyState } from "../components/EmptyState";
import { ErrorMessage } from "../components/ErrorMessage";
import { Loading } from "../components/Loading";
import { Toast } from "../components/Toast";
import { ApiError } from "../services/apiError";
import { settingsService } from "../services/settingsService";
import { Setting } from "../types/models";

function isLikelyPath(path: string): boolean {
  return /^(?:[a-zA-Z]:\\|\\\\[^\\]+\\[^\\]+|\/)/.test(path.trim());
}

export function SettingsPage(): JSX.Element {
  const [setting, setSetting] = useState<Setting | null>(null);
  const [baseStorageFolder, setBaseStorageFolder] = useState("");
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ type: "success" | "error"; message: string } | null>(null);
  const baseStorageRef = useRef<HTMLInputElement>(null);

  const loadSettings = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await settingsService.getSettings();
      setSetting(data);
      setBaseStorageFolder(data.baseStorageFolder ?? "");
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      setError(apiError.message ?? "No fue posible cargar los settings.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadSettings();
  }, [loadSettings]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setFieldError(null);

    const trimmedPath = baseStorageFolder.trim();

    if (!trimmedPath) {
      setFieldError("La ruta base es obligatoria.");
      baseStorageRef.current?.focus();
      return;
    }

    if (!isLikelyPath(trimmedPath)) {
      setFieldError("La ruta base debe ser una ruta absoluta local o UNC.");
      baseStorageRef.current?.focus();
      return;
    }

    try {
      setSubmitting(true);
      await settingsService.updateSettings({ baseStorageFolder: trimmedPath });
      setSetting((previous) => {
        if (!previous) {
          return previous;
        }

        return {
          ...previous,
          baseStorageFolder: trimmedPath
        };
      });

      setToast({ type: "success", message: "Settings guardados correctamente." });
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      setToast({ type: "error", message: apiError.message ?? "No fue posible guardar settings." });
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    return <Loading text="Cargando settings..." />;
  }

  if (error) {
    return <ErrorMessage message={error} onRetry={() => void loadSettings()} />;
  }

  if (!setting) {
    return <EmptyState title="No hay settings configurados" description="Configura la API y vuelve a intentar." />;
  }

  return (
    <>
      <div className="grid two-columns">
        <article className="card">
          <h3>Lectura actual</h3>
          <dl className="definition-list">
            <dt>Base Storage Folder</dt>
            <dd>{setting.baseStorageFolder || "Sin valor"}</dd>
            <dt>Processing Tag</dt>
            <dd>{setting.processingTag || "Sin valor"}</dd>
            <dt>Mail Subject Keywords</dt>
            <dd>{setting.mailSubjectKeywords.length > 0 ? setting.mailSubjectKeywords.join(", ") : "Sin keywords"}</dd>
          </dl>
        </article>

        <article className="card">
          <h3>Editar</h3>
          <form onSubmit={(event) => void handleSubmit(event)} noValidate>
            <label htmlFor="baseStorageFolder">Base Storage Folder</label>
            <input
              id="baseStorageFolder"
              name="baseStorageFolder"
              ref={baseStorageRef}
              value={baseStorageFolder}
              onChange={(event) => setBaseStorageFolder(event.target.value)}
              placeholder="C:\\Mail\\Storage"
              disabled={submitting}
            />
            {fieldError ? <p className="field-error">{fieldError}</p> : null}

            <button type="submit" className="btn primary" disabled={submitting}>
              {submitting ? "Guardando..." : "Guardar settings"}
            </button>
          </form>
        </article>
      </div>

      {toast ? <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} /> : null}
    </>
  );
}