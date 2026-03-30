using System.Text;
using MailMonitor.Application.Abstractions.Storage;
using MailMonitor.Domain.Abstractions;
using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;

namespace MailMonitor.Infrastructure.Storage
{
    /// <summary>
    /// Stores attachments in the configured file system path with robust sanitization and retry logic.
    /// </summary>
    internal sealed class FileSystemAttachmentStorageService : IAttachmentStorageService
    {
        private const string LogCodePermissionDenied = "STG-403";
        private const string LogCodePathUnavailable = "STG-404";
        private const string LogCodeInvalidPath = "STG-422";
        private const string LogCodeTransientRetry = "STG-503";
        private const string LogCodeTransientExhausted = "STG-504";
        private const string LogCodeUnhandled = "STG-500";

        private static readonly HashSet<int> TransientWin32Codes =
        [
            32,  // ERROR_SHARING_VIOLATION
            33,  // ERROR_LOCK_VIOLATION
            53,  // ERROR_BAD_NETPATH
            64,  // ERROR_NETNAME_DELETED
            67,  // ERROR_BAD_NET_NAME
            121, // ERROR_SEM_TIMEOUT
            1231 // ERROR_NETWORK_UNREACHABLE
        ];

        private readonly ILogger<FileSystemAttachmentStorageService> _logger;
        private readonly AttachmentStorageOptions _options;

        public FileSystemAttachmentStorageService(
            ILogger<FileSystemAttachmentStorageService> logger,
            IOptions<AttachmentStorageOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public Result<StoredAttachmentInfo> StoreFile(Company company, string mailSubject, FileAttachment attachment, Setting settings)
        {
            if (attachment is null || string.IsNullOrWhiteSpace(attachment.Name) || attachment.ContentBytes is null)
            {
                return Result.Failure<StoredAttachmentInfo>(Error.NullValue);
            }

            if (string.IsNullOrWhiteSpace(settings.BaseStorageFolder))
            {
                _logger.LogError("[{LogCode}] Base storage folder is empty.", LogCodeInvalidPath);
                return Result.Failure<StoredAttachmentInfo>(DomainErrors.Storage.InvalidBasePath);
            }

            var resolvedPathResult = TryResolveStoragePaths(
                settings.BaseStorageFolder,
                company.StorageFolder,
                mailSubject,
                attachment.Name);

            if (resolvedPathResult.IsFailure)
            {
                _logger.LogError(
                    "[{LogCode}] Invalid storage path. Error: {ErrorCode} - {ErrorName}. BasePath: {BasePath}, CompanyFolder: {CompanyFolder}, Subject: {Subject}, AttachmentName: {AttachmentName}",
                    LogCodeInvalidPath,
                    resolvedPathResult.Error.Code,
                    resolvedPathResult.Error.Name,
                    settings.BaseStorageFolder,
                    company.StorageFolder,
                    mailSubject,
                    attachment.Name);

                return Result.Failure<StoredAttachmentInfo>(resolvedPathResult.Error);
            }

            var resolvedPath = resolvedPathResult.Value;
            var maxRetries = Math.Max(_options.MaxRetries, 1);
            var baseDelayMs = Math.Max(_options.BaseDelayMilliseconds, 50);
            var maxDelayMs = Math.Max(_options.MaxDelayMilliseconds, baseDelayMs);

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Directory.CreateDirectory(resolvedPath.StorageDirectoryPath);
                    File.WriteAllBytes(resolvedPath.FilePath, attachment.ContentBytes);

                    _logger.LogInformation(
                        "Stored attachment {AttachmentName} at path {FilePath}.",
                        attachment.Name,
                        resolvedPath.FilePath);

                    return Result.Success(new StoredAttachmentInfo(resolvedPath.StorageDirectoryPath, resolvedPath.FilePath));
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError(
                        ex,
                        "[{LogCode}] Permission denied while writing attachment {AttachmentName} to {FilePath}.",
                        LogCodePermissionDenied,
                        attachment.Name,
                        resolvedPath.FilePath);

                    return Result.Failure<StoredAttachmentInfo>(DomainErrors.Storage.PermissionDenied);
                }
                catch (DirectoryNotFoundException ex)
                {
                    _logger.LogError(
                        ex,
                        "[{LogCode}] Storage path unavailable while writing attachment {AttachmentName} to {FilePath}.",
                        LogCodePathUnavailable,
                        attachment.Name,
                        resolvedPath.FilePath);

                    return Result.Failure<StoredAttachmentInfo>(DomainErrors.Storage.PathUnavailable);
                }
                catch (DriveNotFoundException ex)
                {
                    _logger.LogError(
                        ex,
                        "[{LogCode}] Storage drive unavailable while writing attachment {AttachmentName} to {FilePath}.",
                        LogCodePathUnavailable,
                        attachment.Name,
                        resolvedPath.FilePath);

                    return Result.Failure<StoredAttachmentInfo>(DomainErrors.Storage.PathUnavailable);
                }
                catch (PathTooLongException ex)
                {
                    _logger.LogError(
                        ex,
                        "[{LogCode}] Storage path too long for attachment {AttachmentName} at {FilePath}.",
                        LogCodeInvalidPath,
                        attachment.Name,
                        resolvedPath.FilePath);

                    return Result.Failure<StoredAttachmentInfo>(DomainErrors.Storage.InvalidRelativePath);
                }
                catch (IOException ex) when (IsTransientIoException(ex))
                {
                    if (attempt >= maxRetries)
                    {
                        _logger.LogError(
                            ex,
                            "[{LogCode}] Transient network/UNC error persisted after {AttemptCount} attempts while writing attachment {AttachmentName} to {FilePath}.",
                            LogCodeTransientExhausted,
                            attempt,
                            attachment.Name,
                            resolvedPath.FilePath);

                        return Result.Failure<StoredAttachmentInfo>(DomainErrors.Storage.TransientNetworkFailure);
                    }

                    var delay = ComputeRetryDelay(attempt, baseDelayMs, maxDelayMs);

                    _logger.LogWarning(
                        ex,
                        "[{LogCode}] Transient network/UNC error writing attachment {AttachmentName} to {FilePath}. Attempt {Attempt}/{MaxRetries}. Retrying in {DelayMs} ms.",
                        LogCodeTransientRetry,
                        attachment.Name,
                        resolvedPath.FilePath,
                        attempt,
                        maxRetries,
                        delay.TotalMilliseconds);

                    Thread.Sleep(delay);
                }
                catch (IOException ex)
                {
                    _logger.LogError(
                        ex,
                        "[{LogCode}] Non-transient IO error while writing attachment {AttachmentName} to {FilePath}.",
                        LogCodePathUnavailable,
                        attachment.Name,
                        resolvedPath.FilePath);

                    return Result.Failure<StoredAttachmentInfo>(DomainErrors.Storage.PathUnavailable);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "[{LogCode}] Unexpected error storing attachment {AttachmentName}.",
                        LogCodeUnhandled,
                        attachment.Name);

                    return Result.Failure<StoredAttachmentInfo>(Error.Failure);
                }
            }

            _logger.LogError(
                "[{LogCode}] Storage retry loop exhausted unexpectedly for attachment {AttachmentName}.",
                LogCodeUnhandled,
                attachment.Name);

            return Result.Failure<StoredAttachmentInfo>(Error.Failure);
        }

        private static Result<ResolvedStoragePath> TryResolveStoragePaths(
            string baseStorageFolder,
            string? companyStorageFolder,
            string? mailSubject,
            string attachmentName)
        {
            if (string.IsNullOrWhiteSpace(baseStorageFolder))
            {
                return Result.Failure<ResolvedStoragePath>(DomainErrors.Storage.InvalidBasePath);
            }

            string fullBasePath;
            try
            {
                fullBasePath = Path.GetFullPath(baseStorageFolder.Trim());
            }
            catch (Exception)
            {
                return Result.Failure<ResolvedStoragePath>(DomainErrors.Storage.InvalidBasePath);
            }

            if (string.IsNullOrWhiteSpace(fullBasePath))
            {
                return Result.Failure<ResolvedStoragePath>(DomainErrors.Storage.InvalidBasePath);
            }

            var safeCompanyFolder = BuildSafeRelativePath(companyStorageFolder);
            if (safeCompanyFolder is null)
            {
                return Result.Failure<ResolvedStoragePath>(DomainErrors.Storage.InvalidRelativePath);
            }

            var safeSubjectSegment = SanitizePathSegment(mailSubject);
            if (string.IsNullOrWhiteSpace(safeSubjectSegment))
            {
                safeSubjectSegment = "no_subject";
            }

            var storageDirectoryPath = Path.Combine(fullBasePath, safeCompanyFolder, safeSubjectSegment);

            string fullStorageDirectoryPath;
            try
            {
                fullStorageDirectoryPath = Path.GetFullPath(storageDirectoryPath);
            }
            catch (Exception)
            {
                return Result.Failure<ResolvedStoragePath>(DomainErrors.Storage.InvalidRelativePath);
            }

            if (!IsPathUnderBase(fullStorageDirectoryPath, fullBasePath))
            {
                return Result.Failure<ResolvedStoragePath>(DomainErrors.Storage.PathTraversalDetected);
            }

            var safeFileName = BuildSafeFileName(attachmentName);
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                return Result.Failure<ResolvedStoragePath>(DomainErrors.Storage.InvalidRelativePath);
            }

            var filePath = Path.Combine(fullStorageDirectoryPath, safeFileName);

            string fullFilePath;
            try
            {
                fullFilePath = Path.GetFullPath(filePath);
            }
            catch (Exception)
            {
                return Result.Failure<ResolvedStoragePath>(DomainErrors.Storage.InvalidRelativePath);
            }

            if (!IsPathUnderBase(fullFilePath, fullBasePath))
            {
                return Result.Failure<ResolvedStoragePath>(DomainErrors.Storage.PathTraversalDetected);
            }

            return Result.Success(new ResolvedStoragePath(fullStorageDirectoryPath, fullFilePath));
        }

        private static string? BuildSafeRelativePath(string? rawRelativePath)
        {
            if (string.IsNullOrWhiteSpace(rawRelativePath))
            {
                return string.Empty;
            }

            var segments = rawRelativePath
                .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(SanitizePathSegment)
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToList();

            if (segments.Count == 0)
            {
                return string.Empty;
            }

            return Path.Combine(segments.ToArray());
        }

        private static string SanitizePathSegment(string? rawSegment)
        {
            if (string.IsNullOrWhiteSpace(rawSegment))
            {
                return string.Empty;
            }

            var cleaned = rawSegment.Trim();
            if (cleaned is "." or "..")
            {
                return string.Empty;
            }

            var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
            var builder = new StringBuilder(cleaned.Length);

            foreach (var character in cleaned)
            {
                if (invalidChars.Contains(character) || char.IsControl(character))
                {
                    builder.Append('_');
                }
                else if (character is '/' or '\\')
                {
                    builder.Append('_');
                }
                else
                {
                    builder.Append(character);
                }
            }

            var normalized = builder.ToString().Replace("..", "_", StringComparison.Ordinal);
            normalized = normalized.Trim().Trim('.');
            return normalized;
        }

        private static string BuildSafeFileName(string attachmentName)
        {
            var extension = Path.GetExtension(attachmentName);
            extension = SanitizeExtension(extension);

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(attachmentName);
            fileNameWithoutExtension = SanitizePathSegment(fileNameWithoutExtension);

            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                fileNameWithoutExtension = "attachment";
            }

            if (fileNameWithoutExtension.Length > 45)
            {
                fileNameWithoutExtension = fileNameWithoutExtension[..45];
            }

            var suffix = Guid.NewGuid().ToString("N")[..6];
            return $"{fileNameWithoutExtension}_{suffix}{extension}";
        }

        private static string SanitizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return string.Empty;
            }

            var sanitized = extension
                .Trim()
                .TrimStart('.')
                .Where(character => char.IsLetterOrDigit(character))
                .ToArray();

            if (sanitized.Length == 0)
            {
                return string.Empty;
            }

            var safeExtension = new string(sanitized);
            if (safeExtension.Length > 12)
            {
                safeExtension = safeExtension[..12];
            }

            return $".{safeExtension}";
        }

        private static bool IsPathUnderBase(string candidatePath, string basePath)
        {
            var normalizedBase = EnsureTrailingSeparator(basePath);
            var normalizedCandidate = EnsureTrailingSeparator(candidatePath);

            return normalizedCandidate.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return normalized + Path.DirectorySeparatorChar;
        }

        private static bool IsTransientIoException(IOException exception)
        {
            var win32Code = exception.HResult & 0xFFFF;
            if (TransientWin32Codes.Contains(win32Code))
            {
                return true;
            }

            var message = exception.Message ?? string.Empty;
            return message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase);
        }

        private static TimeSpan ComputeRetryDelay(int attempt, int baseDelayMs, int maxDelayMs)
        {
            var exponentialDelay = baseDelayMs * Math.Pow(2, attempt - 1);
            var boundedDelay = Math.Min(exponentialDelay, maxDelayMs);
            return TimeSpan.FromMilliseconds(boundedDelay);
        }

        private sealed record ResolvedStoragePath(string StorageDirectoryPath, string FilePath);
    }
}
