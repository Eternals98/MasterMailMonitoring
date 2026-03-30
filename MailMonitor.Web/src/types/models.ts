export interface Setting {
  baseStorageFolder: string;
  mailSubjectKeywords: string[];
  processingTag: string;
}

export interface UpdateSettingRequest {
  baseStorageFolder: string;
}

export interface Company {
  id: string;
  name: string;
  mail: string;
  startFrom: string;
  mailBox: string[];
  fileTypes: string[];
  attachmentKeywords: string[];
  storageFolder: string;
  reportOutputFolder: string;
  processingTag: string;
  recordType: string;
  processedSubject: string;
  processedDate: string | null;
  processedAttachmentsCount: number;
}

export interface CompanyListItem {
  id: string;
  name: string;
  mail: string;
  startFrom: string;
  storageFolder: string;
  reportOutputFolder: string;
  processingTag: string;
}

export interface CompanyFilters {
  name?: string;
  mail?: string;
}

export interface CompanyUpsertRequest {
  name: string;
  mail: string;
  startFrom: string;
  mailBox: string[];
  fileTypes: string[];
  attachmentKeywords: string[];
  storageFolder: string;
  reportOutputFolder: string;
  processingTag: string;
}

export interface CompanyUpdateRequest extends CompanyUpsertRequest {
  id: string;
}

export interface GraphSetting {
  instance: string;
  clientId: string;
  tenantId: string;
  clientSecretMasked: string;
  graphUserScopesJson: string;
}

export interface UpdateGraphSettingRequest {
  instance: string;
  clientId: string;
  tenantId: string;
  clientSecret: string;
  graphUserScopesJson: string;
}

export interface Trigger {
  id: string;
  name: string;
  cronExpression: string;
}

export interface EmailStatistic {
  id: string;
  date: string;
  company: string;
  userMail: string;
  processed: boolean;
  subject: string;
  attachmentsCount: number;
  reasonIgnored: string;
  mailbox: string;
  storageFolder: string;
  storedAttachments: string[];
  messageId: string | null;
}

export interface EmailStatisticFilters {
  from?: string;
  to?: string;
  company?: string;
  processed?: boolean;
}