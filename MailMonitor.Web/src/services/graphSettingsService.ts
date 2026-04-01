import { GraphConnectivityHealth, GraphSetting, UpdateGraphSettingRequest } from "../types/models";
import { request } from "./httpClient";

export const graphSettingsService = {
  get: (): Promise<GraphSetting> => request<GraphSetting>("/graph-settings"),

  update: (payload: UpdateGraphSettingRequest): Promise<void> =>
    request<void>("/graph-settings", {
      method: "PUT",
      body: payload
    }),

  testConnection: (): Promise<GraphConnectivityHealth> =>
    request<GraphConnectivityHealth>("/graph-settings/verify", {
      method: "POST",
      body: {},
      timeoutMs: 30000
    })
};
