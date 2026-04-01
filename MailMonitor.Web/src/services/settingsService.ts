import { request } from "./httpClient";
import { Setting, StorageAccessCheckResult, UpdateSettingRequest } from "../types/models";

export const settingsService = {
  getSettings: (): Promise<Setting> => request<Setting>("/settings"),

  updateSettings: (payload: UpdateSettingRequest): Promise<void> =>
    request<void>("/settings", {
      method: "PUT",
      body: payload
    }),

  checkStorageAccess: (path: string): Promise<StorageAccessCheckResult> =>
    request<StorageAccessCheckResult>("/settings/storage-access/check", {
      method: "POST",
      body: { path }
    })
};
