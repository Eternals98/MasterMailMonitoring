import {
  Company,
  CompanyFilters,
  CompanyListItem,
  MailboxLookupItem,
  MailboxRecentMessagesResult,
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
    }),

  searchMailboxes: (userMail: string, query: string): Promise<MailboxLookupItem[]> =>
    request<MailboxLookupItem[]>("/companies/mailboxes/search", {
      query: {
        userMail,
        query
      }
    }),

  resolveMailboxes: (userMail: string, mailboxIds: string[]): Promise<MailboxLookupItem[]> =>
    request<MailboxLookupItem[]>("/companies/mailboxes/resolve", {
      method: "POST",
      body: {
        userMail,
        mailboxIds
      }
    }),

  getRecentMailboxMessages: (userMail: string, mailboxId: string, take = 5): Promise<MailboxRecentMessagesResult> =>
    request<MailboxRecentMessagesResult>("/companies/mailboxes/recent", {
      timeoutMs: 15000,
      query: {
        userMail,
        mailboxId,
        take
      }
    })
};
