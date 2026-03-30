import { EmailStatistic, EmailStatisticFilters } from "../types/models";
import { request, requestBlob } from "./httpClient";

function formatDateForQuery(date: string | undefined): string | undefined {
  if (!date) {
    return undefined;
  }

  const parsed = new Date(date);
  if (Number.isNaN(parsed.getTime())) {
    return undefined;
  }

  return parsed.toISOString();
}

function getFileNameFromDisposition(contentDisposition: string | null): string {
  if (!contentDisposition) {
    return `email-statistics-${new Date().toISOString().replace(/[:.]/g, "-")}.xlsx`;
  }

  const match = /filename\*?=(?:UTF-8''|\")?([^;\"]+)/i.exec(contentDisposition);
  if (!match || !match[1]) {
    return `email-statistics-${new Date().toISOString().replace(/[:.]/g, "-")}.xlsx`;
  }

  return decodeURIComponent(match[1].replace(/\"/g, "").trim());
}

export const statisticsService = {
  list: (filters: EmailStatisticFilters): Promise<EmailStatistic[]> =>
    request<EmailStatistic[]>("/email-statistics", {
      query: {
        from: formatDateForQuery(filters.from),
        to: formatDateForQuery(filters.to),
        company: filters.company,
        processed: filters.processed
      }
    }),

  exportExcel: async (filters: EmailStatisticFilters): Promise<{ blob: Blob; fileName: string }> => {
    const { blob, headers } = await requestBlob("/reports/export", {
      query: {
        from: formatDateForQuery(filters.from),
        to: formatDateForQuery(filters.to),
        company: filters.company,
        processed: filters.processed
      }
    });

    const fileName = getFileNameFromDisposition(headers.get("content-disposition"));
    return { blob, fileName };
  }
};

export function downloadBlob(blob: Blob, fileName: string): void {
  const link = document.createElement("a");
  const objectUrl = window.URL.createObjectURL(blob);

  link.href = objectUrl;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();

  document.body.removeChild(link);
  window.URL.revokeObjectURL(objectUrl);
}