import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { EmptyState } from "../components/EmptyState";
import { ErrorMessage } from "../components/ErrorMessage";
import { Loading } from "../components/Loading";
import { Toast } from "../components/Toast";
import { ApiError } from "../services/apiError";
import { companiesService } from "../services/companiesService";
import { CompanyFilters, CompanyListItem, CompanyUpsertRequest } from "../types/models";

interface CompanyFormState {
  name: string;
  mail: string;
  startFrom: string;
  mailBox: string;
  fileTypes: string;
  attachmentKeywords: string;
  storageFolder: string;
  reportOutputFolder: string;
  processingTag: string;
}

const emptyFormState: CompanyFormState = {
  name: "",
  mail: "",
  startFrom: "",
  mailBox: "",
  fileTypes: "",
  attachmentKeywords: "",
  storageFolder: "",
  reportOutputFolder: "",
  processingTag: ""
};

function isLikelyPath(path: string): boolean {
  return /^(?:[a-zA-Z]:\\|\\\\[^\\]+\\[^\\]+|\/)/.test(path.trim());
}

function parseMultiValue(value: string): { values: string[]; duplicates: string[] } {
  const parts = value
    .split(/[\n,;]/g)
    .map((part) => part.trim())
    .filter((part) => part.length > 0);

  const seen = new Set<string>();
  const duplicates = new Set<string>();

  parts.forEach((part) => {
    const normalized = part.toLowerCase();
    if (seen.has(normalized)) {
      duplicates.add(part);
      return;
    }

    seen.add(normalized);
  });

  return {
    values: parts,
    duplicates: [...duplicates]
  };
}

function isValidDate(value: string): boolean {
  if (!value.trim()) {
    return true;
  }

  return !Number.isNaN(Date.parse(value));
}

export function CompaniesPage(): JSX.Element {
  const [companies, setCompanies] = useState<CompanyListItem[]>([]);
  const [filters, setFilters] = useState<CompanyFilters>({ name: "", mail: "" });
  const [formState, setFormState] = useState<CompanyFormState>(emptyFormState);
  const [formErrors, setFormErrors] = useState<Record<keyof CompanyFormState, string | undefined>>({
    name: undefined,
    mail: undefined,
    startFrom: undefined,
    mailBox: undefined,
    fileTypes: undefined,
    attachmentKeywords: undefined,
    storageFolder: undefined,
    reportOutputFolder: undefined,
    processingTag: undefined
  });

  const [loadingList, setLoadingList] = useState(true);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [toast, setToast] = useState<{ type: "success" | "error"; message: string } | null>(null);

  const refs = {
    name: useRef<HTMLInputElement>(null),
    mail: useRef<HTMLInputElement>(null),
    startFrom: useRef<HTMLInputElement>(null),
    mailBox: useRef<HTMLTextAreaElement>(null),
    fileTypes: useRef<HTMLTextAreaElement>(null),
    attachmentKeywords: useRef<HTMLTextAreaElement>(null),
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

  useEffect(() => {
    void loadCompanies({});
  }, [loadCompanies]);

  const firstErrorKey = useMemo(() => {
    const orderedKeys: (keyof CompanyFormState)[] = [
      "name",
      "mail",
      "startFrom",
      "mailBox",
      "fileTypes",
      "attachmentKeywords",
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

  function resetForm(): void {
    setFormState(emptyFormState);
    setFormErrors({
      name: undefined,
      mail: undefined,
      startFrom: undefined,
      mailBox: undefined,
      fileTypes: undefined,
      attachmentKeywords: undefined,
      storageFolder: undefined,
      reportOutputFolder: undefined,
      processingTag: undefined
    });
    setEditingId(null);
  }

  function onFieldChange<Key extends keyof CompanyFormState>(key: Key, value: CompanyFormState[Key]): void {
    setFormState((previous) => ({
      ...previous,
      [key]: value
    }));
  }

  function validateForm(state: CompanyFormState): Record<keyof CompanyFormState, string | undefined> {
    const errors: Record<keyof CompanyFormState, string | undefined> = {
      name: undefined,
      mail: undefined,
      startFrom: undefined,
      mailBox: undefined,
      fileTypes: undefined,
      attachmentKeywords: undefined,
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
      errors.mail = "Ingresa un correo válido.";
    }

    if (!isValidDate(state.startFrom)) {
      errors.startFrom = "La fecha debe tener un formato válido (ej. 2026-03-29).";
    }

    const mailBox = parseMultiValue(state.mailBox);
    if (mailBox.duplicates.length > 0) {
      errors.mailBox = "MailBox no admite duplicados.";
    }

    const fileTypes = parseMultiValue(state.fileTypes);
    if (fileTypes.duplicates.length > 0) {
      errors.fileTypes = "FileTypes no admite duplicados.";
    }

    const attachmentKeywords = parseMultiValue(state.attachmentKeywords);
    if (attachmentKeywords.duplicates.length > 0) {
      errors.attachmentKeywords = "AttachmentKeywords no admite duplicados.";
    }

    if (!state.storageFolder.trim()) {
      errors.storageFolder = "StorageFolder es obligatorio.";
    } else if (!isLikelyPath(state.storageFolder)) {
      errors.storageFolder = "StorageFolder debe ser una ruta absoluta o UNC.";
    }

    if (!state.reportOutputFolder.trim()) {
      errors.reportOutputFolder = "ReportOutputFolder es obligatorio.";
    } else if (!isLikelyPath(state.reportOutputFolder)) {
      errors.reportOutputFolder = "ReportOutputFolder debe ser una ruta absoluta o UNC.";
    }

    if (!state.processingTag.trim()) {
      errors.processingTag = "ProcessingTag es obligatorio.";
    }

    return errors;
  }

  function toPayload(state: CompanyFormState): CompanyUpsertRequest {
    return {
      name: state.name.trim(),
      mail: state.mail.trim(),
      startFrom: state.startFrom.trim(),
      mailBox: parseMultiValue(state.mailBox).values,
      fileTypes: parseMultiValue(state.fileTypes).values,
      attachmentKeywords: parseMultiValue(state.attachmentKeywords).values,
      storageFolder: state.storageFolder.trim(),
      reportOutputFolder: state.reportOutputFolder.trim(),
      processingTag: state.processingTag.trim()
    };
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
      setFormState({
        name: detail.name,
        mail: detail.mail,
        startFrom: detail.startFrom,
        mailBox: detail.mailBox.join("\n"),
        fileTypes: detail.fileTypes.join("\n"),
        attachmentKeywords: detail.attachmentKeywords.join("\n"),
        storageFolder: detail.storageFolder,
        reportOutputFolder: detail.reportOutputFolder,
        processingTag: detail.processingTag
      });
      setFormErrors({
        name: undefined,
        mail: undefined,
        startFrom: undefined,
        mailBox: undefined,
        fileTypes: undefined,
        attachmentKeywords: undefined,
        storageFolder: undefined,
        reportOutputFolder: undefined,
        processingTag: undefined
      });
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      setToast({ type: "error", message: apiError.message ?? "No se pudo cargar el detalle para edición." });
    } finally {
      setLoadingDetail(false);
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    const errors = validateForm(formState);
    setFormErrors(errors);

    if (Object.values(errors).some(Boolean)) {
      return;
    }

    const payload = toPayload(formState);

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
    const shouldDelete = window.confirm(`¿Eliminar la company "${company.name}"?`);
    if (!shouldDelete) {
      return;
    }

    try {
      await companiesService.remove(company.id);
      setToast({ type: "success", message: "Company eliminada." });
      await loadCompanies(filters);

      if (editingId === company.id) {
        resetForm();
      }
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      setToast({ type: "error", message: apiError.message ?? "No fue posible eliminar la company." });
    }
  }

  return (
    <>
      <div className="grid two-columns">
        <article className="card">
          <h3>{editingId ? "Editar company" : "Crear company"}</h3>
          {(submitting || loadingDetail) && <Loading text="Procesando formulario..." />}
          <form onSubmit={(event) => void handleSubmit(event)} noValidate>
            <label htmlFor="name">Name</label>
            <input
              id="name"
              ref={refs.name}
              value={formState.name}
              onChange={(event) => onFieldChange("name", event.target.value)}
              disabled={submitting || loadingDetail}
            />
            {formErrors.name ? <p className="field-error">{formErrors.name}</p> : null}

            <label htmlFor="mail">Mail</label>
            <input
              id="mail"
              ref={refs.mail}
              value={formState.mail}
              onChange={(event) => onFieldChange("mail", event.target.value)}
              disabled={submitting || loadingDetail}
            />
            {formErrors.mail ? <p className="field-error">{formErrors.mail}</p> : null}

            <label htmlFor="startFrom">StartFrom</label>
            <input
              id="startFrom"
              ref={refs.startFrom}
              value={formState.startFrom}
              onChange={(event) => onFieldChange("startFrom", event.target.value)}
              placeholder="2026-03-29"
              disabled={submitting || loadingDetail}
            />
            {formErrors.startFrom ? <p className="field-error">{formErrors.startFrom}</p> : null}

            <label htmlFor="mailBox">MailBox (uno por línea o separado por coma)</label>
            <textarea
              id="mailBox"
              ref={refs.mailBox}
              rows={3}
              value={formState.mailBox}
              onChange={(event) => onFieldChange("mailBox", event.target.value)}
              disabled={submitting || loadingDetail}
            />
            {formErrors.mailBox ? <p className="field-error">{formErrors.mailBox}</p> : null}

            <label htmlFor="fileTypes">FileTypes (ej. .pdf, .xml)</label>
            <textarea
              id="fileTypes"
              ref={refs.fileTypes}
              rows={3}
              value={formState.fileTypes}
              onChange={(event) => onFieldChange("fileTypes", event.target.value)}
              disabled={submitting || loadingDetail}
            />
            {formErrors.fileTypes ? <p className="field-error">{formErrors.fileTypes}</p> : null}

            <label htmlFor="attachmentKeywords">AttachmentKeywords</label>
            <textarea
              id="attachmentKeywords"
              ref={refs.attachmentKeywords}
              rows={3}
              value={formState.attachmentKeywords}
              onChange={(event) => onFieldChange("attachmentKeywords", event.target.value)}
              disabled={submitting || loadingDetail}
            />
            {formErrors.attachmentKeywords ? <p className="field-error">{formErrors.attachmentKeywords}</p> : null}

            <label htmlFor="storageFolder">StorageFolder</label>
            <input
              id="storageFolder"
              ref={refs.storageFolder}
              value={formState.storageFolder}
              onChange={(event) => onFieldChange("storageFolder", event.target.value)}
              disabled={submitting || loadingDetail}
            />
            {formErrors.storageFolder ? <p className="field-error">{formErrors.storageFolder}</p> : null}

            <label htmlFor="reportOutputFolder">ReportOutputFolder</label>
            <input
              id="reportOutputFolder"
              ref={refs.reportOutputFolder}
              value={formState.reportOutputFolder}
              onChange={(event) => onFieldChange("reportOutputFolder", event.target.value)}
              disabled={submitting || loadingDetail}
            />
            {formErrors.reportOutputFolder ? <p className="field-error">{formErrors.reportOutputFolder}</p> : null}

            <label htmlFor="processingTag">ProcessingTag</label>
            <input
              id="processingTag"
              ref={refs.processingTag}
              value={formState.processingTag}
              onChange={(event) => onFieldChange("processingTag", event.target.value)}
              disabled={submitting || loadingDetail}
            />
            {formErrors.processingTag ? <p className="field-error">{formErrors.processingTag}</p> : null}

            <div className="row gap">
              <button type="submit" className="btn primary" disabled={submitting || loadingDetail}>
                {submitting ? "Guardando..." : editingId ? "Actualizar company" : "Crear company"}
              </button>
              <button
                type="button"
                className="btn secondary"
                onClick={resetForm}
                disabled={submitting || loadingDetail}
              >
                Limpiar
              </button>
            </div>
          </form>
        </article>

        <article className="card">
          <h3>Listado</h3>
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
            <button type="submit" className="btn secondary" disabled={loadingList}>
              Buscar
            </button>
          </form>

          {loadingList ? <Loading text="Cargando companies..." /> : null}
          {listError ? <ErrorMessage message={listError} onRetry={() => void loadCompanies(filters)} /> : null}

          {!loadingList && !listError && companies.length === 0 ? (
            <EmptyState title="Sin resultados" description="Ajusta filtros o crea una nueva company." />
          ) : null}

          {!loadingList && !listError && companies.length > 0 ? (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Mail</th>
                    <th>StartFrom</th>
                    <th>Storage</th>
                    <th>Tag</th>
                    <th>Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {companies.map((company) => (
                    <tr key={company.id}>
                      <td>{company.name}</td>
                      <td>{company.mail}</td>
                      <td>{company.startFrom || "-"}</td>
                      <td>{company.storageFolder}</td>
                      <td>{company.processingTag}</td>
                      <td>
                        <div className="row gap">
                          <button
                            type="button"
                            className="btn secondary"
                            onClick={() => void startEdit(company.id)}
                            disabled={loadingDetail}
                          >
                            Editar
                          </button>
                          <button type="button" className="btn danger" onClick={() => void handleDelete(company)}>
                            Eliminar
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </article>
      </div>

      {toast ? <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} /> : null}
    </>
  );
}