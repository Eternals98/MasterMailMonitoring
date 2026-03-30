import { GraphSetting, UpdateGraphSettingRequest } from "../types/models";
import { request } from "./httpClient";

export const graphSettingsService = {
  get: (): Promise<GraphSetting> => request<GraphSetting>("/graph-settings"),

  update: (payload: UpdateGraphSettingRequest): Promise<void> =>
    request<void>("/graph-settings", {
      method: "PUT",
      body: payload
    })
};