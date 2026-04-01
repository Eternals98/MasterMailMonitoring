import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { EmptyState } from "../components/EmptyState";
import { ErrorMessage } from "../components/ErrorMessage";
import { Loading } from "../components/Loading";
import { Toast } from "../components/Toast";
import { ApiError } from "../services/apiError";
import { graphSettingsService } from "../services/graphSettingsService";
import { GraphConnectivityHealth, GraphSetting } from "../types/models";

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
      return "Cada scope debe ser un string no vacio.";
    }

    return null;
  } catch {
    return "Scopes debe ser JSON valido.";
  }
}

function toSafeApiError(error: unknown, fallbackMessage: string): ApiError {
  if (
    typeof error === "object" &&
    error !== null &&
    "code" in error &&
    "message" in error
  ) {
    const candidate = error as ApiError;
    if (typeof candidate.message === "string" && candidate.message.trim().length > 0) {
      return candidate;
    }
  }

  return {
    status: null,
    code: "NETWORK",
    message: fallbackMessage
  };
}

function formatLastVerification(setting: GraphSetting): string {
  if (!setting.lastVerificationAtUtc) {
    return "Sin verificacion registrada";
  }

  const parsed = new Date(setting.lastVerificationAtUtc);
  if (Number.isNaN(parsed.getTime())) {
    return setting.lastVerificationAtUtc;
  }

  return parsed.toLocaleString();
}

function getVerificationDetail(setting: GraphSetting): string {
  if (setting.lastVerificationSucceeded === true) {
    return "Ultima verificacion exitosa.";
  }

  if (setting.lastVerificationSucceeded === false) {
    return setting.lastVerificationErrorMessage || "Ultima verificacion fallida.";
  }

  return "Aun no se ha ejecutado una verificacion de Microsoft Graph.";
}

function compact(value: string): string {
  const trimmed = value.trim();
  if (trimmed.length <= 24) {
    return trimmed;
  }

  return `${trimmed.slice(0, 12)}...${trimmed.slice(-8)}`;
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
  const [showEditForm, setShowEditForm] = useState(false);

  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [checkingConnection, setCheckingConnection] = useState(false);
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
      setFieldErrors({
        instance: undefined,
        clientId: undefined,
        tenantId: undefined,
        clientSecret: undefined,
        graphUserScopesJson: undefined
      });
    } catch (unknownError) {
      const apiError = toSafeApiError(unknownError, "No se pudo cargar Graph Settings.");
      setError(apiError.message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadGraphSettings();
  }, [loadGraphSettings]);

  useEffect(() => {
    if (!firstErrorKey || !showEditForm) {
      return;
    }

    refs[firstErrorKey].current?.focus();
  }, [firstErrorKey, showEditForm, refs]);

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
      newErrors.clientSecret = "Ingresa Client Secret para guardar cambios.";
    }

    const scopesError = validateScopesJson(formState.graphUserScopesJson);
    if (scopesError) {
      newErrors.graphUserScopesJson = scopesError;
    }

    return newErrors;
  }

  function openEditForm(): void {
    if (!graphSetting) {
      return;
    }

    setFormState({
      instance: graphSetting.instance,
      clientId: graphSetting.clientId,
      tenantId: graphSetting.tenantId,
      clientSecret: "",
      graphUserScopesJson: graphSetting.graphUserScopesJson
    });
    setShowEditForm(true);
  }

  function cancelEditForm(): void {
    setShowEditForm(false);
    setFieldErrors({
      instance: undefined,
      clientId: undefined,
      tenantId: undefined,
      clientSecret: undefined,
      graphUserScopesJson: undefined
    });
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
      setShowEditForm(false);
      await loadGraphSettings();
    } catch (unknownError) {
      const apiError = toSafeApiError(unknownError, "No fue posible actualizar Graph Settings.");
      setToast({ type: "error", message: apiError.message });
    } finally {
      setSubmitting(false);
    }
  }

  async function handleTestConnection(): Promise<void> {
    try {
      setCheckingConnection(true);
      const result: GraphConnectivityHealth = await graphSettingsService.testConnection();
      setToast({
        type: result.healthy ? "success" : "error",
        message: result.healthy
          ? "Conexion con Microsoft Graph validada."
          : result.errorMessage || "No se pudo validar la conexion."
      });

      await loadGraphSettings();
    } catch (unknownError) {
      const apiError = toSafeApiError(unknownError, "No se pudo validar la conexion con Microsoft Graph.");
      setToast({ type: "error", message: apiError.message });
      await loadGraphSettings();
    } finally {
      setCheckingConnection(false);
    }
  }

  if (loading) {
    return <Loading text="Cargando configuracion de Graph..." />;
  }

  if (error) {
    return <ErrorMessage message={error} onRetry={() => void loadGraphSettings()} />;
  }

  if (!graphSetting) {
    return <EmptyState title="Sin configuracion de Graph" description="Configura credenciales y vuelve a intentar." />;
  }

  const verificationStateLabel =
    graphSetting.lastVerificationSucceeded === true
      ? "Verificada"
      : graphSetting.lastVerificationSucceeded === false
        ? "Con errores"
        : "Sin verificar";

  const hasInstance = graphSetting.instance.trim().length > 0;
  const hasClientId = graphSetting.clientId.trim().length > 0;
  const hasTenantId = graphSetting.tenantId.trim().length > 0;
  const hasSecret = graphSetting.clientSecretMasked.trim().length > 0;
  const hasValidScopes = validateScopesJson(graphSetting.graphUserScopesJson) === null;

  const missingRequirements: string[] = [];
  if (!hasInstance) {
    missingRequirements.push("Instance");
  }
  if (!hasClientId) {
    missingRequirements.push("ClientId");
  }
  if (!hasTenantId) {
    missingRequirements.push("TenantId");
  }
  if (!hasSecret) {
    missingRequirements.push("Client Secret");
  }
  if (!hasValidScopes) {
    missingRequirements.push("Scopes");
  }

  const canRunConnectionTest = missingRequirements.length === 0;

  return (
    <>
      <article className="card">
        <h3>Microsoft Graph</h3>
        <p className="hint">En esta pantalla puedes validar la conexion y actualizar credenciales cuando sea necesario.</p>

        <div className="graph-single-status">
          <div className={`health-chip ${graphSetting.lastVerificationSucceeded ? "healthy" : "unhealthy"}`}>
            {verificationStateLabel}
          </div>
          <p>
            <strong>Ultima prueba:</strong> {formatLastVerification(graphSetting)}
          </p>
          <p>
            <strong>Detalle:</strong> {getVerificationDetail(graphSetting)}
          </p>
          {graphSetting.lastVerificationErrorCode ? (
            <p>
              <strong>Codigo:</strong> {graphSetting.lastVerificationErrorCode}
            </p>
          ) : null}
        </div>

        <dl className="definition-list graph-current-values">
          <dt>Instance</dt>
          <dd>{graphSetting.instance}</dd>
          <dt>ClientId</dt>
          <dd className="mono-cell" title={graphSetting.clientId}>{compact(graphSetting.clientId)}</dd>
          <dt>TenantId</dt>
          <dd className="mono-cell" title={graphSetting.tenantId}>{compact(graphSetting.tenantId)}</dd>
          <dt>Client Secret</dt>
          <dd className="mono-cell">{graphSetting.clientSecretMasked || "Sin secreto"}</dd>
        </dl>

        <div className="row gap graph-actions">
          <button
            type="button"
            className="btn secondary"
            onClick={() => void handleTestConnection()}
            disabled={checkingConnection || submitting || !canRunConnectionTest}
          >
            {checkingConnection ? "Probando conexion..." : "Probar conexion Graph"}
          </button>

          {!showEditForm ? (
            <button
              type="button"
              className="btn primary"
              onClick={openEditForm}
              disabled={submitting || checkingConnection}
            >
              Actualizar Graph Settings
            </button>
          ) : (
            <button
              type="button"
              className="btn secondary"
              onClick={cancelEditForm}
              disabled={submitting}
            >
              Cancelar actualizacion
            </button>
          )}
        </div>

        {!canRunConnectionTest ? (
          <small className="hint">
            Para habilitar la prueba completa y guarda: {missingRequirements.join(", ")}.
          </small>
        ) : null}

        {showEditForm ? (
          <form className="graph-settings-form" onSubmit={(event) => void handleSubmit(event)} noValidate>
            <label htmlFor="instance">
              Instance
              <input
                id="instance"
                ref={refs.instance}
                value={formState.instance}
                onChange={(event) => onFieldChange("instance", event.target.value)}
                disabled={submitting}
              />
            </label>
            {fieldErrors.instance ? <p className="field-error">{fieldErrors.instance}</p> : null}

            <label htmlFor="clientId">
              ClientId
              <input
                id="clientId"
                ref={refs.clientId}
                value={formState.clientId}
                onChange={(event) => onFieldChange("clientId", event.target.value)}
                disabled={submitting}
              />
            </label>
            {fieldErrors.clientId ? <p className="field-error">{fieldErrors.clientId}</p> : null}

            <label htmlFor="tenantId">
              TenantId
              <input
                id="tenantId"
                ref={refs.tenantId}
                value={formState.tenantId}
                onChange={(event) => onFieldChange("tenantId", event.target.value)}
                disabled={submitting}
              />
            </label>
            {fieldErrors.tenantId ? <p className="field-error">{fieldErrors.tenantId}</p> : null}

            <label htmlFor="clientSecret">
              Client Secret
              <input
                id="clientSecret"
                ref={refs.clientSecret}
                type="password"
                value={formState.clientSecret}
                onChange={(event) => onFieldChange("clientSecret", event.target.value)}
                placeholder="Ingresa secreto para aplicar cambios"
                disabled={submitting}
              />
            </label>
            {fieldErrors.clientSecret ? <p className="field-error">{fieldErrors.clientSecret}</p> : null}

            <label htmlFor="scopes">
              GraphUserScopesJson
              <textarea
                id="scopes"
                ref={refs.graphUserScopesJson}
                value={formState.graphUserScopesJson}
                onChange={(event) => onFieldChange("graphUserScopesJson", event.target.value)}
                rows={5}
                disabled={submitting}
              />
            </label>
            <small className="hint">Ejemplo: ["https://graph.microsoft.com/.default"]</small>
            {fieldErrors.graphUserScopesJson ? <p className="field-error">{fieldErrors.graphUserScopesJson}</p> : null}

            <button type="submit" className="btn primary" disabled={submitting}>
              {submitting ? "Guardando..." : "Guardar cambios de Graph"}
            </button>
          </form>
        ) : null}
      </article>

      {toast ? <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} /> : null}
    </>
  );
}
