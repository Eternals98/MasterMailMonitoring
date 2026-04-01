import { FormEvent, KeyboardEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { EmptyState } from "../components/EmptyState";
import { ErrorMessage } from "../components/ErrorMessage";
import { Loading } from "../components/Loading";
import { TablePagination, clampPage, paginateItems } from "../components/TablePagination";
import { Toast } from "../components/Toast";
import { ApiError, getFirstApiErrorDetail } from "../services/apiError";
import { companiesService } from "../services/companiesService";
import { settingsService } from "../services/settingsService";
import {
  CompanyFilters,
  CompanyListItem,
  CompanyUpsertRequest,
  MailboxLookupItem,
  MailboxRecentMessagesResult,
  StorageAccessCheckResult
} from "../types/models";

interface CompanyFormState {
  name: string;
  mail: string;
  startFrom: string;
  mailBox: string[];
  fileTypes: string[];
  storageFolder: string;
  reportOutputFolder: string;
  processingTag: string;
  overrideGlobalProcessingTag: boolean;
  overrideGlobalStorageFolder: boolean;
  overrideGlobalReportOutputFolder: boolean;
}

interface MailboxMetadata {
  displayName: string;
  path: string;
}

type CompanyFormErrorKey = Exclude<
  keyof CompanyFormState,
  "overrideGlobalProcessingTag" | "overrideGlobalStorageFolder" | "overrideGlobalReportOutputFolder"
>;
const DEFAULT_PROCESSING_TAG = "OnBase";

function toDateInputValue(rawValue: string): string {
  const normalized = rawValue.trim();
  if (!normalized) {
    return "";
  }

  const directMatch = /^(\d{4})-(\d{2})-(\d{2})/.exec(normalized);
  if (directMatch) {
    return `${directMatch[1]}-${directMatch[2]}-${directMatch[3]}`;
  }

  const parsed = new Date(normalized);
  if (Number.isNaN(parsed.getTime())) {
    return "";
  }

  const year = parsed.getFullYear();
  const month = String(parsed.getMonth() + 1).padStart(2, "0");
  const day = String(parsed.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function toStartOfDayIso(rawDate: string): string {
  const dateOnly = rawDate.trim();
  if (!dateOnly) {
    return "";
  }

  return `${dateOnly}T00:00:00`;
}

function createEmptyFormState(): CompanyFormState {
  const now = new Date();
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");

  return {
    name: "",
    mail: "",
    startFrom: `${year}-${month}-${day}T00:00:00`,
    mailBox: [],
    fileTypes: [],
    storageFolder: "",
    reportOutputFolder: "",
    processingTag: DEFAULT_PROCESSING_TAG,
    overrideGlobalProcessingTag: true,
    overrideGlobalStorageFolder: false,
    overrideGlobalReportOutputFolder: false
  };
}

function normalizeInput(value: string): string {
  return value
    .replace(/[â€œâ€]/g, "\"")
    .replace(/[â€˜â€™]/g, "'")
    .trim();
}

function stripWrappingQuotes(value: string): string {
  const normalized = normalizeInput(value);
  if (normalized.length < 2) {
    return normalized;
  }

  const startsWithDouble = normalized.startsWith("\"") && normalized.endsWith("\"");
  const startsWithSingle = normalized.startsWith("'") && normalized.endsWith("'");
  if (!startsWithDouble && !startsWithSingle) {
    return normalized;
  }

  return normalized.slice(1, -1).trim();
}

function normalizePath(value: string): string {
  return stripWrappingQuotes(value);
}

function isLikelyPath(path: string): boolean {
  return /^(?:[a-zA-Z]:\\|\\\\[^\\]+\\[^\\]+|\\[^\\].*|\/)/.test(path);
}

function isAbsoluteOrRootedPath(path: string): boolean {
  return /^(?:[a-zA-Z]:\\|\\\\[^\\]+\\[^\\]+|\\[^\\].*|\/)/.test(path.trim());
}

function combinePaths(basePath: string, relativePath: string): string {
  const normalizedBase = normalizePath(basePath).replace(/[\\/]+$/, "");
  const normalizedRelative = normalizePath(relativePath).replace(/^[\\/]+/, "");

  if (!normalizedBase) {
    return normalizedRelative;
  }

  if (!normalizedRelative) {
    return normalizedBase;
  }

  return `${normalizedBase}\\${normalizedRelative}`;
}

function isValidDate(value: string): boolean {
  if (!value.trim()) {
    return true;
  }

  return !Number.isNaN(Date.parse(value));
}

function addUniqueValue(current: string[], rawValue: string): { values: string[]; added: boolean } {
  const value = normalizeInput(rawValue);
  if (!value) {
    return { values: current, added: false };
  }

  const alreadyExists = current.some((item) => item.localeCompare(value, undefined, { sensitivity: "accent" }) === 0);
  if (alreadyExists) {
    return { values: current, added: false };
  }

  return { values: [...current, value], added: true };
}

function formatDateTimeLabel(value: string | null): string {
  if (!value) {
    return "-";
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString();
}

function formatDateLabel(value: string): string {
  const dateOnly = toDateInputValue(value);
  if (!dateOnly) {
    return value || "-";
  }

  const [year, month, day] = dateOnly.split("-");
  return `${month}-${day}-${year}`;
}

function buildPathValidationMessage(apiError: ApiError, kind: "storage" | "report"): string {
  const fieldName = kind === "storage" ? "StorageFolder" : "ReportOutputFolder";
  const backendPathError = getFirstApiErrorDetail(apiError, "path")
    ?? getFirstApiErrorDetail(apiError, "Path")
    ?? getFirstApiErrorDetail(apiError, "$.path")
    ?? getFirstApiErrorDetail(apiError, "");

  if (backendPathError) {
    const normalized = backendPathError.toLowerCase();
    if (normalized.includes("required")) {
      return `Debes ingresar ${fieldName} antes de probar acceso.`;
    }

    if (normalized.includes("invalid") || normalized.includes("format") || normalized.includes("not valid")) {
      return `${fieldName} no tiene un formato valido. Usa ruta local (C:\\Carpeta) o UNC (\\\\Servidor\\Carpeta).`;
    }

    return `No se pudo validar ${fieldName}: ${backendPathError}`;
  }

  if (apiError.code === "BAD_REQUEST") {
    return `${fieldName} no paso validacion. Revisa el formato de la ruta.`;
  }

  return apiError.message ?? "No fue posible validar acceso de carpeta.";
}

export function CompaniesPage(): JSX.Element {
  const [companies, setCompanies] = useState<CompanyListItem[]>([]);
  const [filters, setFilters] = useState<CompanyFilters>({ name: "", mail: "" });
  const [formState, setFormState] = useState<CompanyFormState>(() => createEmptyFormState());
  const [formErrors, setFormErrors] = useState<Record<CompanyFormErrorKey, string | undefined>>({
    name: undefined,
    mail: undefined,
    startFrom: undefined,
    mailBox: undefined,
    fileTypes: undefined,
    storageFolder: undefined,
    reportOutputFolder: undefined,
    processingTag: undefined
  });

  const [mailboxSearchQuery, setMailboxSearchQuery] = useState("");
  const [mailboxSearchResults, setMailboxSearchResults] = useState<MailboxLookupItem[]>([]);
  const [mailboxMetadataById, setMailboxMetadataById] = useState<Record<string, MailboxMetadata>>({});
  const [mailboxSearching, setMailboxSearching] = useState(false);
  const [mailboxLastTestedId, setMailboxLastTestedId] = useState("");
  const [mailboxPreviewResult, setMailboxPreviewResult] = useState<MailboxRecentMessagesResult | null>(null);
  const [mailboxPreviewLoading, setMailboxPreviewLoading] = useState(false);
  const [manualMailboxEnabled, setManualMailboxEnabled] = useState(false);
  const [manualMailboxId, setManualMailboxId] = useState("");
  const [fileTypeInput, setFileTypeInput] = useState("");
  const [checkingStoragePath, setCheckingStoragePath] = useState(false);
  const [checkingReportPath, setCheckingReportPath] = useState(false);
  const [storageAccessResult, setStorageAccessResult] = useState<StorageAccessCheckResult | null>(null);
  const [reportAccessResult, setReportAccessResult] = useState<StorageAccessCheckResult | null>(null);
  const [globalBaseStorageFolder, setGlobalBaseStorageFolder] = useState("");
  const [globalDefaultReportOutputFolder, setGlobalDefaultReportOutputFolder] = useState("");

  const [loadingList, setLoadingList] = useState(true);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editorOpen, setEditorOpen] = useState(false);
  const [toast, setToast] = useState<{ type: "success" | "error"; message: string } | null>(null);

  const [searchMailboxPage, setSearchMailboxPage] = useState(1);
  const [searchMailboxPageSize, setSearchMailboxPageSize] = useState(5);
  const [selectedMailboxPage, setSelectedMailboxPage] = useState(1);
  const [selectedMailboxPageSize, setSelectedMailboxPageSize] = useState(5);
  const [fileTypesPage, setFileTypesPage] = useState(1);
  const [fileTypesPageSize, setFileTypesPageSize] = useState(5);
  const [companiesPage, setCompaniesPage] = useState(1);
  const [companiesPageSize, setCompaniesPageSize] = useState(5);
  const mailboxMetadataCacheRef = useRef<Record<string, MailboxMetadata>>({});

  const refs = {
    name: useRef<HTMLInputElement>(null),
    mail: useRef<HTMLInputElement>(null),
    startFrom: useRef<HTMLInputElement>(null),
    mailBox: useRef<HTMLInputElement>(null),
    fileTypes: useRef<HTMLInputElement>(null),
    storageFolder: useRef<HTMLInputElement>(null),
    reportOutputFolder: useRef<HTMLInputElement>(null),
    processingTag: useRef<HTMLInputElement>(null)
  };

  const loadCompanies = useCallback(async (activeFilters: CompanyFilters) => {
    try {
      setLoadingList(true);
      setListError(null);
      const data = await companiesService.list(activeFilters);
      setCompanies(data);
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      setListError(apiError.message ?? "No se pudo cargar la lista de companies.");
    } finally {
      setLoadingList(false);
    }
  }, []);

  const loadGlobalFolderSettings = useCallback(async () => {
    try {
      const settings = await settingsService.getSettings();
      setGlobalBaseStorageFolder(normalizePath(settings.baseStorageFolder ?? ""));
      setGlobalDefaultReportOutputFolder(normalizePath(settings.defaultReportOutputFolder ?? ""));
    } catch {
      setGlobalBaseStorageFolder("");
      setGlobalDefaultReportOutputFolder("");
    }
  }, []);

  useEffect(() => {
    void loadCompanies({});
    void loadGlobalFolderSettings();
  }, [loadCompanies, loadGlobalFolderSettings]);

  const firstErrorKey = useMemo(() => {
    const orderedKeys: CompanyFormErrorKey[] = [
      "name",
      "mail",
      "startFrom",
      "mailBox",
      "fileTypes",
      "storageFolder",
      "reportOutputFolder",
      "processingTag"
    ];

    return orderedKeys.find((key) => formErrors[key]);
  }, [formErrors]);

  useEffect(() => {
    if (!firstErrorKey) {
      return;
    }

    refs[firstErrorKey].current?.focus();
  }, [firstErrorKey, refs]);

  useEffect(() => {
    setSearchMailboxPage((previous) => clampPage(previous, mailboxSearchResults.length, searchMailboxPageSize));
  }, [mailboxSearchResults, searchMailboxPageSize]);

  useEffect(() => {
    setSelectedMailboxPage((previous) => clampPage(previous, formState.mailBox.length, selectedMailboxPageSize));
    setFileTypesPage((previous) => clampPage(previous, formState.fileTypes.length, fileTypesPageSize));
  }, [formState, selectedMailboxPageSize, fileTypesPageSize]);

  useEffect(() => {
    setMailboxPreviewResult(null);
    setMailboxLastTestedId("");
  }, [formState.mail]);

  useEffect(() => {
    if (!mailboxLastTestedId) {
      return;
    }

    const stillExists = formState.mailBox.some((mailboxId) => mailboxId === mailboxLastTestedId);
    if (!stillExists) {
      setMailboxPreviewResult(null);
      setMailboxLastTestedId("");
    }
  }, [formState.mailBox, mailboxLastTestedId]);

  useEffect(() => {
    setCompaniesPage((previous) => clampPage(previous, companies.length, companiesPageSize));
  }, [companies, companiesPageSize]);

  function resetForm(): void {
    setFormState(createEmptyFormState());
    setFormErrors({
      name: undefined,
      mail: undefined,
      startFrom: undefined,
      mailBox: undefined,
      fileTypes: undefined,
      storageFolder: undefined,
      reportOutputFolder: undefined,
      processingTag: undefined
    });
    setEditingId(null);
    setMailboxSearchQuery("");
    setMailboxSearchResults([]);
    setMailboxMetadataById({});
    setMailboxLastTestedId("");
    setMailboxPreviewResult(null);
    setMailboxPreviewLoading(false);
    setManualMailboxEnabled(false);
    setManualMailboxId("");
    setFileTypeInput("");
    setCheckingStoragePath(false);
    setCheckingReportPath(false);
    setStorageAccessResult(null);
    setReportAccessResult(null);
    setSearchMailboxPage(1);
    setSelectedMailboxPage(1);
    setFileTypesPage(1);
  }

  function openCreateEditor(): void {
    resetForm();
    setEditorOpen(true);
  }

  function closeEditor(): void {
    if (submitting || loadingDetail) {
      return;
    }

    setEditorOpen(false);
    resetForm();
  }

  function onFieldChange<Key extends keyof CompanyFormState>(key: Key, value: CompanyFormState[Key]): void {
    setFormState((previous) => ({
      ...previous,
      [key]: value
    }));
  }

  function normalizeFileType(rawValue: string): string {
    const normalized = normalizeInput(rawValue).replace(/^\./, "");
    return normalized.toUpperCase();
  }

  function validateForm(state: CompanyFormState): Record<CompanyFormErrorKey, string | undefined> {
    const errors: Record<CompanyFormErrorKey, string | undefined> = {
      name: undefined,
      mail: undefined,
      startFrom: undefined,
      mailBox: undefined,
      fileTypes: undefined,
      storageFolder: undefined,
      reportOutputFolder: undefined,
      processingTag: undefined
    };

    if (!state.name.trim()) {
      errors.name = "El nombre es obligatorio.";
    }

    const emailValue = state.mail.trim();
    if (!emailValue) {
      errors.mail = "El correo es obligatorio.";
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(emailValue)) {
      errors.mail = "Ingresa un correo valido.";
    }

    if (!isValidDate(state.startFrom)) {
      errors.startFrom = "La fecha debe tener un formato valido (ej. 2026-03-29).";
    }

    if (state.mailBox.length > 0) {
      const normalized = new Set(state.mailBox.map((item) => item.toLowerCase()));
      if (normalized.size !== state.mailBox.length) {
        errors.mailBox = "MailBox no admite duplicados.";
      }
    }

    if (state.fileTypes.length > 0) {
      const normalized = new Set(state.fileTypes.map((item) => item.toLowerCase()));
      if (normalized.size !== state.fileTypes.length) {
        errors.fileTypes = "FileTypes no admite duplicados.";
      }
    }

    const normalizedStorage = normalizePath(state.storageFolder);
    if (!normalizedStorage) {
      errors.storageFolder = "StorageFolder es obligatorio.";
    } else if (state.overrideGlobalStorageFolder && !isLikelyPath(normalizedStorage)) {
      errors.storageFolder = "Si invalida el global, StorageFolder debe ser ruta local o UNC.";
    } else if (!state.overrideGlobalStorageFolder && isAbsoluteOrRootedPath(normalizedStorage)) {
      errors.storageFolder = "Sin override global, StorageFolder debe ser subcarpeta relativa (ej. Contoso\\Inbox).";
    }

    const normalizedReportOutput = normalizePath(state.reportOutputFolder);
    if (!normalizedReportOutput) {
      errors.reportOutputFolder = "ReportOutputFolder es obligatorio.";
    } else if (state.overrideGlobalReportOutputFolder && !isLikelyPath(normalizedReportOutput)) {
      errors.reportOutputFolder = "Si invalida el global, ReportOutputFolder debe ser ruta local o UNC.";
    } else if (!state.overrideGlobalReportOutputFolder && isAbsoluteOrRootedPath(normalizedReportOutput)) {
      errors.reportOutputFolder = "Sin override global, ReportOutputFolder debe ser subcarpeta relativa (ej. Contoso\\Reportes).";
    }

    if (state.overrideGlobalProcessingTag && !state.processingTag.trim()) {
      errors.processingTag = "ProcessingTag es obligatorio cuando invalida el global.";
    }

    return errors;
  }

  function toPayload(state: CompanyFormState): CompanyUpsertRequest {
    return {
      name: state.name.trim(),
      mail: state.mail.trim(),
      startFrom: state.startFrom.trim(),
      mailBox: state.mailBox.map((item) => normalizeInput(item)).filter((item) => item.length > 0),
      fileTypes: state.fileTypes.map((item) => normalizeFileType(item)).filter((item) => item.length > 0),
      attachmentKeywords: [],
      storageFolder: normalizePath(state.storageFolder),
      reportOutputFolder: normalizePath(state.reportOutputFolder),
      processingTag: normalizeInput(state.processingTag) || DEFAULT_PROCESSING_TAG,
      overrideGlobalProcessingTag: state.overrideGlobalProcessingTag,
      overrideGlobalStorageFolder: state.overrideGlobalStorageFolder,
      overrideGlobalReportOutputFolder: state.overrideGlobalReportOutputFolder
    };
  }

  function getMailboxName(mailboxId: string): string {
    const metadata = mailboxMetadataById[mailboxId];
    if (!metadata || !metadata.displayName.trim()) {
      return "Sin nombre (usa Buscar mailbox)";
    }

    return metadata.displayName;
  }

  function getMailboxPath(mailboxId: string): string {
    const metadata = mailboxMetadataById[mailboxId];
    return metadata?.path?.trim() || "-";
  }

  function removeItemFromList(key: "mailBox" | "fileTypes", value: string): void {
    setFormState((previous) => ({
      ...previous,
      [key]: previous[key].filter((item) => item !== value)
    }));

    if (key === "mailBox") {
      setMailboxMetadataById((previous) => {
        if (!(value in previous)) {
          return previous;
        }

        const next = { ...previous };
        delete next[value];
        return next;
      });
    }
  }

  function handleFileTypeAdd(): void {
    const normalizedValue = normalizeFileType(fileTypeInput);
    if (!normalizedValue) {
      return;
    }

    const updated = addUniqueValue(formState.fileTypes, normalizedValue);
    if (!updated.added) {
      setToast({ type: "error", message: "El file type ya existe en la lista." });
      return;
    }

    onFieldChange("fileTypes", updated.values);
    setFileTypeInput("");
  }

  function handleMailboxManualAdd(): void {
    const normalizedMailboxId = normalizeInput(manualMailboxId);
    const updated = addUniqueValue(formState.mailBox, manualMailboxId);
    if (!updated.added) {
      setToast({ type: "error", message: "El mailbox id ya existe en la lista o esta vacio." });
      return;
    }

    onFieldChange("mailBox", updated.values);
    if (normalizedMailboxId) {
      setMailboxMetadataById((previous) => ({
        ...previous,
        [normalizedMailboxId]: previous[normalizedMailboxId] ?? {
          displayName: "Agregado manualmente",
          path: ""
        }
      }));
      mailboxMetadataCacheRef.current[normalizedMailboxId] = mailboxMetadataCacheRef.current[normalizedMailboxId] ?? {
        displayName: "Agregado manualmente",
        path: ""
      };
    }
    setManualMailboxId("");
  }

  function handleMailboxEnter(event: KeyboardEvent<HTMLInputElement>): void {
    if (event.key !== "Enter") {
      return;
    }

    event.preventDefault();
    handleMailboxManualAdd();
  }

  async function resolveSelectedMailboxMetadata(userMail: string, mailboxIds: string[]): Promise<void> {
    const normalizedMail = normalizeInput(userMail);
    if (!normalizedMail || mailboxIds.length === 0) {
      return;
    }

    try {
      const resolved = await companiesService.resolveMailboxes(normalizedMail, mailboxIds);
      if (resolved.length === 0) {
        return;
      }

      setMailboxMetadataById((previous) => {
        const next = { ...previous };
        resolved.forEach((item) => {
          next[item.id] = {
            displayName: item.displayName,
            path: item.path
          };
        });
        mailboxMetadataCacheRef.current = {
          ...mailboxMetadataCacheRef.current,
          ...next
        };
        return next;
      });
    } catch {
      // Keep placeholders when Graph metadata lookup is unavailable.
    }
  }

  async function searchMailboxes(): Promise<void> {
    const mail = normalizeInput(formState.mail);
    if (!mail) {
      setToast({ type: "error", message: "Debes completar Mail antes de buscar mailbox." });
      refs.mail.current?.focus();
      return;
    }

    const query = normalizeInput(mailboxSearchQuery);
    if (query.length < 2) {
      setToast({ type: "error", message: "Escribe al menos 2 caracteres para buscar mailbox." });
      return;
    }

    try {
      setMailboxSearching(true);
      const results = await companiesService.searchMailboxes(mail, query);
      setMailboxSearchResults(results);
      setMailboxMetadataById((previous) => {
        const next = { ...previous };
        results.forEach((item) => {
          next[item.id] = {
            displayName: item.displayName,
            path: item.path
          };
        });
        mailboxMetadataCacheRef.current = {
          ...mailboxMetadataCacheRef.current,
          ...next
        };
        return next;
      });

      if (results.length === 0) {
        setToast({ type: "error", message: "No se encontraron mailboxes para esa busqueda." });
      }
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      setToast({ type: "error", message: apiError.message ?? "No fue posible buscar mailboxes en Graph." });
    } finally {
      setMailboxSearching(false);
    }
  }

  function addMailboxFromLookup(item: MailboxLookupItem): void {
    const updated = addUniqueValue(formState.mailBox, item.id);
    if (!updated.added) {
      setToast({ type: "error", message: "Ese mailbox id ya esta agregado." });
      return;
    }

    onFieldChange("mailBox", updated.values);
    setMailboxMetadataById((previous) => ({
      ...previous,
      [item.id]: {
        displayName: item.displayName,
        path: item.path
      }
    }));
    mailboxMetadataCacheRef.current[item.id] = {
      displayName: item.displayName,
      path: item.path
    };
  }

  async function handleMailboxPreviewConnection(mailboxId: string): Promise<void> {
    const mail = normalizeInput(formState.mail);
    if (!mail) {
      setToast({ type: "error", message: "Debes completar Mail antes de probar conexion." });
      refs.mail.current?.focus();
      return;
    }

    const normalizedMailboxId = normalizeInput(mailboxId);
    if (!normalizedMailboxId) {
      setToast({ type: "error", message: "Selecciona un mailbox para ejecutar la prueba." });
      return;
    }

    try {
      setMailboxLastTestedId(normalizedMailboxId);
      setMailboxPreviewLoading(true);
      const result = await companiesService.getRecentMailboxMessages(mail, normalizedMailboxId, 5);
      setMailboxPreviewResult(result);
      setToast({
        type: result.healthy ? "success" : "error",
        message: result.healthy
          ? "Conexion validada. Se listaron los ultimos correos de la bandeja."
          : result.errorMessage || "No se pudo validar la conexion con el mailbox."
      });
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      setMailboxPreviewResult(null);
      setToast({ type: "error", message: apiError.message ?? "No fue posible consultar los ultimos correos del mailbox." });
    } finally {
      setMailboxPreviewLoading(false);
    }
  }

  async function handleCheckPathAccess(kind: "storage" | "report"): Promise<void> {
    const rawPath = kind === "storage" ? formState.storageFolder : formState.reportOutputFolder;
    const normalizedPath = normalizePath(rawPath);
    let targetPathForCheck = normalizedPath;

    if (!normalizedPath) {
      setToast({
        type: "error",
        message: kind === "storage"
          ? "Debes ingresar StorageFolder antes de probar acceso."
          : "Debes ingresar ReportOutputFolder antes de probar acceso."
      });
      return;
    }

    if (kind === "storage") {
      if (formState.overrideGlobalStorageFolder) {
        if (!isLikelyPath(normalizedPath)) {
          setToast({
            type: "error",
            message: "Con override global activo, StorageFolder debe ser ruta local o UNC."
          });
          return;
        }
      } else {
        if (isAbsoluteOrRootedPath(normalizedPath)) {
          setToast({
            type: "error",
            message: "Sin override global, StorageFolder debe ser subcarpeta relativa."
          });
          return;
        }

        let baseStorage = normalizePath(globalBaseStorageFolder);
        if (!baseStorage) {
          try {
            const settings = await settingsService.getSettings();
            baseStorage = normalizePath(settings.baseStorageFolder ?? "");
            setGlobalBaseStorageFolder(baseStorage);
          } catch {
            baseStorage = "";
          }
        }

        if (!baseStorage) {
          setToast({
            type: "error",
            message: "No se encontro BaseStorageFolder global. Configuralo en Settings antes de probar acceso."
          });
          return;
        }

        targetPathForCheck = combinePaths(baseStorage, normalizedPath);
      }
    } else {
      if (formState.overrideGlobalReportOutputFolder) {
        if (!isLikelyPath(normalizedPath)) {
          setToast({
            type: "error",
            message: "Con override global activo, ReportOutputFolder debe ser ruta local o UNC."
          });
          return;
        }
      } else {
        if (isAbsoluteOrRootedPath(normalizedPath)) {
          setToast({
            type: "error",
            message: "Sin override global, ReportOutputFolder debe ser subcarpeta relativa."
          });
          return;
        }

        let baseReport = normalizePath(globalDefaultReportOutputFolder);
        if (!baseReport) {
          try {
            const settings = await settingsService.getSettings();
            baseReport = normalizePath(settings.defaultReportOutputFolder ?? "");
            setGlobalDefaultReportOutputFolder(baseReport);
          } catch {
            baseReport = "";
          }
        }

        if (!baseReport) {
          setToast({
            type: "error",
            message: "No se encontro DefaultReportOutputFolder global. Configuralo en Settings antes de probar acceso."
          });
          return;
        }

        targetPathForCheck = combinePaths(baseReport, normalizedPath);
      }
    }

    try {
      if (kind === "storage") {
        setCheckingStoragePath(true);
      } else {
        setCheckingReportPath(true);
      }

      const result = await settingsService.checkStorageAccess(targetPathForCheck);
      if (kind === "storage") {
        setStorageAccessResult(result);
      } else {
        setReportAccessResult(result);
      }

      setToast({
        type: result.success ? "success" : "error",
        message: result.success
          ? `Acceso validado en ${result.normalizedPath}.`
          : result.message || "No fue posible validar acceso a carpeta."
      });
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      const message = buildPathValidationMessage(apiError, kind);
      const failedResult: StorageAccessCheckResult = {
        checkedAtUtc: new Date().toISOString(),
        targetPath: targetPathForCheck,
        normalizedPath: targetPathForCheck,
        exists: false,
        canRead: false,
        canWrite: false,
        success: false,
        message
      };

      if (kind === "storage") {
        setStorageAccessResult(failedResult);
      } else {
        setReportAccessResult(failedResult);
      }

      setToast({ type: "error", message });
    } finally {
      if (kind === "storage") {
        setCheckingStoragePath(false);
      } else {
        setCheckingReportPath(false);
      }
    }
  }

  async function handleFilterSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    await loadCompanies(filters);
  }

  async function startEdit(companyId: string): Promise<void> {
    try {
      setLoadingDetail(true);
      const detail = await companiesService.getById(companyId);
      setEditingId(companyId);
      setEditorOpen(true);
      setFormState({
        name: detail.name,
        mail: detail.mail,
        startFrom: detail.startFrom || createEmptyFormState().startFrom,
        mailBox: detail.mailBox,
        fileTypes: detail.fileTypes,
        storageFolder: normalizePath(detail.storageFolder),
        reportOutputFolder: normalizePath(detail.reportOutputFolder),
        processingTag: detail.processingTag || DEFAULT_PROCESSING_TAG,
        overrideGlobalProcessingTag: detail.overrideGlobalProcessingTag ?? true,
        overrideGlobalStorageFolder: detail.overrideGlobalStorageFolder ?? false,
        overrideGlobalReportOutputFolder: detail.overrideGlobalReportOutputFolder ?? false
      });
      setFormErrors({
        name: undefined,
        mail: undefined,
        startFrom: undefined,
        mailBox: undefined,
        fileTypes: undefined,
        storageFolder: undefined,
        reportOutputFolder: undefined,
        processingTag: undefined
      });
      setMailboxSearchResults([]);
      setMailboxSearchQuery("");
      setMailboxMetadataById(
        detail.mailBox.reduce<Record<string, MailboxMetadata>>((acc, mailboxId) => {
          const cached = mailboxMetadataCacheRef.current[mailboxId];
          acc[mailboxId] = cached ?? {
            displayName: "Sin nombre (usa Buscar mailbox)",
            path: ""
          };
          return acc;
        }, {})
      );
      await resolveSelectedMailboxMetadata(detail.mail, detail.mailBox);
      setMailboxLastTestedId("");
      setMailboxPreviewResult(null);
      setMailboxPreviewLoading(false);
      setManualMailboxEnabled(false);
      setManualMailboxId("");
      setFileTypeInput("");
      setCheckingStoragePath(false);
      setCheckingReportPath(false);
      setStorageAccessResult(null);
      setReportAccessResult(null);
      setSearchMailboxPage(1);
      setSelectedMailboxPage(1);
      setFileTypesPage(1);
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      setToast({ type: "error", message: apiError.message ?? "No se pudo cargar el detalle para edicion." });
    } finally {
      setLoadingDetail(false);
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();

    const normalizedState: CompanyFormState = {
      ...formState,
      storageFolder: normalizePath(formState.storageFolder),
      reportOutputFolder: normalizePath(formState.reportOutputFolder)
    };

    const errors = validateForm(normalizedState);
    setFormErrors(errors);
    setFormState(normalizedState);

    if (Object.values(errors).some(Boolean)) {
      return;
    }

    const payload = toPayload(normalizedState);

    try {
      setSubmitting(true);

      if (editingId) {
        await companiesService.update(editingId, {
          id: editingId,
          ...payload
        });
        setToast({ type: "success", message: "Company actualizada correctamente." });
      } else {
        await companiesService.create(payload);
        setToast({ type: "success", message: "Company creada correctamente." });
      }

      setEditorOpen(false);
      resetForm();
      await loadCompanies(filters);
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      setToast({ type: "error", message: apiError.message ?? "No fue posible guardar la company." });
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDelete(company: CompanyListItem): Promise<void> {
    const shouldDelete = window.confirm(`Eliminar la company "${company.name}"?`);
    if (!shouldDelete) {
      return;
    }

    try {
      await companiesService.remove(company.id);
      setToast({ type: "success", message: "Company eliminada." });
      await loadCompanies(filters);

      if (editingId === company.id) {
        setEditorOpen(false);
        resetForm();
      }
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      setToast({ type: "error", message: apiError.message ?? "No fue posible eliminar la company." });
    }
  }

  const pagedMailboxSearchResults = paginateItems(mailboxSearchResults, searchMailboxPage, searchMailboxPageSize);
  const pagedSelectedMailboxIds = paginateItems(formState.mailBox, selectedMailboxPage, selectedMailboxPageSize);
  const pagedFileTypes = paginateItems(formState.fileTypes, fileTypesPage, fileTypesPageSize);
  const pagedCompanies = paginateItems(companies, companiesPage, companiesPageSize);

  return (
    <>
      <div className="grid">
        {editorOpen ? (
          <div className="modal-backdrop" role="presentation" onClick={closeEditor}>
            <article
              className="card modal-card companies-modal-card"
              role="dialog"
              aria-modal="true"
              aria-labelledby="company-editor-title"
              onClick={(event) => event.stopPropagation()}
            >
              <div className="modal-head row gap">
                <h3 id="company-editor-title">{editingId ? "Editar company" : "Crear company"}</h3>
                <button type="button" className="btn secondary" onClick={closeEditor} disabled={submitting || loadingDetail}>
                  Cerrar
                </button>
              </div>
              {(submitting || loadingDetail) && <Loading text="Procesando formulario..." />}
              <form onSubmit={(event) => void handleSubmit(event)} noValidate className="companies-edit-form">
                <div className="companies-two-col">
                  <div className="list-editor-block">
                    <label htmlFor="name">Name</label>
                    <input
                      id="name"
                      ref={refs.name}
                      value={formState.name}
                      onChange={(event) => onFieldChange("name", event.target.value)}
                      disabled={submitting || loadingDetail}
                    />
                    {formErrors.name ? <p className="field-error">{formErrors.name}</p> : null}
                  </div>

                  <div className="list-editor-block">
                    <label htmlFor="mail">Mail</label>
                    <input
                      id="mail"
                      ref={refs.mail}
                      value={formState.mail}
                      onChange={(event) => onFieldChange("mail", event.target.value)}
                      disabled={submitting || loadingDetail}
                    />
                    {formErrors.mail ? <p className="field-error">{formErrors.mail}</p> : null}
                  </div>
                </div>

                <div className="list-editor-block companies-half-field">
                  <label htmlFor="startFrom">Fecha Inicial de Busqueda</label>
                  <input
                    id="startFrom"
                    ref={refs.startFrom}
                    type="date"
                    lang="en-US"
                    value={toDateInputValue(formState.startFrom)}
                    onChange={(event) => onFieldChange("startFrom", toStartOfDayIso(event.target.value))}
                    disabled={submitting || loadingDetail}
                  />
                  {formErrors.startFrom ? <p className="field-error">{formErrors.startFrom}</p> : null}
                  <small className="hint">Formato visual MM-DD-YYYY. Hora aplicada por defecto: 00:00.</small>
                </div>

                <div className="list-editor-block">
                  <label htmlFor="mailboxSearch">MailBox</label>
                  <div className="inline-form companies-mailbox-inline">
                    <input
                      id="mailboxSearch"
                      placeholder="Buscar mailbox por nombre"
                      value={mailboxSearchQuery}
                      onChange={(event) => setMailboxSearchQuery(event.target.value)}
                      disabled={submitting || loadingDetail || mailboxSearching}
                    />
                    <button
                      type="button"
                      className="btn secondary"
                      onClick={() => void searchMailboxes()}
                      disabled={submitting || loadingDetail || mailboxSearching}
                    >
                      {mailboxSearching ? "Buscando..." : "Buscar mailbox"}
                    </button>
                    <label className="settings-toggle companies-inline-toggle">
                      <input
                        type="checkbox"
                        checked={manualMailboxEnabled}
                        onChange={(event) => {
                          const enabled = event.target.checked;
                          setManualMailboxEnabled(enabled);
                          if (!enabled) {
                            setManualMailboxId("");
                          }
                        }}
                        disabled={submitting || loadingDetail}
                      />
                      Agregar id manual
                    </label>
                    {manualMailboxEnabled ? (
                      <>
                        <input
                          ref={refs.mailBox}
                          placeholder="Mailbox id manual"
                          value={manualMailboxId}
                          onChange={(event) => setManualMailboxId(event.target.value)}
                          onKeyDown={handleMailboxEnter}
                          disabled={submitting || loadingDetail}
                        />
                        <button
                          type="button"
                          className="btn secondary"
                          onClick={handleMailboxManualAdd}
                          disabled={submitting || loadingDetail}
                        >
                          Agregar
                        </button>
                      </>
                    ) : null}
                  </div>
                  <small className="hint">
                    El checkbox manual es solo visual para habilitar/deshabilitar la captura manual de mailbox id.
                  </small>

                  {mailboxSearchResults.length > 0 ? (
                    <>
                      <div className="table-wrap compact-table">
                        <table className="fixed-grid mailbox-search-table">
                          <thead>
                            <tr>
                              <th>Nombre</th>
                              <th>Path</th>
                              <th>MailboxId</th>
                              <th>Accion</th>
                            </tr>
                          </thead>
                          <tbody>
                            {pagedMailboxSearchResults.map((result) => (
                              <tr key={result.id}>
                                <td>{result.displayName}</td>
                                <td>{result.path}</td>
                                <td className="mono-cell">{result.id}</td>
                                <td>
                                  <button type="button" className="btn secondary" onClick={() => addMailboxFromLookup(result)}>
                                    Agregar
                                  </button>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                      <TablePagination
                        totalItems={mailboxSearchResults.length}
                        page={searchMailboxPage}
                        pageSize={searchMailboxPageSize}
                        onPageChange={(page) => setSearchMailboxPage(clampPage(page, mailboxSearchResults.length, searchMailboxPageSize))}
                        onPageSizeChange={(pageSize) => {
                          const normalized = pageSize === 10 ? 10 : 5;
                          setSearchMailboxPageSize(normalized);
                          setSearchMailboxPage(1);
                        }}
                      />
                    </>
                  ) : null}

                  {formState.mailBox.length > 0 ? (
                    <>
                      <div className="table-wrap compact-table">
                        <table className="fixed-grid mailbox-selected-table">
                          <thead>
                            <tr>
                              <th>Nombre</th>
                              <th>Path</th>
                              <th>MailboxId</th>
                              <th>Acciones</th>
                            </tr>
                          </thead>
                          <tbody>
                            {pagedSelectedMailboxIds.map((mailboxId) => (
                              <tr key={mailboxId}>
                                <td>{getMailboxName(mailboxId)}</td>
                                <td>{getMailboxPath(mailboxId)}</td>
                                <td className="mono-cell">{mailboxId}</td>
                                <td>
                                  <div className="row gap">
                                    <button
                                      type="button"
                                      className="btn secondary"
                                      onClick={() => void handleMailboxPreviewConnection(mailboxId)}
                                      disabled={submitting || loadingDetail || mailboxPreviewLoading}
                                    >
                                      {mailboxPreviewLoading && mailboxLastTestedId === mailboxId ? "Probando..." : "Probar conexion"}
                                    </button>
                                    <button
                                      type="button"
                                      className="btn danger"
                                      onClick={() => removeItemFromList("mailBox", mailboxId)}
                                      disabled={submitting || loadingDetail || mailboxPreviewLoading}
                                    >
                                      Quitar
                                    </button>
                                  </div>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                      <TablePagination
                        totalItems={formState.mailBox.length}
                        page={selectedMailboxPage}
                        pageSize={selectedMailboxPageSize}
                        onPageChange={(page) => setSelectedMailboxPage(clampPage(page, formState.mailBox.length, selectedMailboxPageSize))}
                        onPageSizeChange={(pageSize) => {
                          const normalized = pageSize === 10 ? 10 : 5;
                          setSelectedMailboxPageSize(normalized);
                          setSelectedMailboxPage(1);
                        }}
                      />

                      <small className="hint">
                        Solo lectura: la prueba de conexion no descarga adjuntos ni aplica tags. Muestra hasta 5 correos recientes.
                      </small>

                      {mailboxPreviewResult ? (
                        <>
                          <div className={`health-result ${mailboxPreviewResult.healthy ? "healthy" : "unhealthy"}`}>
                            <p>
                              <strong>Resultado:</strong> {mailboxPreviewResult.healthy ? "Conexion valida" : "Conexion fallida"}
                            </p>
                            <p>
                              <strong>Bandeja probada:</strong> {getMailboxName(mailboxPreviewResult.mailboxId)} ({mailboxPreviewResult.mailboxId})
                            </p>
                            <p>
                              <strong>Ultima prueba:</strong> {formatDateTimeLabel(mailboxPreviewResult.checkedAtUtc)}
                            </p>
                            {!mailboxPreviewResult.healthy ? (
                              <p>
                                <strong>Detalle:</strong> {mailboxPreviewResult.errorMessage || mailboxPreviewResult.errorCode}
                              </p>
                            ) : null}
                          </div>

                          {mailboxPreviewResult.healthy && mailboxPreviewResult.messages.length > 0 ? (
                            <div className="table-wrap compact-table">
                              <table className="fixed-grid recent-messages-table">
                                <thead>
                                  <tr>
                                    <th>#</th>
                                    <th>Fecha</th>
                                    <th>Asunto</th>
                                    <th>Remitente</th>
                                    <th>Adjuntos</th>
                                  </tr>
                                </thead>
                                <tbody>
                                  {mailboxPreviewResult.messages.map((message, index) => (
                                    <tr key={message.messageId || `${message.subject}-${index}`}>
                                      <td>{index + 1}</td>
                                      <td>{formatDateTimeLabel(message.receivedDateTime)}</td>
                                      <td>{message.subject || "(sin asunto)"}</td>
                                      <td>{message.sender || "-"}</td>
                                      <td>{message.hasAttachments ? "Si" : "No"}</td>
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            </div>
                          ) : null}

                          {mailboxPreviewResult.healthy && mailboxPreviewResult.messages.length === 0 ? (
                            <small className="hint">La bandeja no tiene correos recientes para mostrar.</small>
                          ) : null}
                        </>
                      ) : null}
                    </>
                  ) : (
                    <small className="hint">Sin mailbox ids agregados.</small>
                  )}

                  {formErrors.mailBox ? <p className="field-error">{formErrors.mailBox}</p> : null}
                </div>

                <div className="list-editor-block">
                  <label htmlFor="fileTypeInput">FileTypes</label>
                  <div className="inline-form">
                    <input
                      id="fileTypeInput"
                      ref={refs.fileTypes}
                      value={fileTypeInput}
                      onChange={(event) => setFileTypeInput(event.target.value)}
                      placeholder="Ej: pdf o xml"
                      disabled={submitting || loadingDetail}
                    />
                    <button type="button" className="btn secondary" onClick={handleFileTypeAdd} disabled={submitting || loadingDetail}>
                      Agregar
                    </button>
                  </div>
                  {formState.fileTypes.length > 0 ? (
                    <>
                      <div className="table-wrap compact-table filetypes-compact-wrap">
                        <table className="fixed-grid filetypes-table filetypes-single-col">
                          <thead>
                            <tr>
                              <th>FileType</th>
                            </tr>
                          </thead>
                          <tbody>
                            {pagedFileTypes.map((fileType) => (
                              <tr key={fileType}>
                                <td>
                                  <div className="filetypes-value-cell">
                                    <span>{fileType}</span>
                                    <button
                                      type="button"
                                      className="btn danger"
                                      onClick={() => removeItemFromList("fileTypes", fileType)}
                                    >
                                      Quitar
                                    </button>
                                  </div>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                      <TablePagination
                        totalItems={formState.fileTypes.length}
                        page={fileTypesPage}
                        pageSize={fileTypesPageSize}
                        onPageChange={(page) => setFileTypesPage(clampPage(page, formState.fileTypes.length, fileTypesPageSize))}
                        onPageSizeChange={(pageSize) => {
                          const normalized = pageSize === 10 ? 10 : 5;
                          setFileTypesPageSize(normalized);
                          setFileTypesPage(1);
                        }}
                      />
                    </>
                  ) : (
                    <small className="hint">Sin file types agregados.</small>
                  )}
                  {formErrors.fileTypes ? <p className="field-error">{formErrors.fileTypes}</p> : null}
                </div>

                <div className="list-editor-block">
                  <div className="company-field-with-action">
                    <div className="companies-field-grow">
                      <label htmlFor="storageFolder">
                        {formState.overrideGlobalStorageFolder
                          ? "StorageFolder (ruta absoluta)"
                          : "StorageFolder (subcarpeta relativa)"}
                      </label>
                      <input
                        id="storageFolder"
                        ref={refs.storageFolder}
                        value={formState.storageFolder}
                        onChange={(event) => onFieldChange("storageFolder", event.target.value)}
                        onBlur={(event) => onFieldChange("storageFolder", normalizePath(event.target.value))}
                        disabled={submitting || loadingDetail}
                      />
                    </div>
                    <button
                      type="button"
                      className="btn secondary"
                      onClick={() => void handleCheckPathAccess("storage")}
                      disabled={submitting || loadingDetail || checkingStoragePath}
                    >
                      {checkingStoragePath ? "Probando..." : "Probar acceso storage"}
                    </button>
                    <label className="settings-toggle companies-inline-toggle">
                      <input
                        type="checkbox"
                        checked={formState.overrideGlobalStorageFolder}
                        onChange={(event) => onFieldChange("overrideGlobalStorageFolder", event.target.checked)}
                        disabled={submitting || loadingDetail}
                      />
                      Invalida storage global
                    </label>
                  </div>
                  {!formState.overrideGlobalStorageFolder ? (
                    <small className="hint">
                      Base global: {globalBaseStorageFolder || "sin configurar"}.
                      Ruta efectiva: {globalBaseStorageFolder ? combinePaths(globalBaseStorageFolder, formState.storageFolder || "(subcarpeta)") : "(configura BaseStorageFolder en Settings)"}.
                    </small>
                  ) : (
                    <small className="hint">Con override activo, esta company usa su propia ruta absoluta.</small>
                  )}
                  {formErrors.storageFolder ? <p className="field-error">{formErrors.storageFolder}</p> : null}
                  {storageAccessResult ? (
                    <div className={`health-result ${storageAccessResult.success ? "healthy" : "unhealthy"}`}>
                      <p>
                        <strong>Storage:</strong> {storageAccessResult.success ? "Acceso valido" : "Acceso fallido"}
                      </p>
                      <p>
                        <strong>Ruta:</strong> {storageAccessResult.normalizedPath}
                      </p>
                      <p>
                        <strong>Detalle:</strong> {storageAccessResult.message || "-"}
                      </p>
                    </div>
                  ) : null}
                </div>

                <div className="list-editor-block">
                  <div className="company-field-with-action">
                    <div className="companies-field-grow">
                      <label htmlFor="reportOutputFolder">
                        {formState.overrideGlobalReportOutputFolder
                          ? "ReportOutputFolder (ruta absoluta)"
                          : "ReportOutputFolder (subcarpeta relativa)"}
                      </label>
                      <input
                        id="reportOutputFolder"
                        ref={refs.reportOutputFolder}
                        value={formState.reportOutputFolder}
                        onChange={(event) => onFieldChange("reportOutputFolder", event.target.value)}
                        onBlur={(event) => onFieldChange("reportOutputFolder", normalizePath(event.target.value))}
                        disabled={submitting || loadingDetail}
                      />
                    </div>
                    <button
                      type="button"
                      className="btn secondary"
                      onClick={() => void handleCheckPathAccess("report")}
                      disabled={submitting || loadingDetail || checkingReportPath}
                    >
                      {checkingReportPath ? "Probando..." : "Probar acceso report"}
                    </button>
                    <label className="settings-toggle companies-inline-toggle">
                      <input
                        type="checkbox"
                        checked={formState.overrideGlobalReportOutputFolder}
                        onChange={(event) => onFieldChange("overrideGlobalReportOutputFolder", event.target.checked)}
                        disabled={submitting || loadingDetail}
                      />
                      Invalida report global
                    </label>
                  </div>
                  {!formState.overrideGlobalReportOutputFolder ? (
                    <small className="hint">
                      Base global: {globalDefaultReportOutputFolder || "sin configurar"}.
                      Ruta efectiva: {globalDefaultReportOutputFolder
                        ? combinePaths(globalDefaultReportOutputFolder, formState.reportOutputFolder || "(subcarpeta)")
                        : "(configura DefaultReportOutputFolder en Settings)"}.
                    </small>
                  ) : (
                    <small className="hint">Con override activo, esta company usa su propia ruta absoluta de reportes.</small>
                  )}
                  {formErrors.reportOutputFolder ? <p className="field-error">{formErrors.reportOutputFolder}</p> : null}
                  {reportAccessResult ? (
                    <div className={`health-result ${reportAccessResult.success ? "healthy" : "unhealthy"}`}>
                      <p>
                        <strong>Report:</strong> {reportAccessResult.success ? "Acceso valido" : "Acceso fallido"}
                      </p>
                      <p>
                        <strong>Ruta:</strong> {reportAccessResult.normalizedPath}
                      </p>
                      <p>
                        <strong>Detalle:</strong> {reportAccessResult.message || "-"}
                      </p>
                    </div>
                  ) : null}
                </div>

                <div className="list-editor-block">
                  <div className="company-processing-row">
                    <div className="companies-field-grow">
                      <label htmlFor="processingTag">ProcessingTag (company)</label>
                      <input
                        id="processingTag"
                        ref={refs.processingTag}
                        value={formState.processingTag}
                        onChange={(event) => onFieldChange("processingTag", event.target.value)}
                        disabled={submitting || loadingDetail || !formState.overrideGlobalProcessingTag}
                      />
                    </div>
                    <label className="settings-toggle companies-inline-toggle">
                      <input
                        type="checkbox"
                        checked={formState.overrideGlobalProcessingTag}
                        onChange={(event) => onFieldChange("overrideGlobalProcessingTag", event.target.checked)}
                        disabled={submitting || loadingDetail}
                      />
                      Invalida tag global
                    </label>
                  </div>
                  {!formState.overrideGlobalProcessingTag ? (
                    <small className="hint">Se usara el ProcessingTag global configurado en Settings.</small>
                  ) : null}
                  {formErrors.processingTag ? <p className="field-error">{formErrors.processingTag}</p> : null}
                </div>

                <div className="modal-actions row gap">
                  <button
                    type="button"
                    className="btn secondary"
                    onClick={closeEditor}
                    disabled={submitting || loadingDetail}
                  >
                    Cancelar
                  </button>
                  <button type="submit" className="btn primary" disabled={submitting || loadingDetail}>
                    {submitting ? "Guardando..." : editingId ? "Actualizar company" : "Crear company"}
                  </button>
                </div>
              </form>
            </article>
          </div>
        ) : null}

        <article className="card">
          <div className="settings-card-head row gap">
            <h3>Companies</h3>
            <button
              type="button"
              className="btn primary"
              onClick={openCreateEditor}
              disabled={editorOpen || loadingDetail || submitting}
            >
              Crear company
            </button>
          </div>
          <form className="inline-form" onSubmit={(event) => void handleFilterSubmit(event)}>
            <input
              value={filters.name ?? ""}
              onChange={(event) => setFilters((previous) => ({ ...previous, name: event.target.value }))}
              placeholder="Filtrar por nombre"
            />
            <input
              value={filters.mail ?? ""}
              onChange={(event) => setFilters((previous) => ({ ...previous, mail: event.target.value }))}
              placeholder="Filtrar por correo"
            />
            <button type="submit" className="btn secondary" disabled={loadingList || loadingDetail}>
              Buscar
            </button>
          </form>

          {loadingDetail && !editorOpen ? <Loading text="Cargando detalle de company..." /> : null}
          {loadingList ? <Loading text="Cargando companies..." /> : null}
          {listError ? <ErrorMessage message={listError} onRetry={() => void loadCompanies(filters)} /> : null}

          {!loadingList && !listError && companies.length === 0 ? (
            <EmptyState title="Sin resultados" description="Ajusta filtros o crea una nueva company." />
          ) : null}

          {!loadingList && !listError && companies.length > 0 ? (
            <>
              <div className="table-wrap">
                <table className="fixed-grid companies-table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Mail</th>
                      <th>Fecha inicial</th>
                      <th>Storage</th>
                      <th>Tag</th>
                      <th>Acciones</th>
                    </tr>
                  </thead>
                  <tbody>
                    {pagedCompanies.map((company) => (
                      <tr key={company.id}>
                        <td>{company.name}</td>
                        <td>{company.mail}</td>
                        <td>{company.startFrom ? formatDateLabel(company.startFrom) : "-"}</td>
                        <td>{company.storageFolder}</td>
                        <td>{company.processingTag}</td>
                        <td>
                          <div className="row gap">
                            <button
                              type="button"
                              className="btn secondary"
                              onClick={() => void startEdit(company.id)}
                              disabled={loadingDetail || editorOpen || submitting}
                            >
                              Editar
                            </button>
                            <button
                              type="button"
                              className="btn danger"
                              onClick={() => void handleDelete(company)}
                              disabled={loadingDetail || editorOpen || submitting}
                            >
                              Eliminar
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <TablePagination
                totalItems={companies.length}
                page={companiesPage}
                pageSize={companiesPageSize}
                onPageChange={(page) => setCompaniesPage(clampPage(page, companies.length, companiesPageSize))}
                onPageSizeChange={(pageSize) => {
                  const normalized = pageSize === 10 ? 10 : 5;
                  setCompaniesPageSize(normalized);
                  setCompaniesPage(1);
                }}
              />
            </>
          ) : null}
        </article>
      </div>

      {toast ? <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} /> : null}
    </>
  );
}

