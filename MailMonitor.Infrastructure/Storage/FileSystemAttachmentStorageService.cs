using MailMonitor.Application.Abstractions.Storage;
using MailMonitor.Domain.Abstractions;
using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace MailMonitor.Infrastructure.Storage
{
    /// <summary>
    /// Implementación que guarda adjuntos en el sistema de archivos local.
    /// </summary>
    internal sealed class FileSystemAttachmentStorageService : IAttachmentStorageService
    {
        private readonly ILogger<FileSystemAttachmentStorageService> _logger;

        public FileSystemAttachmentStorageService(ILogger<FileSystemAttachmentStorageService> logger)
        {
            _logger = logger;
        }

        public Result<StoredAttachmentInfo> StoreFile(Company company, string mailSubject, FileAttachment attachment, Setting settings)
        {
            if (attachment is null || string.IsNullOrWhiteSpace(attachment.Name) || attachment.ContentBytes is null)
            {
                return Result.Failure<StoredAttachmentInfo>(Error.NullValue);
            }

            var basePath = settings.BaseStorageFolder;

            if (string.IsNullOrWhiteSpace(basePath))
            {
                return Result.Failure<StoredAttachmentInfo>(Error.EmptyValue);
            }

            var companyStorageFolder = company.StorageFolder?.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? string.Empty;

            // Sanitizar asunto
            var sanitizedSubject = Regex.Replace(mailSubject, @"[<>:""/\\|?*]", "_");
            sanitizedSubject = sanitizedSubject.Replace("..", "_");

            var storagePath = Path.Combine(basePath, companyStorageFolder, sanitizedSubject).Trim();

            Directory.CreateDirectory(storagePath);

            // Nombre único para evitar colisiones
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(attachment.Name);
            var extension = Path.GetExtension(attachment.Name);

            if (!string.IsNullOrEmpty(fileNameWithoutExt) && fileNameWithoutExt.Length > 45)
            {
                fileNameWithoutExt = fileNameWithoutExt[..45];
            }

            var suffix = Guid.NewGuid().ToString("N")[..4];
            var safeName = string.IsNullOrEmpty(fileNameWithoutExt)
                ? $"attachment_{suffix}{extension}"
                : $"{fileNameWithoutExt}_{suffix}{extension}";

            var filePath = Path.Combine(storagePath, safeName);

            try
            {
                File.WriteAllBytes(filePath, attachment.ContentBytes);

                _logger.LogInformation(
                    "Stored attachment '{AttachmentName}' for user '{UserMail}' in company '{CompanyName}' at path: {FilePath}",
                    attachment.Name, company.Mail, company.Name, filePath);

                var storedInfo = new StoredAttachmentInfo(storagePath, filePath);

                return Result.Success(storedInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error storing attachment '{AttachmentName}' for company '{CompanyName}'",
                    attachment.Name, company.Name);
                return Result.Failure<StoredAttachmentInfo>(Error.Failure);
            }
        }
    }
}
