import { FormEvent, useEffect, useMemo, useState } from "react";
import { EmptyState } from "../components/EmptyState";
import { ErrorMessage } from "../components/ErrorMessage";
import { Loading } from "../components/Loading";
import { TablePagination, clampPage, paginateItems } from "../components/TablePagination";
import { Toast } from "../components/Toast";
import { ApiError } from "../services/apiError";
import { companiesService } from "../services/companiesService";
import { downloadBlob, statisticsService } from "../services/statisticsService";
import { EmailStatistic, EmailStatisticFilters } from "../types/models";

type StatusFilter = "all" | "processed" | "ignored";

interface MonitoringFormFilters {
  from: string;
  to: string;
  company: string;
  status: StatusFilter;
}

function toApiFilters(filters: MonitoringFormFilters): EmailStatisticFilters {
  return {
    from: filters.from || undefined,
    to: filters.to || undefined,
    company: filters.company || undefined,
    processed:
      filters.status === "all" ? undefined : filters.status === "processed" ? true : false
  };
}

function formatDateTime(date: string): string {
  const parsed = new Date(date);
  if (Number.isNaN(parsed.getTime())) {
    return date;
  }

  return parsed.toLocaleString();
}

function getErrorMessage(error: unknown, fallback: string): string {
  if (typeof error === "object" && error !== null && "message" in error) {
    const value = (error as { message?: unknown }).message;
    if (typeof value === "string" && value.trim().length > 0) {
      return value;
    }
  }

  return fallback;
}

export function MonitoringPage(): JSX.Element {
  const [filters, setFilters] = useState<MonitoringFormFilters>({
    from: "",
    to: "",
    company: "",
    status: "all"
  });

  const [statistics, setStatistics] = useState<EmailStatistic[]>([]);
  const [companyOptions, setCompanyOptions] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ type: "success" | "error"; message: string } | null>(null);
  const [tablePage, setTablePage] = useState(1);
  const [tablePageSize, setTablePageSize] = useState(5);

  async function loadStatistics(activeFilters: MonitoringFormFilters): Promise<void> {
    try {
      setLoading(true);
      setError(null);
      const data = await statisticsService.list(toApiFilters(activeFilters));
      setStatistics(data);
    } catch (unknownError) {
      const apiError = unknownError as ApiError;
      setError(apiError.message ?? "No fue posible cargar estadísticas.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadStatistics(filters);
  }, []);

  useEffect(() => {
    async function loadCompaniesForFilter(): Promise<void> {
      try {
        const companies = await companiesService.list({});
        const names = [...new Set(companies.map((company) => company.name).filter(Boolean))];
        setCompanyOptions(names.sort((a, b) => a.localeCompare(b)));
      } catch {
        setCompanyOptions([]);
      }
    }

    void loadCompaniesForFilter();
  }, []);

  useEffect(() => {
    setTablePage((previous) => clampPage(previous, statistics.length, tablePageSize));
  }, [statistics, tablePageSize]);

  const processedCount = useMemo(
    () => statistics.filter((item) => item.processed).length,
    [statistics]
  );

  const ignoredCount = useMemo(
    () => statistics.filter((item) => !item.processed).length,
    [statistics]
  );
  const pagedStatistics = useMemo(
    () => paginateItems(statistics, tablePage, tablePageSize),
    [statistics, tablePage, tablePageSize]
  );

  async function handleFilterSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    await loadStatistics(filters);
  }

  async function handleExport(): Promise<void> {
    try {
      setExporting(true);
      const { blob, fileName } = await statisticsService.exportExcel(toApiFilters(filters));
      downloadBlob(blob, fileName);
      setToast({ type: "success", message: "Archivo exportado correctamente." });
    } catch (unknownError) {
      setToast({ type: "error", message: getErrorMessage(unknownError, "No fue posible exportar el Excel.") });
    } finally {
      setExporting(false);
    }
  }

  return (
    <>
      <article className="card">
        <h3>Filtros</h3>
        <form className="inline-form monitoring-filters" onSubmit={(event) => void handleFilterSubmit(event)}>
          <label>
            Desde
            <input
              type="date"
              value={filters.from}
              onChange={(event) => setFilters((previous) => ({ ...previous, from: event.target.value }))}
            />
          </label>

          <label>
            Hasta
            <input
              type="date"
              value={filters.to}
              onChange={(event) => setFilters((previous) => ({ ...previous, to: event.target.value }))}
            />
          </label>

          <label>
            Company
            <input
              value={filters.company}
              onChange={(event) => setFilters((previous) => ({ ...previous, company: event.target.value }))}
              list="company-options"
              placeholder="Contoso"
            />
            <datalist id="company-options">
              {companyOptions.map((name) => (
                <option key={name} value={name} />
              ))}
            </datalist>
          </label>

          <label>
            Estado
            <select
              value={filters.status}
              onChange={(event) =>
                setFilters((previous) => ({
                  ...previous,
                  status: event.target.value as StatusFilter
                }))
              }
            >
              <option value="all">Todos</option>
              <option value="processed">Procesados</option>
              <option value="ignored">Ignorados</option>
            </select>
          </label>

          <button type="submit" className="btn secondary" disabled={loading}>
            {loading ? "Filtrando..." : "Aplicar filtros"}
          </button>

          <button type="button" className="btn primary" onClick={() => void handleExport()} disabled={exporting}>
            {exporting ? "Exportando..." : "Exportar Excel"}
          </button>
        </form>
      </article>

      <div className="kpi-grid">
        <article className="card kpi">
          <p>Total</p>
          <strong>{statistics.length}</strong>
        </article>
        <article className="card kpi">
          <p>Procesados</p>
          <strong>{processedCount}</strong>
        </article>
        <article className="card kpi">
          <p>Ignorados</p>
          <strong>{ignoredCount}</strong>
        </article>
      </div>

      {loading ? <Loading text="Consultando estadísticas..." /> : null}
      {error ? <ErrorMessage message={error} onRetry={() => void loadStatistics(filters)} /> : null}

      {!loading && !error && statistics.length === 0 ? (
        <EmptyState title="Sin estadísticas" description="No hay resultados para los filtros seleccionados." />
      ) : null}

      {!loading && !error && statistics.length > 0 ? (
        <article className="card table-wrap">
          <table>
            <thead>
              <tr>
                <th>Fecha</th>
                <th>Company</th>
                <th>User Mail</th>
                <th>Estado</th>
                <th>Adjuntos</th>
                <th>Asunto</th>
                <th>Motivo ignorado</th>
              </tr>
            </thead>
            <tbody>
              {pagedStatistics.map((item) => (
                <tr key={item.id}>
                  <td>{formatDateTime(item.date)}</td>
                  <td>{item.company}</td>
                  <td>{item.userMail}</td>
                  <td>{item.processed ? "Procesado" : "Ignorado"}</td>
                  <td>{item.attachmentsCount}</td>
                  <td>{item.subject}</td>
                  <td>{item.reasonIgnored || "-"}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <TablePagination
            totalItems={statistics.length}
            page={tablePage}
            pageSize={tablePageSize}
            onPageChange={(page) => setTablePage(clampPage(page, statistics.length, tablePageSize))}
            onPageSizeChange={(pageSize) => {
              const normalized = pageSize === 10 ? 10 : 5;
              setTablePageSize(normalized);
              setTablePage(1);
            }}
          />
        </article>
      ) : null}

      {toast ? <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} /> : null}
    </>
  );
}
