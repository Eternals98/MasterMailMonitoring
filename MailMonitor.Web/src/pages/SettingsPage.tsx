import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { EmptyState } from "../components/EmptyState";
import { ErrorMessage } from "../components/ErrorMessage";
import { Loading } from "../components/Loading";
import { TablePagination, clampPage, paginateItems } from "../components/TablePagination";
import { Toast } from "../components/Toast";
import { ApiError, getFirstApiErrorDetail } from "../services/apiError";
import { settingsService } from "../services/settingsService";
import { Setting, StorageAccessCheckResult, UpdateSettingRequest } from "../types/models";

interface SettingsFormState {
  baseStorageFolder: string;
  processingTag: string;
  defaultReportOutputFolder: string;
  schedulerTimeZoneId: string;
  schedulerFallbackCronExpression: string;
  storageMaxRetries: number;
  storageBaseDelayMs: number;
  storageMaxDelayMs: number;
  graphHealthCheckEnabled: boolean;
  mailboxSearchEnabled: boolean;
  processingActionsEnabled: boolean;
  globalSearchKeywords: string[];
  defaultFileTypes: string[];
  defaultAttachmentKeywords: string[];
}

type SettingsBlock = "routes" | "scheduler" | "switches" | "keywords";

type SettingsFormErrorKey =
  | "baseStorageFolder"
  | "processingTag"
  | "defaultReportOutputFolder"
  | "schedulerTimeZoneId"
  | "schedulerFallbackCronExpression"
  | "schedulerDays"
  | "storageMaxRetries"
  | "storageBaseDelayMs"
  | "storageMaxDelayMs"
  | "globalSearchKeywords";

type ListFieldKey = "globalSearchKeywords" | "defaultFileTypes";
type SchedulerIntervalUnit = "minutes" | "hours";

interface SchedulerIntervalConfig {
  unit: SchedulerIntervalUnit;
  value: number;
  weekDays: string[];
  supported: boolean;
}

const minuteIntervalOptions = Array.from({ length: 59 }, (_, index) => index + 1);
const hourIntervalOptions = Array.from({ length: 24 }, (_, index) => index + 1);
const weekDayOptions = [
  { code: "MON", label: "Lunes" },
  { code: "TUE", label: "Martes" },
  { code: "WED", label: "Miercoles" },
  { code: "THU", label: "Jueves" },
  { code: "FRI", label: "Viernes" },
  { code: "SAT", label: "Sabado" },
  { code: "SUN", label: "Domingo" }
];
const allWeekDayCodes = weekDayOptions.map((item) => item.code);
const weekDayByCode = new Map(weekDayOptions.map((item) => [item.code, item.label]));
const weekDayByNumber = new Map<string, string>([
  ["1", "SUN"],
  ["2", "MON"],
  ["3", "TUE"],
  ["4", "WED"],
  ["5", "THU"],
  ["6", "FRI"],
  ["7", "SAT"]
]);
const fallbackTimeZones = [
  "America/New_York",
  "America/Chicago",
  "America/Denver",
  "America/Los_Angeles",
  "America/Phoenix",
  "America/Puerto_Rico",
  "America/Mexico_City",
  "America/Bogota",
  "Europe/Madrid",
  "UTC"
];

const emptyErrors: Record<SettingsFormErrorKey, string | undefined> = {
  baseStorageFolder: undefined,
  processingTag: undefined,
  defaultReportOutputFolder: undefined,
  schedulerTimeZoneId: undefined,
  schedulerFallbackCronExpression: undefined,
  schedulerDays: undefined,
  storageMaxRetries: undefined,
  storageBaseDelayMs: undefined,
  storageMaxDelayMs: undefined,
  globalSearchKeywords: undefined
};

function isLikelyPath(path: string): boolean {
  return /^(?:[a-zA-Z]:\\|\\\\[^\\]+\\[^\\]+|\\[^\\].*|\/)/.test(path.trim());
}

function normalizeValue(value: string): string {
  return value
    .replace(/[“”]/g, "\"")
    .replace(/[‘’]/g, "'")
    .trim();
}

function stripWrappingQuotes(value: string): string {
  const normalized = normalizeValue(value);
  if (normalized.length < 2) {
    return normalized;
  }

  const wrappedWithDoubleQuotes = normalized.startsWith("\"") && normalized.endsWith("\"");
  const wrappedWithSingleQuotes = normalized.startsWith("'") && normalized.endsWith("'");
  if (!wrappedWithDoubleQuotes && !wrappedWithSingleQuotes) {
    return normalized;
  }

  return normalized.slice(1, -1).trim();
}

function toFormState(setting: Setting): SettingsFormState {
  const globalKeywords = setting.globalSearchKeywords?.length > 0
    ? setting.globalSearchKeywords
    : setting.mailSubjectKeywords;

  return {
    baseStorageFolder: setting.baseStorageFolder ?? "",
    processingTag: setting.processingTag ?? "",
    defaultReportOutputFolder: setting.defaultReportOutputFolder ?? "",
    schedulerTimeZoneId: setting.schedulerTimeZoneId ?? "America/New_York",
    schedulerFallbackCronExpression: setting.schedulerFallbackCronExpression ?? "0 0/10 * ? * * *",
    storageMaxRetries: setting.storageMaxRetries ?? 3,
    storageBaseDelayMs: setting.storageBaseDelayMs ?? 300,
    storageMaxDelayMs: setting.storageMaxDelayMs ?? 4000,
    graphHealthCheckEnabled: setting.graphHealthCheckEnabled ?? true,
    mailboxSearchEnabled: setting.mailboxSearchEnabled ?? true,
    processingActionsEnabled: setting.processingActionsEnabled ?? true,
    globalSearchKeywords: [...(globalKeywords ?? [])],
    defaultFileTypes: [...(setting.defaultFileTypes ?? [])],
    defaultAttachmentKeywords: [...(setting.defaultAttachmentKeywords ?? [])]
  };
}

function addUniqueListValue(current: string[], rawValue: string): { values: string[]; added: boolean } {
  const value = normalizeValue(rawValue);
  if (!value) {
    return { values: current, added: false };
  }

  const exists = current.some((item) => item.localeCompare(value, undefined, { sensitivity: "accent" }) === 0);
  if (exists) {
    return { values: current, added: false };
  }

  return { values: [...current, value], added: true };
}

function getTimeZoneOptions(selectedTimeZone?: string): string[] {
  let options = fallbackTimeZones;
  const intlWithSupported = Intl as typeof Intl & { supportedValuesOf?: (key: string) => string[] };

  if (typeof intlWithSupported.supportedValuesOf === "function") {
    const supported = intlWithSupported.supportedValuesOf("timeZone");
    if (supported.length > 0) {
      options = [...supported].sort((left, right) => left.localeCompare(right));
    }
  }

  if (selectedTimeZone && !options.includes(selectedTimeZone)) {
    return [selectedTimeZone, ...options];
  }

  return options;
}

function normalizeWeekDays(days: string[]): string[] {
  const set = new Set(days.map((day) => normalizeValue(day).toUpperCase()).filter((day) => weekDayByCode.has(day)));
  return allWeekDayCodes.filter((code) => set.has(code));
}

function parseWeekDaysToken(token: string): { weekDays: string[]; supported: boolean } {
  const normalizedToken = normalizeValue(token).toUpperCase();
  if (normalizedToken === "*" || normalizedToken === "?") {
    return { weekDays: [...allWeekDayCodes], supported: true };
  }

  const resolved = new Set<string>();
  const segments = normalizedToken.split(",").map((segment) => segment.trim()).filter((segment) => segment.length > 0);
  if (segments.length === 0) {
    return { weekDays: [...allWeekDayCodes], supported: false };
  }

  for (const segment of segments) {
    if (segment.includes("-")) {
      const [startRaw, endRaw] = segment.split("-", 2);
      const startCode = weekDayByNumber.get(startRaw) ?? startRaw;
      const endCode = weekDayByNumber.get(endRaw) ?? endRaw;
      const startIndex = allWeekDayCodes.indexOf(startCode);
      const endIndex = allWeekDayCodes.indexOf(endCode);
      if (startIndex < 0 || endIndex < 0) {
        return { weekDays: [...allWeekDayCodes], supported: false };
      }

      if (startIndex <= endIndex) {
        for (let index = startIndex; index <= endIndex; index += 1) {
          resolved.add(allWeekDayCodes[index]);
        }
      } else {
        for (let index = startIndex; index < allWeekDayCodes.length; index += 1) {
          resolved.add(allWeekDayCodes[index]);
        }
        for (let index = 0; index <= endIndex; index += 1) {
          resolved.add(allWeekDayCodes[index]);
        }
      }

      continue;
    }

    const mapped = weekDayByNumber.get(segment) ?? segment;
    if (!weekDayByCode.has(mapped)) {
      return { weekDays: [...allWeekDayCodes], supported: false };
    }

    resolved.add(mapped);
  }

  const ordered = allWeekDayCodes.filter((code) => resolved.has(code));
  if (ordered.length === 0) {
    return { weekDays: [...allWeekDayCodes], supported: false };
  }

  return { weekDays: ordered, supported: true };
}

function weekDaysToCronToken(days: string[]): string {
  const normalized = normalizeWeekDays(days);
  if (normalized.length === 0 || normalized.length === allWeekDayCodes.length) {
    return "*";
  }

  return normalized.join(",");
}

function toCronExpression(unit: SchedulerIntervalUnit, value: number, weekDays: string[]): string {
  const safeValue = unit === "minutes"
    ? Math.min(59, Math.max(1, value))
    : Math.min(24, Math.max(1, value));
  const dayOfWeek = weekDaysToCronToken(weekDays);

  if (unit === "hours") {
    return `0 0 0/${safeValue} ? * ${dayOfWeek} *`;
  }

  return `0 0/${safeValue} * ? * ${dayOfWeek} *`;
}

function fromCronExpression(expression: string): SchedulerIntervalConfig {
  const parts = normalizeValue(expression).split(/\s+/).filter((part) => part.length > 0);
  const normalized = parts.length === 6 ? [...parts, "*"] : parts;

  if (normalized.length !== 7) {
    return { unit: "minutes", value: 10, weekDays: [...allWeekDayCodes], supported: false };
  }

  const [seconds, minutes, hours, dayOfMonth, month, dayOfWeek] = normalized;
  if (seconds !== "0" || month !== "*") {
    return { unit: "minutes", value: 10, weekDays: [...allWeekDayCodes], supported: false };
  }

  if (dayOfMonth !== "?" && dayOfMonth !== "*") {
    return { unit: "minutes", value: 10, weekDays: [...allWeekDayCodes], supported: false };
  }

  const parsedWeekDays = parseWeekDaysToken(dayOfWeek);

  const minuteMatch = /^0\/([1-9]\d?)$/.exec(minutes);
  if (minuteMatch && hours === "*") {
    const parsed = Number.parseInt(minuteMatch[1], 10);
    if (parsed >= 1 && parsed <= 59) {
      return {
        unit: "minutes",
        value: parsed,
        weekDays: parsedWeekDays.weekDays,
        supported: parsedWeekDays.supported
      };
    }
  }

  const hourMatch = /^0\/([1-9]\d?)$/.exec(hours);
  if (hourMatch && minutes === "0") {
    const parsed = Number.parseInt(hourMatch[1], 10);
    if (parsed >= 1 && parsed <= 24) {
      return {
        unit: "hours",
        value: parsed,
        weekDays: parsedWeekDays.weekDays,
        supported: parsedWeekDays.supported
      };
    }
  }

  return { unit: "minutes", value: 10, weekDays: parsedWeekDays.weekDays, supported: false };
}

function describeSchedulerInterval(unit: SchedulerIntervalUnit, value: number): string {
  if (unit === "hours") {
    return value === 1 ? "Cada 1 hora" : `Cada ${value} horas`;
  }

  return value === 1 ? "Cada 1 minuto" : `Cada ${value} minutos`;
}

function describeSchedulerDays(days: string[]): string {
  const normalized = normalizeWeekDays(days);
  if (normalized.length === 0 || normalized.length === allWeekDayCodes.length) {
    return "Todos los dias";
  }

  return normalized
    .map((code) => weekDayByCode.get(code) ?? code)
    .join(", ");
}

function blockTitle(block: SettingsBlock): string {
  switch (block) {
    case "routes":
      return "Rutas y etiquetado";
    case "scheduler":
      return "Scheduler y reintentos";
    case "switches":
      return "Interruptores operativos";
    case "keywords":
      return "Keywords y tipos";
    default:
      return "Editar settings";
  }
}

function buildStorageValidationMessage(apiError: ApiError): string {
  const backendPathError = getFirstApiErrorDetail(apiError, "path")
    ?? getFirstApiErrorDetail(apiError, "Path")
    ?? getFirstApiErrorDetail(apiError, "$.path")
    ?? getFirstApiErrorDetail(apiError, "");

  if (backendPathError) {
    const normalized = backendPathError.toLowerCase();
    if (normalized.includes("required")) {
      return "Debes ingresar la ruta antes de probar acceso.";
    }

    if (normalized.includes("invalid") || normalized.includes("format") || normalized.includes("not valid")) {
      return "La ruta no tiene un formato valido. Usa ruta local (C:\\Carpeta) o UNC (\\\\Servidor\\Carpeta).";
    }

    return `No se pudo validar la ruta: ${backendPathError}`;
  }

  if (apiError.code === "BAD_REQUEST") {
    return "La ruta enviada no paso validacion. Revisa el formato antes de reintentar.";
  }

  return apiError.message ?? "No fue posible validar acceso.";
}

export function SettingsPage(): JSX.Element {
  const [setting, setSetting] = useState<Setting | null>(null);
  const [formState, setFormState] = useState<SettingsFormState | null>(null);
  const [activeBlock, setActiveBlock] = useState<SettingsBlock | null>(null);
  const [schedulerIntervalUnit, setSchedulerIntervalUnit] = useState<SchedulerIntervalUnit>("minutes");
  const [schedulerIntervalValue, setSchedulerIntervalValue] = useState<number>(10);
  const [schedulerWeekDays, setSchedulerWeekDays] = useState<string[]>([...allWeekDayCodes]);
  const [schedulerIntervalSupported, setSchedulerIntervalSupported] = useState<boolean>(true);
  const [showSchedulerCronInCard, setShowSchedulerCronInCard] = useState<boolean>(false);
  const [showSchedulerCronInModal, setShowSchedulerCronInModal] = useState<boolean>(false);

  const [newGlobalKeyword, setNewGlobalKeyword] = useState("");
  const [newDefaultFileType, setNewDefaultFileType] = useState("");

  const [summaryKeywordsPage, setSummaryKeywordsPage] = useState(1);
  const [summaryKeywordsPageSize, setSummaryKeywordsPageSize] = useState(5);
  const [summaryFileTypesPage, setSummaryFileTypesPage] = useState(1);
  const [summaryFileTypesPageSize, setSummaryFileTypesPageSize] = useState(5);
  const [editKeywordsPage, setEditKeywordsPage] = useState(1);
  const [editKeywordsPageSize, setEditKeywordsPageSize] = useState(5);
  const [editFileTypesPage, setEditFileTypesPage] = useState(1);
  const [editFileTypesPageSize, setEditFileTypesPageSize] = useState(5);

  const [storageAccessResult, setStorageAccessResult] = useState<StorageAccessCheckResult | null>(null);

  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [checkingStorage, setCheckingStorage] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<SettingsFormErrorKey, string | undefined>>(emptyErrors);
  const [toast, setToast] = useState<{ type: "success" | "error"; message: string } | null>(null);

  const refs = {
    baseStorageFolder: useRef<HTMLInputElement>(null),
    processingTag: useRef<HTMLInputElement>(null),
    defaultReportOutputFolder: useRef<HTMLInputElement>(null),
    schedulerTimeZoneId: useRef<HTMLSelectElement>(null),
    storageMaxRetries: useRef<HTMLInputElement>(null),
    storageBaseDelayMs: useRef<HTMLInputElement>(null),
    storageMaxDelayMs: useRef<HTMLInputElement>(null)
  };

  const firstErrorKey = useMemo(() => {
    const orderedKeys: SettingsFormErrorKey[] = [
      "baseStorageFolder",
      "processingTag",
      "defaultReportOutputFolder",
      "schedulerTimeZoneId",
      "schedulerFallbackCronExpression",
      "schedulerDays",
      "storageMaxRetries",
      "storageBaseDelayMs",
      "storageMaxDelayMs",
      "globalSearchKeywords"
    ];

    return orderedKeys.find((key) => fieldErrors[key]);
  }, [fieldErrors]);

  const loadSettings = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await settingsService.getSettings();
      const schedulerInterval = fromCronExpression(data.schedulerFallbackCronExpression ?? "");
      setSetting(data);
      setFormState(toFormState(data));
      setSchedulerIntervalUnit(schedulerInterval.unit);
      setSchedulerIntervalValue(schedulerInterval.value);
      setSchedulerWeekDays([...schedulerInterval.weekDays]);
      setSchedulerIntervalSupported(schedulerInterval.supported);
      setShowSchedulerCronInCard(false);
      setShowSchedulerCronInModal(false);
      setStorageAccessResult(null);
      setFieldErrors(emptyErrors);
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

  useEffect(() => {
    if (!firstErrorKey || !activeBlock) {
      return;
    }

    if (firstErrorKey in refs) {
      refs[firstErrorKey as keyof typeof refs]?.current?.focus();
    }
  }, [activeBlock, firstErrorKey, refs]);

  const timeZoneOptions = useMemo(
    () => getTimeZoneOptions(formState?.schedulerTimeZoneId),
    [formState?.schedulerTimeZoneId]
  );

  useEffect(() => {
    if (!setting) {
      return;
    }

    const source = setting.globalSearchKeywords?.length > 0
      ? setting.globalSearchKeywords
      : setting.mailSubjectKeywords;

    setSummaryKeywordsPage((previous) => clampPage(previous, source.length, summaryKeywordsPageSize));
    setSummaryFileTypesPage((previous) => clampPage(previous, setting.defaultFileTypes.length, summaryFileTypesPageSize));
  }, [setting, summaryKeywordsPageSize, summaryFileTypesPageSize]);

  useEffect(() => {
    if (!formState) {
      return;
    }

    setEditKeywordsPage((previous) => clampPage(previous, formState.globalSearchKeywords.length, editKeywordsPageSize));
    setEditFileTypesPage((previous) => clampPage(previous, formState.defaultFileTypes.length, editFileTypesPageSize));
  }, [formState, editKeywordsPageSize, editFileTypesPageSize]);

  function syncSchedulerToCron(unit: SchedulerIntervalUnit, rawValue: number, rawWeekDays: string[]): void {
    const normalizedValue = unit === "hours"
      ? Math.min(24, Math.max(1, rawValue))
      : Math.min(59, Math.max(1, rawValue));
    const normalizedWeekDays = normalizeWeekDays(rawWeekDays);

    setSchedulerIntervalUnit(unit);
    setSchedulerIntervalValue(normalizedValue);
    setSchedulerWeekDays(normalizedWeekDays);
    setSchedulerIntervalSupported(true);
    onFieldChange("schedulerFallbackCronExpression", toCronExpression(unit, normalizedValue, normalizedWeekDays));
  }

  function toggleSchedulerWeekDay(code: string): void {
    const normalizedCode = normalizeValue(code).toUpperCase();
    const exists = schedulerWeekDays.includes(normalizedCode);
    const next = exists
      ? schedulerWeekDays.filter((day) => day !== normalizedCode)
      : [...schedulerWeekDays, normalizedCode];

    syncSchedulerToCron(schedulerIntervalUnit, schedulerIntervalValue, next);
  }

  function onFieldChange<Key extends keyof SettingsFormState>(key: Key, value: SettingsFormState[Key]): void {
    setFormState((previous) => {
      if (!previous) {
        return previous;
      }

      return {
        ...previous,
        [key]: value
      };
    });
  }

  function removeListItem(key: ListFieldKey, value: string): void {
    setFormState((previous) => {
      if (!previous) {
        return previous;
      }

      return {
        ...previous,
        [key]: previous[key].filter((item) => item !== value)
      };
    });
  }

  function addListItem(key: ListFieldKey, rawValue: string): boolean {
    if (!formState) {
      return false;
    }

    const updated = addUniqueListValue(formState[key], rawValue);
    if (!updated.added) {
      return false;
    }

    onFieldChange(key, updated.values as SettingsFormState[typeof key]);
    return true;
  }

  function handleAddGlobalKeyword(): void {
    if (!addListItem("globalSearchKeywords", newGlobalKeyword)) {
      setToast({ type: "error", message: "Keyword invalida o repetida." });
      return;
    }

    setNewGlobalKeyword("");
  }

  function handleAddDefaultFileType(): void {
    const value = normalizeValue(newDefaultFileType).replace(/^\./, "").toUpperCase();
    if (!addListItem("defaultFileTypes", value)) {
      setToast({ type: "error", message: "File type invalido o repetido." });
      return;
    }

    setNewDefaultFileType("");
  }

  function validateRoutesBlock(state: SettingsFormState): Record<SettingsFormErrorKey, string | undefined> {
    const errors = { ...emptyErrors };
    const baseStorageFolder = stripWrappingQuotes(state.baseStorageFolder);
    if (!baseStorageFolder) {
      errors.baseStorageFolder = "BaseStorageFolder es obligatorio.";
    } else if (!isLikelyPath(baseStorageFolder)) {
      errors.baseStorageFolder = "BaseStorageFolder debe ser ruta local, UNC o raiz (ej. \\\\Mail).";
    }

    const reportFolder = stripWrappingQuotes(state.defaultReportOutputFolder);
    if (!reportFolder) {
      errors.defaultReportOutputFolder = "DefaultReportOutputFolder es obligatorio.";
    } else if (!isLikelyPath(reportFolder)) {
      errors.defaultReportOutputFolder = "DefaultReportOutputFolder debe ser ruta local, UNC o raiz.";
    }

    if (!normalizeValue(state.processingTag)) {
      errors.processingTag = "ProcessingTag es obligatorio.";
    }

    return errors;
  }

  function validateSchedulerBlock(state: SettingsFormState): Record<SettingsFormErrorKey, string | undefined> {
    const errors = { ...emptyErrors };

    if (!normalizeValue(state.schedulerTimeZoneId)) {
      errors.schedulerTimeZoneId = "SchedulerTimeZoneId es obligatorio.";
    }

    if (!normalizeValue(state.schedulerFallbackCronExpression)) {
      errors.schedulerFallbackCronExpression = "SchedulerFallbackCronExpression es obligatorio.";
    }

    if (normalizeWeekDays(schedulerWeekDays).length === 0) {
      errors.schedulerDays = "Selecciona al menos un dia de ejecucion.";
    }

    if (!Number.isInteger(state.storageMaxRetries) || state.storageMaxRetries < 0 || state.storageMaxRetries > 10) {
      errors.storageMaxRetries = "StorageMaxRetries debe ser entero entre 0 y 10.";
    }

    if (!Number.isInteger(state.storageBaseDelayMs) || state.storageBaseDelayMs < 0) {
      errors.storageBaseDelayMs = "StorageBaseDelayMs debe ser entero mayor o igual a 0.";
    }

    if (!Number.isInteger(state.storageMaxDelayMs) || state.storageMaxDelayMs < state.storageBaseDelayMs) {
      errors.storageMaxDelayMs = "StorageMaxDelayMs debe ser mayor o igual a StorageBaseDelayMs.";
    }

    return errors;
  }

  function validateKeywordsBlock(state: SettingsFormState): Record<SettingsFormErrorKey, string | undefined> {
    const errors = { ...emptyErrors };

    if (state.globalSearchKeywords.length === 0) {
      errors.globalSearchKeywords = "Agrega al menos una keyword global para subject.";
    }

    return errors;
  }

  function validateActiveBlock(state: SettingsFormState, block: SettingsBlock): Record<SettingsFormErrorKey, string | undefined> {
    switch (block) {
      case "routes":
        return validateRoutesBlock(state);
      case "scheduler":
        return validateSchedulerBlock(state);
      case "keywords":
        return validateKeywordsBlock(state);
      default:
        return { ...emptyErrors };
    }
  }

  function buildUpdatePayload(state: SettingsFormState): UpdateSettingRequest {
    const normalizedGlobalKeywords = state.globalSearchKeywords
      .map((item) => normalizeValue(item))
      .filter((item) => item.length > 0);

    return {
      baseStorageFolder: stripWrappingQuotes(state.baseStorageFolder),
      mailSubjectKeywords: normalizedGlobalKeywords,
      globalSearchKeywords: normalizedGlobalKeywords,
      processingTag: normalizeValue(state.processingTag),
      defaultReportOutputFolder: stripWrappingQuotes(state.defaultReportOutputFolder),
      defaultFileTypes: state.defaultFileTypes
        .map((item) => normalizeValue(item).replace(/^\./, "").toUpperCase())
        .filter((item) => item.length > 0),
      schedulerTimeZoneId: normalizeValue(state.schedulerTimeZoneId),
      schedulerFallbackCronExpression: normalizeValue(state.schedulerFallbackCronExpression),
      storageMaxRetries: state.storageMaxRetries,
      storageBaseDelayMs: state.storageBaseDelayMs,
      storageMaxDelayMs: state.storageMaxDelayMs,
      graphHealthCheckEnabled: state.graphHealthCheckEnabled,
      mailboxSearchEnabled: state.mailboxSearchEnabled,
      processingActionsEnabled: state.processingActionsEnabled,
      updatedBy: "web-admin"
    };
  }

  async function handleSaveBlock(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();

    if (!formState || !activeBlock) {
      return;
    }

    const normalizedState: SettingsFormState = {
      ...formState,
      baseStorageFolder: stripWrappingQuotes(formState.baseStorageFolder),
      defaultReportOutputFolder: stripWrappingQuotes(formState.defaultReportOutputFolder),
      processingTag: normalizeValue(formState.processingTag),
      schedulerTimeZoneId: normalizeValue(formState.schedulerTimeZoneId),
      schedulerFallbackCronExpression: normalizeValue(formState.schedulerFallbackCronExpression)
    };

    setFormState(normalizedState);

    const validationErrors = validateActiveBlock(normalizedState, activeBlock);
    setFieldErrors(validationErrors);

    if (Object.values(validationErrors).some(Boolean)) {
      return;
    }

    try {
      setSubmitting(true);
      await settingsService.updateSettings(buildUpdatePayload(normalizedState));
      await loadSettings();
      setToast({ type: "success", message: `${blockTitle(activeBlock)} actualizados correctamente.` });
      closeEditor();
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      const backendPathError = getFirstApiErrorDetail(apiError, "path");
      const backendModelError = getFirstApiErrorDetail(apiError, "");
      setToast({
        type: "error",
        message: backendPathError ?? backendModelError ?? apiError.message ?? "No fue posible guardar settings."
      });
    } finally {
      setSubmitting(false);
    }
  }

  async function handleCheckStorageAccess(): Promise<void> {
    const selectedPath = activeBlock === "routes" && formState
      ? stripWrappingQuotes(formState.baseStorageFolder)
      : stripWrappingQuotes(setting?.baseStorageFolder ?? "");

    if (!selectedPath) {
      setToast({ type: "error", message: "Selecciona una ruta para probar acceso." });
      return;
    }

    try {
      setCheckingStorage(true);
      const result = await settingsService.checkStorageAccess(selectedPath);
      setStorageAccessResult(result);

      if (result.success) {
        setToast({ type: "success", message: "Acceso validado correctamente." });
      } else {
        setToast({ type: "error", message: result.message });
      }
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      const message = buildStorageValidationMessage(apiError);
      setStorageAccessResult({
        checkedAtUtc: new Date().toISOString(),
        targetPath: selectedPath,
        normalizedPath: selectedPath,
        exists: false,
        canRead: false,
        canWrite: false,
        success: false,
        message
      });
      setToast({ type: "error", message });
    } finally {
      setCheckingStorage(false);
    }
  }

  function openEditor(block: SettingsBlock): void {
    if (!setting) {
      return;
    }

    setShowSchedulerCronInModal(false);
    const draft = toFormState(setting);
    if (block === "scheduler") {
      const interval = fromCronExpression(draft.schedulerFallbackCronExpression);
      draft.schedulerFallbackCronExpression = toCronExpression(interval.unit, interval.value, interval.weekDays);
      setSchedulerIntervalUnit(interval.unit);
      setSchedulerIntervalValue(interval.value);
      setSchedulerWeekDays([...interval.weekDays]);
      setSchedulerIntervalSupported(interval.supported);
    }

    setFormState(draft);
    setFieldErrors(emptyErrors);
    setActiveBlock(block);
    setNewGlobalKeyword("");
    setNewDefaultFileType("");
    setEditKeywordsPage(1);
    setEditFileTypesPage(1);
  }

  function closeEditor(): void {
    if (setting) {
      const restored = toFormState(setting);
      const interval = fromCronExpression(restored.schedulerFallbackCronExpression);
      setSchedulerIntervalUnit(interval.unit);
      setSchedulerIntervalValue(interval.value);
      setSchedulerWeekDays([...interval.weekDays]);
      setSchedulerIntervalSupported(interval.supported);
      setFormState(restored);
    }

    setFieldErrors(emptyErrors);
    setActiveBlock(null);
    setShowSchedulerCronInModal(false);
    setNewGlobalKeyword("");
    setNewDefaultFileType("");
  }

  function renderModalForm(block: SettingsBlock, state: SettingsFormState): JSX.Element {
    if (block === "routes") {
      return (
        <>
          <label htmlFor="baseStorageFolder">Carpeta base de almacenamiento</label>
          <input
            id="baseStorageFolder"
            ref={refs.baseStorageFolder}
            value={state.baseStorageFolder}
            onChange={(event) => onFieldChange("baseStorageFolder", event.target.value)}
            disabled={submitting}
          />
          {fieldErrors.baseStorageFolder ? <p className="field-error">{fieldErrors.baseStorageFolder}</p> : null}

          <label htmlFor="defaultReportOutputFolder">Carpeta de salida de reportes</label>
          <input
            id="defaultReportOutputFolder"
            ref={refs.defaultReportOutputFolder}
            value={state.defaultReportOutputFolder}
            onChange={(event) => onFieldChange("defaultReportOutputFolder", event.target.value)}
            disabled={submitting}
          />
          {fieldErrors.defaultReportOutputFolder ? <p className="field-error">{fieldErrors.defaultReportOutputFolder}</p> : null}

          <label htmlFor="processingTag">Tag de procesado</label>
          <input
            id="processingTag"
            ref={refs.processingTag}
            value={state.processingTag}
            onChange={(event) => onFieldChange("processingTag", event.target.value)}
            disabled={submitting}
          />
          {fieldErrors.processingTag ? <p className="field-error">{fieldErrors.processingTag}</p> : null}

          <button type="button" className="btn secondary" onClick={() => void handleCheckStorageAccess()} disabled={checkingStorage || submitting}>
            {checkingStorage ? "Probando acceso..." : "Probar acceso carpeta"}
          </button>
        </>
      );
    }

    if (block === "scheduler") {
      const intervalOptions = schedulerIntervalUnit === "hours" ? hourIntervalOptions : minuteIntervalOptions;
      const allDaysSelected = normalizeWeekDays(schedulerWeekDays).length === allWeekDayCodes.length;
      return (
        <>
          <label htmlFor="schedulerTimeZoneId">Zona horaria del scheduler</label>
          <select
            id="schedulerTimeZoneId"
            ref={refs.schedulerTimeZoneId}
            value={state.schedulerTimeZoneId}
            onChange={(event) => onFieldChange("schedulerTimeZoneId", event.target.value)}
            disabled={submitting}
          >
            {timeZoneOptions.map((timeZoneId) => (
              <option key={timeZoneId} value={timeZoneId}>
                {timeZoneId}
              </option>
            ))}
          </select>
          {fieldErrors.schedulerTimeZoneId ? <p className="field-error">{fieldErrors.schedulerTimeZoneId}</p> : null}

          <label htmlFor="schedulerIntervalUnit">Frecuencia fallback</label>
          <div className="scheduler-interval-grid">
            <select
              id="schedulerIntervalUnit"
              value={schedulerIntervalUnit}
              onChange={(event) => {
                const selectedUnit = event.target.value as SchedulerIntervalUnit;
                const nextValue = selectedUnit === "hours"
                  ? Math.min(24, schedulerIntervalValue)
                  : Math.min(59, schedulerIntervalValue);
                syncSchedulerToCron(selectedUnit, nextValue, schedulerWeekDays);
              }}
              disabled={submitting}
            >
              <option value="minutes">Minutos</option>
              <option value="hours">Horas</option>
            </select>
            <select
              id="schedulerIntervalValue"
              value={schedulerIntervalValue}
              onChange={(event) => syncSchedulerToCron(schedulerIntervalUnit, Number(event.target.value), schedulerWeekDays)}
              disabled={submitting}
            >
              {intervalOptions.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </div>
          <small className="hint">{describeSchedulerInterval(schedulerIntervalUnit, schedulerIntervalValue)}</small>

          <label>Dias de ejecucion</label>
          <label className="settings-toggle">
            <input
              type="checkbox"
              checked={allDaysSelected}
              onChange={(event) => {
                const next = event.target.checked ? [...allWeekDayCodes] : [];
                syncSchedulerToCron(schedulerIntervalUnit, schedulerIntervalValue, next);
              }}
              disabled={submitting}
            />
            Todos los dias
          </label>
          <div className="weekday-grid">
            {weekDayOptions.map((option) => (
              <label key={option.code} className="weekday-chip">
                <input
                  type="checkbox"
                  checked={schedulerWeekDays.includes(option.code)}
                  onChange={() => toggleSchedulerWeekDay(option.code)}
                  disabled={submitting}
                />
                {option.label}
              </label>
            ))}
          </div>
          {fieldErrors.schedulerDays ? <p className="field-error">{fieldErrors.schedulerDays}</p> : null}

          <button type="button" className="btn secondary" onClick={() => setShowSchedulerCronInModal((previous) => !previous)} disabled={submitting}>
            {showSchedulerCronInModal ? "Ocultar expresion CRON" : "Ver expresion CRON"}
          </button>
          {showSchedulerCronInModal ? (
            <>
              <label htmlFor="schedulerFallbackCronExpression">Expresion CRON generada</label>
              <input
                id="schedulerFallbackCronExpression"
                value={state.schedulerFallbackCronExpression}
                readOnly
                disabled
              />
            </>
          ) : null}
          {fieldErrors.schedulerFallbackCronExpression ? <p className="field-error">{fieldErrors.schedulerFallbackCronExpression}</p> : null}
          {!schedulerIntervalSupported ? (
            <small className="hint">Se detecto una expresion previa personalizada. Al guardar se normalizara al formato por intervalo.</small>
          ) : null}

          <label htmlFor="storageMaxRetries">Maximo de reintentos de storage</label>
          <input
            id="storageMaxRetries"
            ref={refs.storageMaxRetries}
            type="number"
            min={0}
            max={10}
            value={state.storageMaxRetries}
            onChange={(event) => onFieldChange("storageMaxRetries", Number(event.target.value))}
            disabled={submitting}
          />
          {fieldErrors.storageMaxRetries ? <p className="field-error">{fieldErrors.storageMaxRetries}</p> : null}

          <label htmlFor="storageBaseDelayMs">Delay base de storage (ms)</label>
          <input
            id="storageBaseDelayMs"
            ref={refs.storageBaseDelayMs}
            type="number"
            min={0}
            value={state.storageBaseDelayMs}
            onChange={(event) => onFieldChange("storageBaseDelayMs", Number(event.target.value))}
            disabled={submitting}
          />
          {fieldErrors.storageBaseDelayMs ? <p className="field-error">{fieldErrors.storageBaseDelayMs}</p> : null}

          <label htmlFor="storageMaxDelayMs">Delay maximo de storage (ms)</label>
          <input
            id="storageMaxDelayMs"
            ref={refs.storageMaxDelayMs}
            type="number"
            min={0}
            value={state.storageMaxDelayMs}
            onChange={(event) => onFieldChange("storageMaxDelayMs", Number(event.target.value))}
            disabled={submitting}
          />
          {fieldErrors.storageMaxDelayMs ? <p className="field-error">{fieldErrors.storageMaxDelayMs}</p> : null}
        </>
      );
    }

    if (block === "switches") {
      return (
        <div className="settings-toggle-grid">
          <label className="settings-toggle">
            <input
              type="checkbox"
              checked={state.graphHealthCheckEnabled}
              onChange={(event) => onFieldChange("graphHealthCheckEnabled", event.target.checked)}
              disabled={submitting}
            />
            Health check Graph habilitado
          </label>
          <label className="settings-toggle">
            <input
              type="checkbox"
              checked={state.mailboxSearchEnabled}
              onChange={(event) => onFieldChange("mailboxSearchEnabled", event.target.checked)}
              disabled={submitting}
            />
            Busqueda de mailbox habilitada
          </label>
          <label className="settings-toggle">
            <input
              type="checkbox"
              checked={state.processingActionsEnabled}
              onChange={(event) => onFieldChange("processingActionsEnabled", event.target.checked)}
              disabled={submitting}
            />
            Acciones de procesamiento (descarga/tag)
          </label>
        </div>
      );
    }

    const pagedGlobalKeywords = paginateItems(state.globalSearchKeywords, editKeywordsPage, editKeywordsPageSize);
    const pagedDefaultFileTypes = paginateItems(state.defaultFileTypes, editFileTypesPage, editFileTypesPageSize);

    return (
      <>
        <div className="list-editor-block">
          <label htmlFor="globalKeywordInput">Keywords globales (Subject contains)</label>
          <div className="inline-form">
            <input
              id="globalKeywordInput"
              value={newGlobalKeyword}
              onChange={(event) => setNewGlobalKeyword(event.target.value)}
              placeholder="Ej: factura"
              disabled={submitting}
            />
            <button type="button" className="btn secondary" onClick={handleAddGlobalKeyword} disabled={submitting}>
              Agregar
            </button>
          </div>

          {state.globalSearchKeywords.length > 0 ? (
            <div className="table-wrap compact-table">
              <table className="fixed-grid keywords-table">
                <thead>
                  <tr>
                    <th>Keyword</th>
                    <th>Accion</th>
                  </tr>
                </thead>
                <tbody>
                  {pagedGlobalKeywords.map((keyword) => (
                    <tr key={keyword}>
                      <td>{keyword}</td>
                      <td>
                        <button type="button" className="btn danger" onClick={() => removeListItem("globalSearchKeywords", keyword)}>
                          Quitar
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <small className="hint">Sin keywords globales.</small>
          )}
          {state.globalSearchKeywords.length > 0 ? (
            <TablePagination
              totalItems={state.globalSearchKeywords.length}
              page={editKeywordsPage}
              pageSize={editKeywordsPageSize}
              onPageChange={(page) => setEditKeywordsPage(clampPage(page, state.globalSearchKeywords.length, editKeywordsPageSize))}
              onPageSizeChange={(pageSize) => {
                const normalized = pageSize === 10 ? 10 : 5;
                setEditKeywordsPageSize(normalized);
                setEditKeywordsPage(1);
              }}
            />
          ) : null}
          {fieldErrors.globalSearchKeywords ? <p className="field-error">{fieldErrors.globalSearchKeywords}</p> : null}
        </div>

        <div className="list-editor-block">
          <label htmlFor="defaultFileTypeInput">Tipos de archivo por defecto</label>
          <div className="inline-form">
            <input
              id="defaultFileTypeInput"
              value={newDefaultFileType}
              onChange={(event) => setNewDefaultFileType(event.target.value)}
              placeholder="Ej: pdf"
              disabled={submitting}
            />
            <button type="button" className="btn secondary" onClick={handleAddDefaultFileType} disabled={submitting}>
              Agregar
            </button>
          </div>
          {state.defaultFileTypes.length > 0 ? (
            <div className="table-wrap compact-table">
              <table className="fixed-grid filetypes-table">
                <thead>
                  <tr>
                    <th>FileType</th>
                    <th>Accion</th>
                  </tr>
                </thead>
                <tbody>
                  {pagedDefaultFileTypes.map((fileType) => (
                    <tr key={fileType}>
                      <td className="mono-cell">{fileType}</td>
                      <td>
                        <button type="button" className="btn danger" onClick={() => removeListItem("defaultFileTypes", fileType)}>
                          Quitar
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <small className="hint">Sin file types por defecto.</small>
          )}
          {state.defaultFileTypes.length > 0 ? (
            <TablePagination
              totalItems={state.defaultFileTypes.length}
              page={editFileTypesPage}
              pageSize={editFileTypesPageSize}
              onPageChange={(page) => setEditFileTypesPage(clampPage(page, state.defaultFileTypes.length, editFileTypesPageSize))}
              onPageSizeChange={(pageSize) => {
                const normalized = pageSize === 10 ? 10 : 5;
                setEditFileTypesPageSize(normalized);
                setEditFileTypesPage(1);
              }}
            />
          ) : null}
        </div>
      </>
    );
  }

  if (loading) {
    return <Loading text="Cargando settings..." />;
  }

  if (error) {
    return <ErrorMessage message={error} onRetry={() => void loadSettings()} />;
  }

  if (!setting || !formState) {
    return <EmptyState title="No hay settings configurados" description="Configura la API y vuelve a intentar." />;
  }

  const editorOpen = activeBlock !== null;
  const currentKeywords = setting.globalSearchKeywords?.length > 0
    ? setting.globalSearchKeywords
    : setting.mailSubjectKeywords;
  const pagedCurrentKeywords = paginateItems(currentKeywords, summaryKeywordsPage, summaryKeywordsPageSize);
  const pagedCurrentFileTypes = paginateItems(setting.defaultFileTypes, summaryFileTypesPage, summaryFileTypesPageSize);
  const schedulerIntervalSummary = fromCronExpression(setting.schedulerFallbackCronExpression ?? "");
  const schedulerDaysSummary = describeSchedulerDays(schedulerIntervalSummary.weekDays);
  const updatedAtLabel = !setting.updatedAtUtc
    ? "Sin registro"
    : Number.isNaN(new Date(setting.updatedAtUtc).getTime())
      ? setting.updatedAtUtc
      : new Date(setting.updatedAtUtc).toLocaleString();

  return (
    <>
      <div className="grid settings-block-grid">
        <article className="card settings-block-card">
          <div className="settings-card-head row gap">
            <h3>Rutas y etiquetado</h3>
            <button type="button" className="btn secondary" onClick={() => openEditor("routes")} disabled={editorOpen}>
              Editar bloque
            </button>
          </div>

          <dl className="definition-list settings-summary-grid">
            <dt>Carpeta base</dt>
            <dd>{setting.baseStorageFolder || "Sin valor"}</dd>
            <dt>Carpeta de reportes</dt>
            <dd>{setting.defaultReportOutputFolder || "Sin valor"}</dd>
            <dt>Tag de procesado</dt>
            <dd>{setting.processingTag || "Sin valor"}</dd>
          </dl>

          <button
            type="button"
            className="btn secondary"
            onClick={() => void handleCheckStorageAccess()}
            disabled={checkingStorage || submitting}
          >
            {checkingStorage ? "Probando acceso..." : "Probar acceso carpeta"}
          </button>

          {storageAccessResult ? (
            <div className={`health-result ${storageAccessResult.success ? "healthy" : "unhealthy"}`}>
              <p>
                <strong>Resultado:</strong> {storageAccessResult.success ? "Acceso correcto" : "Acceso fallido"}
              </p>
              <p>
                <strong>Ruta:</strong> {storageAccessResult.normalizedPath}
              </p>
              <p>
                <strong>Lectura / Escritura:</strong> {storageAccessResult.canRead ? "si" : "no"} / {storageAccessResult.canWrite ? "si" : "no"}
              </p>
              <p>
                <strong>Detalle:</strong> {storageAccessResult.message}
              </p>
            </div>
          ) : (
            <small className="hint">Valida permisos reales de la carpeta antes de ejecutar el worker.</small>
          )}
        </article>

        <article className="card settings-block-card">
          <div className="settings-card-head row gap">
            <h3>Scheduler y reintentos</h3>
            <button type="button" className="btn secondary" onClick={() => openEditor("scheduler")} disabled={editorOpen}>
              Editar bloque
            </button>
          </div>

          <dl className="definition-list settings-summary-grid">
            <dt>Zona horaria</dt>
            <dd>{setting.schedulerTimeZoneId || "Sin valor"}</dd>
            <dt>Frecuencia fallback</dt>
            <dd>
              {schedulerIntervalSummary.supported
                ? describeSchedulerInterval(schedulerIntervalSummary.unit, schedulerIntervalSummary.value)
                : "Expresion personalizada"}
            </dd>
            <dt>Dias de ejecucion</dt>
            <dd>{schedulerDaysSummary}</dd>
            <dt>Politica de reintento</dt>
            <dd>{`Max retries ${setting.storageMaxRetries}, delay ${setting.storageBaseDelayMs}-${setting.storageMaxDelayMs} ms`}</dd>
          </dl>
          <button
            type="button"
            className="btn secondary"
            onClick={() => setShowSchedulerCronInCard((previous) => !previous)}
            disabled={editorOpen}
          >
            {showSchedulerCronInCard ? "Ocultar expresion CRON" : "Ver expresion CRON"}
          </button>
          {showSchedulerCronInCard ? (
            <small className="hint mono-cell">{setting.schedulerFallbackCronExpression || "Sin valor"}</small>
          ) : null}
        </article>

        <article className="card settings-block-card">
          <div className="settings-card-head row gap">
            <h3>Interruptores operativos</h3>
            <button type="button" className="btn secondary" onClick={() => openEditor("switches")} disabled={editorOpen}>
              Editar bloque
            </button>
          </div>

          <div className="settings-switch-status-grid">
            <div className={`health-chip ${setting.graphHealthCheckEnabled ? "healthy" : "unhealthy"}`}>
              Health check Graph: {setting.graphHealthCheckEnabled ? "ON" : "OFF"}
            </div>
            <div className={`health-chip ${setting.mailboxSearchEnabled ? "healthy" : "unhealthy"}`}>
              Busqueda mailbox: {setting.mailboxSearchEnabled ? "ON" : "OFF"}
            </div>
            <div className={`health-chip ${setting.processingActionsEnabled ? "healthy" : "unhealthy"}`}>
              Descarga y tag: {setting.processingActionsEnabled ? "ON" : "OFF"}
            </div>
          </div>
        </article>

        <article className="card settings-block-card">
          <div className="settings-card-head row gap">
            <h3>Keywords y tipos</h3>
            <button type="button" className="btn secondary" onClick={() => openEditor("keywords")} disabled={editorOpen}>
              Editar bloque
            </button>
          </div>

          <p className="hint">Keywords globales del asunto y tipos de archivo por defecto.</p>

          {currentKeywords.length > 0 ? (
            <div className="table-wrap compact-table">
              <table className="fixed-grid keywords-table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Keyword global</th>
                  </tr>
                </thead>
                <tbody>
                  {pagedCurrentKeywords.map((keyword, index) => (
                    <tr key={`${keyword}-${index}`}>
                      <td>{(summaryKeywordsPage - 1) * summaryKeywordsPageSize + index + 1}</td>
                      <td>{keyword}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <small className="hint">Sin keywords globales configuradas.</small>
          )}
          {currentKeywords.length > 0 ? (
            <TablePagination
              totalItems={currentKeywords.length}
              page={summaryKeywordsPage}
              pageSize={summaryKeywordsPageSize}
              onPageChange={(page) => setSummaryKeywordsPage(clampPage(page, currentKeywords.length, summaryKeywordsPageSize))}
              onPageSizeChange={(pageSize) => {
                const normalized = pageSize === 10 ? 10 : 5;
                setSummaryKeywordsPageSize(normalized);
                setSummaryKeywordsPage(1);
              }}
            />
          ) : null}

          <p className="hint">Tipos de archivo por defecto.</p>
          {setting.defaultFileTypes.length > 0 ? (
            <div className="table-wrap compact-table">
              <table className="fixed-grid filetypes-table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>FileType</th>
                  </tr>
                </thead>
                <tbody>
                  {pagedCurrentFileTypes.map((fileType, index) => (
                    <tr key={`${fileType}-${index}`}>
                      <td>{(summaryFileTypesPage - 1) * summaryFileTypesPageSize + index + 1}</td>
                      <td className="mono-cell">{fileType}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <small className="hint">Sin file types por defecto.</small>
          )}
          {setting.defaultFileTypes.length > 0 ? (
            <TablePagination
              totalItems={setting.defaultFileTypes.length}
              page={summaryFileTypesPage}
              pageSize={summaryFileTypesPageSize}
              onPageChange={(page) => setSummaryFileTypesPage(clampPage(page, setting.defaultFileTypes.length, summaryFileTypesPageSize))}
              onPageSizeChange={(pageSize) => {
                const normalized = pageSize === 10 ? 10 : 5;
                setSummaryFileTypesPageSize(normalized);
                setSummaryFileTypesPage(1);
              }}
            />
          ) : null}

          <div className="row gap settings-meta-row">
            <span className="settings-counter">{`FileTypes: ${setting.defaultFileTypes.length}`}</span>
          </div>
        </article>
      </div>

      <p className="hint settings-footnote">{`Ultima actualizacion: ${updatedAtLabel} por ${setting.updatedBy || "system"}`}</p>

      {activeBlock ? (
        <div className="modal-backdrop" role="presentation" onClick={() => !submitting && closeEditor()}>
          <div
            className="modal-card"
            role="dialog"
            aria-modal="true"
            aria-labelledby="settings-block-editor-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="modal-head row gap">
              <h3 id="settings-block-editor-title">{`Editar ${blockTitle(activeBlock)}`}</h3>
              <button type="button" className="btn secondary" onClick={closeEditor} disabled={submitting}>
                Cerrar
              </button>
            </div>

            <form onSubmit={(event) => void handleSaveBlock(event)} noValidate className="settings-edit-form">
              {renderModalForm(activeBlock, formState)}

              <div className="modal-actions row gap">
                <button type="button" className="btn secondary" onClick={closeEditor} disabled={submitting}>
                  Cancelar
                </button>
                <button type="submit" className="btn primary" disabled={submitting}>
                  {submitting ? "Guardando..." : "Guardar bloque"}
                </button>
              </div>
            </form>
          </div>
        </div>
      ) : null}

      {toast ? <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} /> : null}
    </>
  );
}

