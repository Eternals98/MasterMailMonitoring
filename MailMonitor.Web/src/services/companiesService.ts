import {
  Company,
  CompanyFilters,
  CompanyListItem,
  CompanyUpdateRequest,
  CompanyUpsertRequest
} from "../types/models";
import { request } from "./httpClient";

export const companiesService = {
  list: (filters: CompanyFilters = {}): Promise<CompanyListItem[]> =>
    request<CompanyListItem[]>("/companies", {
      query: {
        name: filters.name,
        mail: filters.mail
      }
    }),

  getById: (id: string): Promise<Company> => request<Company>(`/companies/${id}`),

  create: (payload: CompanyUpsertRequest): Promise<Company> =>
    request<Company>("/companies", {
      method: "POST",
      body: payload
    }),

  update: (id: string, payload: CompanyUpdateRequest): Promise<void> =>
    request<void>(`/companies/${id}`, {
      method: "PUT",
      body: payload
    }),

  remove: (id: string): Promise<void> =>
    request<void>(`/companies/${id}`, {
      method: "DELETE"
    })
};