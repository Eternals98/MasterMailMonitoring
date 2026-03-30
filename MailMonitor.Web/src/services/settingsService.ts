import { request } from "./httpClient";
import { Setting, UpdateSettingRequest } from "../types/models";

export const settingsService = {
  getSettings: (): Promise<Setting> => request<Setting>("/settings"),

  updateSettings: (payload: UpdateSettingRequest): Promise<void> =>
    request<void>("/settings", {
      method: "PUT",
      body: payload
    })
};