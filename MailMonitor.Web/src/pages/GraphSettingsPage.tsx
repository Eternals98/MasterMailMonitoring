import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { EmptyState } from "../components/EmptyState";
import { ErrorMessage } from "../components/ErrorMessage";
import { Loading } from "../components/Loading";
import { Toast } from "../components/Toast";
import { ApiError } from "../services/apiError";
import { graphSettingsService } from "../services/graphSettingsService";
import { GraphSetting } from "../types/models";

interface GraphFormState {
  instance: string;
  clientId: string;
  tenantId: string;
  clientSecret: string;
  graphUserScopesJson: string;
}

function validateScopesJson(scopesJson: string): string | null {
  try {
    const parsed = JSON.parse(scopesJson);
    if (!Array.isArray(parsed)) {
      return "Scopes debe ser un arreglo JSON de strings.";
    }

    if (parsed.some((item) => typeof item !== "string" || item.trim().length === 0)) {
      return "Cada scope debe ser un string no vacío.";
    }

    return null;
  } catch {
    return "Scopes debe ser JSON válido.";
  }
}

export function GraphSettingsPage(): JSX.Element {
  const [graphSetting, setGraphSetting] = useState<GraphSetting | null>(null);
  const [formState, setFormState] = useState<GraphFormState>({
    instance: "",
    clientId: "",
    tenantId: "",
    clientSecret: "",
    graphUserScopesJson: "[]"
  });

  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<keyof GraphFormState, string | undefined>>({
    instance: undefined,
    clientId: undefined,
    tenantId: undefined,
    clientSecret: undefined,
    graphUserScopesJson: undefined
  });
  const [toast, setToast] = useState<{ type: "success" | "error"; message: string } | null>(null);

  const refs = {
    instance: useRef<HTMLInputElement>(null),
    clientId: useRef<HTMLInputElement>(null),
    tenantId: useRef<HTMLInputElement>(null),
    clientSecret: useRef<HTMLInputElement>(null),
    graphUserScopesJson: useRef<HTMLTextAreaElement>(null)
  };

  const firstErrorKey = useMemo(() => {
    const orderedKeys: (keyof GraphFormState)[] = [
      "instance",
      "clientId",
      "tenantId",
      "clientSecret",
      "graphUserScopesJson"
    ];

    return orderedKeys.find((key) => fieldErrors[key]);
  }, [fieldErrors]);

  const loadGraphSettings = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await graphSettingsService.get();
      setGraphSetting(data);
      setFormState({
        instance: data.instance,
        clientId: data.clientId,
        tenantId: data.tenantId,
        clientSecret: "",
        graphUserScopesJson: data.graphUserScopesJson
      });
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      setError(apiError.message ?? "No se pudo cargar Graph Settings.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadGraphSettings();
  }, [loadGraphSettings]);

  useEffect(() => {
    if (!firstErrorKey) {
      return;
    }

    refs[firstErrorKey].current?.focus();
  }, [firstErrorKey, refs]);

  function onFieldChange<Key extends keyof GraphFormState>(key: Key, value: GraphFormState[Key]): void {
    setFormState((previous) => ({
      ...previous,
      [key]: value
    }));
  }

  function validate(): Record<keyof GraphFormState, string | undefined> {
    const newErrors: Record<keyof GraphFormState, string | undefined> = {
      instance: undefined,
      clientId: undefined,
      tenantId: undefined,
      clientSecret: undefined,
      graphUserScopesJson: undefined
    };

    if (!formState.instance.trim()) {
      newErrors.instance = "Instance es obligatorio.";
    }

    if (!formState.clientId.trim()) {
      newErrors.clientId = "ClientId es obligatorio.";
    }

    if (!formState.tenantId.trim()) {
      newErrors.tenantId = "TenantId es obligatorio.";
    }

    if (!formState.clientSecret.trim()) {
      newErrors.clientSecret = "Debes ingresar el client secret actual o uno nuevo para guardar.";
    }

    const scopesError = validateScopesJson(formState.graphUserScopesJson);
    if (scopesError) {
      newErrors.graphUserScopesJson = scopesError;
    }

    return newErrors;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();

    const newErrors = validate();
    setFieldErrors(newErrors);

    if (Object.values(newErrors).some(Boolean)) {
      return;
    }

    try {
      setSubmitting(true);
      await graphSettingsService.update({
        instance: formState.instance.trim(),
        clientId: formState.clientId.trim(),
        tenantId: formState.tenantId.trim(),
        clientSecret: formState.clientSecret.trim(),
        graphUserScopesJson: formState.graphUserScopesJson.trim()
      });

      setToast({ type: "success", message: "Graph Settings actualizados." });
      setFormState((previous) => ({ ...previous, clientSecret: "" }));
      await loadGraphSettings();
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      setToast({ type: "error", message: apiError.message ?? "No fue posible actualizar Graph Settings." });
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    return <Loading text="Cargando configuración de Graph..." />;
  }

  if (error) {
    return <ErrorMessage message={error} onRetry={() => void loadGraphSettings()} />;
  }

  if (!graphSetting) {
    return <EmptyState title="No hay configuración de Graph" description="Configúrala desde API e intenta nuevamente." />;
  }

  return (
    <>
      <div className="grid two-columns">
        <article className="card">
          <h3>Estado actual</h3>
          <dl className="definition-list">
            <dt>Instance</dt>
            <dd>{graphSetting.instance}</dd>
            <dt>ClientId</dt>
            <dd>{graphSetting.clientId}</dd>
            <dt>TenantId</dt>
            <dd>{graphSetting.tenantId}</dd>
            <dt>Client Secret (enmascarado)</dt>
            <dd>{graphSetting.clientSecretMasked || "Sin secreto"}</dd>
          </dl>
        </article>

        <article className="card">
          <h3>Actualizar seguro</h3>
          <form onSubmit={(event) => void handleSubmit(event)} noValidate>
            <label htmlFor="instance">Instance</label>
            <input
              id="instance"
              ref={refs.instance}
              value={formState.instance}
              onChange={(event) => onFieldChange("instance", event.target.value)}
              disabled={submitting}
            />
            {fieldErrors.instance ? <p className="field-error">{fieldErrors.instance}</p> : null}

            <label htmlFor="clientId">ClientId</label>
            <input
              id="clientId"
              ref={refs.clientId}
              value={formState.clientId}
              onChange={(event) => onFieldChange("clientId", event.target.value)}
              disabled={submitting}
            />
            {fieldErrors.clientId ? <p className="field-error">{fieldErrors.clientId}</p> : null}

            <label htmlFor="tenantId">TenantId</label>
            <input
              id="tenantId"
              ref={refs.tenantId}
              value={formState.tenantId}
              onChange={(event) => onFieldChange("tenantId", event.target.value)}
              disabled={submitting}
            />
            {fieldErrors.tenantId ? <p className="field-error">{fieldErrors.tenantId}</p> : null}

            <label htmlFor="clientSecret">Client Secret</label>
            <input
              id="clientSecret"
              ref={refs.clientSecret}
              type="password"
              value={formState.clientSecret}
              onChange={(event) => onFieldChange("clientSecret", event.target.value)}
              placeholder="Ingresa secreto para aplicar cambios"
              disabled={submitting}
            />
            {fieldErrors.clientSecret ? <p className="field-error">{fieldErrors.clientSecret}</p> : null}

            <label htmlFor="scopes">GraphUserScopesJson</label>
            <textarea
              id="scopes"
              ref={refs.graphUserScopesJson}
              value={formState.graphUserScopesJson}
              onChange={(event) => onFieldChange("graphUserScopesJson", event.target.value)}
              rows={5}
              disabled={submitting}
            />
            <small className="hint">Ejemplo: ["Mail.Read","Mail.ReadWrite"]</small>
            {fieldErrors.graphUserScopesJson ? <p className="field-error">{fieldErrors.graphUserScopesJson}</p> : null}

            <button type="submit" className="btn primary" disabled={submitting}>
              {submitting ? "Actualizando..." : "Actualizar Graph Settings"}
            </button>
          </form>
        </article>
      </div>

      {toast ? <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} /> : null}
    </>
  );
}