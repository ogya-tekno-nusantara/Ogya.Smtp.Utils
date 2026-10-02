using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Ogya.Smtp.Utils
{
    public class MailService
    {
        public string? SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public string? SmtpUsername { get; set; }
        public string? SmtpPassword { get; set; }
        public bool SmtpEnableSsl { get; set; }
        public string? SenderEmail { get; set; }
        public string? SenderName { get; set; }
        public string? TemplateFilePath { get; set; }
        public string? BodyContent { get; set; }
        public bool IsBodyHtml { get; set; } = true;
        
        public string[] To { get; set; } = Array.Empty<string>();
        public string[] Cc { get; set; } = Array.Empty<string>();
        public string[] Bcc { get; set; } = Array.Empty<string>();
        public string? Subject { get; set; }

        private readonly Dictionary<string, string> _parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Menambahkan atau memperbarui parameter untuk me-replace string dalam template.
        /// Contoh: AddParameter("user", "sams") akan mereplace {{user}} dengan "sams".
        /// </summary>
        /// <param name="paramName">Nama parameter (tanpa kurung kurawal)</param>
        /// <param name="paramValue">Nilai yang akan disisipkan</param>
        public void AddParameter(string paramName, string paramValue)
        {
            if (string.IsNullOrWhiteSpace(paramName))
                throw new ArgumentException("Nama parameter tidak boleh kosong.", nameof(paramName));

            _parameters[paramName] = paramValue;
        }

        /// <summary>
        /// Menghapus semua parameter yang telah ditambahkan.
        /// </summary>
        public void ClearParameters()
        {
            _parameters.Clear();
        }

        /// <summary>
        /// Memproses template dengan mengganti semua parameter (format {{paramName}}).
        /// </summary>
        private string ProcessTemplate(string templateContent)
        {
            string body = templateContent;
            foreach (var param in _parameters)
            {
                string placeholder =  param.Key ;
                body = body.Replace(placeholder, param.Value ?? string.Empty);
            }
            return body;
        }

        /// <summary>
        /// Mempersiapkan instance MailMessage.
        /// </summary>
        private MailMessage PrepareMessage(string? toEmail, string? subject, string bodyContent)
        {
            if (string.IsNullOrWhiteSpace(SenderEmail))
                throw new InvalidOperationException("SenderEmail belum dikonfigurasi.");

            var finalSubject = !string.IsNullOrWhiteSpace(subject) ? subject : Subject;

            var message = new MailMessage
            {
                From = string.IsNullOrWhiteSpace(SenderName) 
                    ? new MailAddress(SenderEmail) 
                    : new MailAddress(SenderEmail, SenderName),
                Subject = finalSubject,
                Body = bodyContent,
                IsBodyHtml = IsBodyHtml
            };

            if (!string.IsNullOrWhiteSpace(toEmail))
                message.To.Add(toEmail);
            else {
                foreach (var addr in To.Where(a => !string.IsNullOrWhiteSpace(a)))
                    message.To.Add(addr);   
            }
            
            foreach (var addr in Cc.Where(a => !string.IsNullOrWhiteSpace(a)))
                message.CC.Add(addr);

            foreach (var addr in Bcc.Where(a => !string.IsNullOrWhiteSpace(a)))
                message.Bcc.Add(addr);

            if (message.To.Count == 0)
                throw new InvalidOperationException("Minimal satu alamat penerima harus diisi.");

            return message;
        }

        /// <summary>
        /// Mempersiapkan instance SmtpClient.
        /// </summary>
        private SmtpClient PrepareSmtpClient()
        {
            if (string.IsNullOrWhiteSpace(SmtpHost))
                throw new InvalidOperationException("SmtpHost belum dikonfigurasi.");

            var client = new SmtpClient(SmtpHost, SmtpPort)
            {
                EnableSsl = SmtpEnableSsl
            };

            if (!string.IsNullOrWhiteSpace(SmtpUsername) && !string.IsNullOrWhiteSpace(SmtpPassword))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(SmtpUsername, SmtpPassword);
            }
            else
            {
                client.UseDefaultCredentials = true;
            }

            return client;
        }

        /// <summary>
        /// Mengirim email secara sinkron (Synchronous) menggunakan parameter To dan Subject dari property class.
        /// </summary>
        public void SendEmail()
        {
            SendEmailInternal(null, null);
        }

        /// <summary>
        /// Mengirim email secara sinkron (Synchronous).
        /// </summary>
        /// <param name="toEmail">Alamat email penerima</param>
        /// <param name="subject">Subjek email</param>
        public void SendEmail(string toEmail, string subject)
        {
            SendEmailInternal(toEmail, subject);
        }

        private void SendEmailInternal(string? toEmail, string? subject)
        {
            string rawContent = string.Empty;

            if (!string.IsNullOrWhiteSpace(TemplateFilePath) && File.Exists(TemplateFilePath))
            {
                rawContent = File.ReadAllText(TemplateFilePath);
            }
            else if (!string.IsNullOrWhiteSpace(BodyContent))
            {
                rawContent = BodyContent ?? string.Empty;
            }
            else
            {
                throw new InvalidOperationException("TemplateFilePath tidak ditemukan dan BodyContent kosong.");
            }

            string processedBody = ProcessTemplate(rawContent);

            using var message = PrepareMessage(toEmail, subject, processedBody);
            using var client = PrepareSmtpClient();
            
            client.Send(message);
        }
        
        /// <summary>
        /// Mengirim email secara asinkron (Asynchronous) menggunakan parameter To dan Subject dari property class.
        /// </summary>
        public async Task SendEmailAsync()
        {
            await SendEmailAsyncInternal(null, null).ConfigureAwait(false);
        }

        /// <summary>
        /// Mengirim email secara asinkron (Asynchronous).
        /// </summary>
        /// <param name="toEmail">Alamat email penerima</param>
        /// <param name="subject">Subjek email</param>
        public async Task SendEmailAsync(string toEmail, string subject)
        {
            await SendEmailAsyncInternal(toEmail, subject).ConfigureAwait(false);
        }

        private async Task SendEmailAsyncInternal(string? toEmail, string? subject)
        {
            string rawContent = string.Empty;

            if (!string.IsNullOrWhiteSpace(TemplateFilePath) && File.Exists(TemplateFilePath))
            {
                using (var reader = new StreamReader(TemplateFilePath))
                {
                    rawContent = await reader.ReadToEndAsync().ConfigureAwait(false) ?? string.Empty;
                }
            }
            else if (!string.IsNullOrWhiteSpace(BodyContent))
            {
                rawContent = BodyContent ?? string.Empty;
            }
            else
            {
                throw new InvalidOperationException("TemplateFilePath tidak ditemukan dan BodyContent kosong.");
            }
            
            string processedBody = ProcessTemplate(rawContent);

            using var message = PrepareMessage(toEmail, subject, processedBody);
            using var client = PrepareSmtpClient();
            
            await client.SendMailAsync(message).ConfigureAwait(false);
        }
    }
}
