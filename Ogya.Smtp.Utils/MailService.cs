using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Ogya.Smtp.Utils
{
    public class MailService
    {
        public string? SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public string? SmtpUsername { get; set; }
        public string? SmtpPassword { get; set; }

        /// <summary>
        /// Mode SSL/TLS yang digunakan saat koneksi ke SMTP server.
        /// - Auto       : Dipilih otomatis berdasarkan port (default, direkomendasikan)
        /// - SslOnConnect: Implicit SSL — untuk port 465
        /// - StartTls   : Explicit TLS — untuk port 587
        /// - None       : Tanpa enkripsi — untuk port 25
        /// </summary>
        public SecureSocketOptions SecureSocketOptions { get; set; } = SecureSocketOptions.Auto;

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
        /// Contoh: AddParameter("{{user}}", "sams") akan mereplace {{user}} dengan "sams".
        /// </summary>
        /// <param name="paramName">Nama parameter (termasuk kurung kurawal jika diinginkan)</param>
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
                body = body.Replace(param.Key, param.Value ?? string.Empty);
            }
            return body;
        }

        /// <summary>
        /// Membangun instance MimeMessage.
        /// </summary>
        private MimeMessage BuildMimeMessage(string? toEmail, string? subject, string bodyContent)
        {
            if (string.IsNullOrWhiteSpace(SenderEmail))
                throw new InvalidOperationException("SenderEmail belum dikonfigurasi.");

            var finalSubject = !string.IsNullOrWhiteSpace(subject) ? subject : Subject;

            var message = new MimeMessage();

            message.From.Add(string.IsNullOrWhiteSpace(SenderName)
                ? new MailboxAddress(SenderEmail, SenderEmail)
                : new MailboxAddress(SenderName, SenderEmail));

            message.Subject = finalSubject;

            var bodyBuilder = new BodyBuilder();
            if (IsBodyHtml)
                bodyBuilder.HtmlBody = bodyContent;
            else
                bodyBuilder.TextBody = bodyContent;

            message.Body = bodyBuilder.ToMessageBody();

            // Penerima To
            if (!string.IsNullOrWhiteSpace(toEmail))
                message.To.Add(MailboxAddress.Parse(toEmail));
            else
            {
                foreach (var addr in To.Where(a => !string.IsNullOrWhiteSpace(a)))
                    message.To.Add(MailboxAddress.Parse(addr));
            }

            // Penerima Cc
            foreach (var addr in Cc.Where(a => !string.IsNullOrWhiteSpace(a)))
                message.Cc.Add(MailboxAddress.Parse(addr));

            // Penerima Bcc
            foreach (var addr in Bcc.Where(a => !string.IsNullOrWhiteSpace(a)))
                message.Bcc.Add(MailboxAddress.Parse(addr));

            if (message.To.Count == 0)
                throw new InvalidOperationException("Minimal satu alamat penerima harus diisi.");

            return message;
        }

        private void ValidateSmtpConfig()
        {
            if (string.IsNullOrWhiteSpace(SmtpHost))
                throw new InvalidOperationException("SmtpHost belum dikonfigurasi.");

            if (string.IsNullOrWhiteSpace(SmtpUsername))
                throw new InvalidOperationException("SmtpUsername belum dikonfigurasi. SMTP memerlukan autentikasi.");

            if (string.IsNullOrWhiteSpace(SmtpPassword))
                throw new InvalidOperationException("SmtpPassword belum dikonfigurasi. SMTP memerlukan autentikasi.");
        }

        private string GetRawContent()
        {
            if (!string.IsNullOrWhiteSpace(TemplateFilePath) && File.Exists(TemplateFilePath))
                return File.ReadAllText(TemplateFilePath);

            if (!string.IsNullOrWhiteSpace(BodyContent))
                return BodyContent!;

            throw new InvalidOperationException("TemplateFilePath tidak ditemukan dan BodyContent kosong.");
        }

        private async Task<string> GetRawContentAsync()
        {
            if (!string.IsNullOrWhiteSpace(TemplateFilePath) && File.Exists(TemplateFilePath))
            {
                using var reader = new StreamReader(TemplateFilePath);
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(BodyContent))
                return BodyContent!;

            throw new InvalidOperationException("TemplateFilePath tidak ditemukan dan BodyContent kosong.");
        }

        /// <summary>
        /// Mengirim email secara sinkron menggunakan parameter To dan Subject dari property class.
        /// </summary>
        public void SendEmail()
        {
            SendEmailInternal(null, null);
        }

        /// <summary>
        /// Mengirim email secara sinkron.
        /// </summary>
        /// <param name="toEmail">Alamat email penerima</param>
        /// <param name="subject">Subjek email</param>
        public void SendEmail(string toEmail, string subject)
        {
            SendEmailInternal(toEmail, subject);
        }

        private void SendEmailInternal(string? toEmail, string? subject)
        {
            ValidateSmtpConfig();

            string processedBody = ProcessTemplate(GetRawContent());
            var message = BuildMimeMessage(toEmail, subject, processedBody);

            using var client = new SmtpClient();
            // SecureSocketOptions.Auto otomatis memilih Implicit SSL (port 465)
            // atau STARTTLS (port 587) sesuai port yang dikonfigurasi
            client.Connect(SmtpHost, SmtpPort, SecureSocketOptions);
            client.Authenticate(SmtpUsername, SmtpPassword);
            client.Send(message);
            client.Disconnect(true);
        }

        /// <summary>
        /// Mengirim email secara asinkron menggunakan parameter To dan Subject dari property class.
        /// </summary>
        public async Task SendEmailAsync()
        {
            await SendEmailAsyncInternal(null, null).ConfigureAwait(false);
        }

        /// <summary>
        /// Mengirim email secara asinkron.
        /// </summary>
        /// <param name="toEmail">Alamat email penerima</param>
        /// <param name="subject">Subjek email</param>
        public async Task SendEmailAsync(string toEmail, string subject)
        {
            await SendEmailAsyncInternal(toEmail, subject).ConfigureAwait(false);
        }

        private async Task SendEmailAsyncInternal(string? toEmail, string? subject)
        {
            ValidateSmtpConfig();

            string processedBody = ProcessTemplate(await GetRawContentAsync().ConfigureAwait(false));
            var message = BuildMimeMessage(toEmail, subject, processedBody);

            using var client = new SmtpClient();
            await client.ConnectAsync(SmtpHost, SmtpPort, SecureSocketOptions).ConfigureAwait(false);
            await client.AuthenticateAsync(SmtpUsername, SmtpPassword).ConfigureAwait(false);
            await client.SendAsync(message).ConfigureAwait(false);
            await client.DisconnectAsync(true).ConfigureAwait(false);
        }
    }
}
