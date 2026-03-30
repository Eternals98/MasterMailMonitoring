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
}

function isValidationProblemDetails(value: unknown): value is ValidationProblemDetails {
  return typeof value === "object" && value !== null;
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
    return {
      status,
      code: "BAD_REQUEST",
      message: "La solicitud no pasó validaciones.",
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
    return {
      status,
      code: "SERVER_ERROR",
      message: "La API reportó un error interno. Intenta nuevamente."
    };
  }

  return {
    status,
    code: "UNKNOWN",
    message: fallbackMessage ?? "Ocurrió un error inesperado."
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