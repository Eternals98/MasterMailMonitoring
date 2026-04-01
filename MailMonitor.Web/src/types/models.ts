export interface Setting {
  baseStorageFolder: string;
  mailSubjectKeywords: string[];
  globalSearchKeywords: string[];
  processingTag: string;
  defaultReportOutputFolder: string;
  defaultFileTypes: string[];
  defaultAttachmentKeywords: string[];
  schedulerTimeZoneId: string;
  schedulerFallbackCronExpression: string;
  storageMaxRetries: number;
  storageBaseDelayMs: number;
  storageMaxDelayMs: number;
  graphHealthCheckEnabled: boolean;
  mailboxSearchEnabled: boolean;
  processingActionsEnabled: boolean;
  updatedAtUtc: string;
  updatedBy: string;
  revision: number;
}

export interface UpdateSettingRequest {
  baseStorageFolder: string;
  mailSubjectKeywords?: string[];
  globalSearchKeywords?: string[];
  processingTag?: string;
  defaultReportOutputFolder?: string;
  defaultFileTypes?: string[];
  defaultAttachmentKeywords?: string[];
  schedulerTimeZoneId?: string;
  schedulerFallbackCronExpression?: string;
  storageMaxRetries?: number;
  storageBaseDelayMs?: number;
  storageMaxDelayMs?: number;
  graphHealthCheckEnabled?: boolean;
  mailboxSearchEnabled?: boolean;
  processingActionsEnabled?: boolean;
  updatedBy?: string;
}

export interface StorageAccessCheckResult {
  checkedAtUtc: string;
  targetPath: string;
  normalizedPath: string;
  exists: boolean;
  canRead: boolean;
  canWrite: boolean;
  success: boolean;
  message: string;
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
  overrideGlobalProcessingTag: boolean;
  overrideGlobalStorageFolder: boolean;
  overrideGlobalReportOutputFolder: boolean;
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
  overrideGlobalProcessingTag: boolean;
  overrideGlobalStorageFolder: boolean;
  overrideGlobalReportOutputFolder: boolean;
}

export interface MailboxLookupItem {
  id: string;
  displayName: string;
  path: string;
}

export interface MailboxRecentMessageItem {
  messageId: string;
  subject: string;
  receivedDateTime: string | null;
  hasAttachments: boolean;
  sender: string;
}

export interface MailboxRecentMessagesResult {
  checkedAtUtc: string;
  healthy: boolean;
  userMail: string;
  mailboxId: string;
  messages: MailboxRecentMessageItem[];
  errorCode: string;
  errorMessage: string;
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
  lastVerificationAtUtc: string | null;
  lastVerificationSucceeded: boolean | null;
  lastVerificationErrorCode: string;
  lastVerificationErrorMessage: string;
}

export interface UpdateGraphSettingRequest {
  instance: string;
  clientId: string;
  tenantId: string;
  clientSecret: string;
  graphUserScopesJson: string;
}

export interface GraphConnectivityHealth {
  checkedAtUtc: string;
  healthy: boolean;
  userMail: string;
  mailboxId: string;
  errorCode: string;
  errorMessage: string;
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
