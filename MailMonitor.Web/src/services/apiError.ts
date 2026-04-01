export type ApiErrorCode = "BAD_REQUEST" | "NOT_FOUND" | "SERVER_ERROR" | "NETWORK" | "UNKNOWN";

export interface ApiError {
  status: number | null;
  code: ApiErrorCode;
  message: string;
  details?: Record<string, string[]>;
}

interface ValidationProblemDetails {
  errors?: Record<string, string[]>;
  title?: string;
  message?: string;
  detail?: string;
  errorMessage?: string;
}

function isValidationProblemDetails(value: unknown): value is ValidationProblemDetails {
  return typeof value === "object" && value !== null;
}

function getServerMessage(data: unknown): string | null {
  if (!isValidationProblemDetails(data)) {
    return null;
  }

  const candidates = [data.errorMessage, data.detail, data.message, data.title];
  const message = candidates.find((candidate) => typeof candidate === "string" && candidate.trim().length > 0);
  return message?.trim() ?? null;
}

function getFirstValidationMessage(details?: Record<string, string[]>): string | null {
  if (!details) {
    return null;
  }

  for (const values of Object.values(details)) {
    if (!values || values.length === 0) {
      continue;
    }

    const first = values.find((value) => typeof value === "string" && value.trim().length > 0);
    if (first) {
      return first.trim();
    }
  }

  return null;
}

export function toApiError(status: number | null, data?: unknown, fallbackMessage?: string): ApiError {
  if (status === null) {
    return {
      status,
      code: "NETWORK",
      message: fallbackMessage ?? "No se pudo conectar con la API. Revisa red y URL base."
    };
  }

  if (status === 400) {
    const details = isValidationProblemDetails(data) ? data.errors : undefined;
    const serverMessage = getServerMessage(data);
    const validationMessage = getFirstValidationMessage(details);
    return {
      status,
      code: "BAD_REQUEST",
      message: validationMessage ?? serverMessage ?? "La solicitud no paso validaciones.",
      details
    };
  }

  if (status === 404) {
    return {
      status,
      code: "NOT_FOUND",
      message: "El recurso solicitado no fue encontrado."
    };
  }

  if (status >= 500) {
    const serverMessage = getServerMessage(data);
    return {
      status,
      code: "SERVER_ERROR",
      message: serverMessage ?? "La API reporto un error interno. Intenta nuevamente."
    };
  }

  return {
    status,
    code: "UNKNOWN",
    message: fallbackMessage ?? "Ocurrio un error inesperado."
  };
}

export function getFirstApiErrorDetail(error: ApiError, key: string): string | null {
  if (!error.details) {
    return null;
  }

  const values = error.details[key];
  if (!values || values.length === 0) {
    return null;
  }

  return values[0];
}
