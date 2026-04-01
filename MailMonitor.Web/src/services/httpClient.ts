import { ApiError, toApiError } from "./apiError";

const DEFAULT_TIMEOUT_MS = 10000;
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL?.trim();

if (!API_BASE_URL) {
  // Solo se evalua en runtime para avisar una configuracion incompleta.
  // eslint-disable-next-line no-console
  console.warn("VITE_API_BASE_URL no esta definida. Configura .env para consumir la API.");
}

export interface RequestOptions extends Omit<RequestInit, "body"> {
  timeoutMs?: number;
  query?: Record<string, string | number | boolean | undefined | null>;
  body?: unknown;
}

export interface BlobResponse {
  blob: Blob;
  headers: Headers;
}

function isAbortLikeError(error: unknown): boolean {
  if (typeof error !== "object" || error === null) {
    return false;
  }

  const candidate = error as { name?: unknown; code?: unknown };
  return candidate.name === "AbortError" || candidate.code === 20;
}

function buildUrl(path: string, query?: RequestOptions["query"]): string {
  const normalizedBase = API_BASE_URL ? API_BASE_URL.replace(/\/$/, "") : "";
  const normalizedPath = path.startsWith("/") ? path : `/${path}`;
  const url = new URL(`${normalizedBase}${normalizedPath}`, window.location.origin);

  if (query) {
    Object.entries(query).forEach(([key, value]) => {
      if (value === undefined || value === null || value === "") {
        return;
      }

      url.searchParams.set(key, String(value));
    });
  }

  return url.toString();
}

async function parseResponseBody(response: Response): Promise<unknown> {
  const contentType = response.headers.get("content-type") ?? "";

  if (contentType.includes("application/json")) {
    return response.json();
  }

  if (contentType.includes("application/octet-stream") || contentType.includes("application/vnd.openxmlformats")) {
    return response.blob();
  }

  const text = await response.text();
  return text.length > 0 ? text : undefined;
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const controller = new AbortController();
  const timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;
  const timeoutHandle = window.setTimeout(() => controller.abort(), timeoutMs);

  try {
    const response = await fetch(buildUrl(path, options.query), {
      ...options,
      headers: {
        "Content-Type": "application/json",
        ...options.headers
      },
      body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
      signal: controller.signal
    });

    const parsedBody = await parseResponseBody(response);

    if (!response.ok) {
      throw toApiError(response.status, parsedBody);
    }

    return parsedBody as T;
  } catch (error) {
    if (isApiError(error)) {
      throw error;
    }

    if (isAbortLikeError(error)) {
      throw toApiError(null, undefined, "La solicitud superÃ³ el tiempo de espera configurado.");
    }

    throw toApiError(null, undefined, "No se pudo completar la solicitud por error de red.");
  } finally {
    window.clearTimeout(timeoutHandle);
  }
}

export async function requestBlob(path: string, options: RequestOptions = {}): Promise<BlobResponse> {
  const controller = new AbortController();
  const timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;
  const timeoutHandle = window.setTimeout(() => controller.abort(), timeoutMs);

  try {
    const response = await fetch(buildUrl(path, options.query), {
      ...options,
      headers: {
        "Content-Type": "application/json",
        ...options.headers
      },
      body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
      signal: controller.signal
    });

    if (!response.ok) {
      const parsedBody = await parseResponseBody(response);
      throw toApiError(response.status, parsedBody);
    }

    const blob = await response.blob();
    return {
      blob,
      headers: response.headers
    };
  } catch (error) {
    if (isApiError(error)) {
      throw error;
    }

    if (isAbortLikeError(error)) {
      throw toApiError(null, undefined, "La solicitud superÃ³ el tiempo de espera configurado.");
    }

    throw toApiError(null, undefined, "No se pudo completar la solicitud por error de red.");
  } finally {
    window.clearTimeout(timeoutHandle);
  }
}

function isApiError(value: unknown): value is ApiError {
  return typeof value === "object" && value !== null && "code" in value && "message" in value;
}

