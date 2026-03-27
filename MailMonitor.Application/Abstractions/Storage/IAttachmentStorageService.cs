using MailMonitor.Domain.Abstractions;
using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Settings;
using Microsoft.Graph.Models;

namespace MailMonitor.Application.Abstractions.Storage
{
    /// <summary>
    /// Define un servicio para almacenar archivos adjuntos provenientes de correos electrónicos.
    /// Implementaciones pueden guardar en disco local, nube, etc.
    /// </summary>
    public interface IAttachmentStorageService
    {
        /// <summary>
        /// Almacena un archivo adjunto en el sistema de almacenamiento configurado.
        /// </summary>
        /// <param name="company">La compañía asociada al correo.</param>
        /// <param name="mailSubject">El asunto del correo.</param>
        /// <param name="attachment">El archivo adjunto recibido desde Microsoft Graph.</param>
        /// <param name="settings">Configuración global de la aplicación.</param>
        /// <returns>Un <see cref="Result{StoredAttachmentInfo}"/> con la información del archivo almacenado o error.</returns>
        Result<StoredAttachmentInfo> StoreFile(Company company, string mailSubject, FileAttachment attachment, Setting settings);
    }
}
