using System;
using Microsoft.Extensions.Configuration;

namespace Ogya.Smtp.Utils
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Load konfigurasi dari appsettings.json
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            var service = new MailService();

            // SMTP Settings
            service.SmtpHost     = config["SmtpSettings:Host"];
            service.SmtpPort     = int.Parse(config["SmtpSettings:Port"] ?? "587");
            service.SmtpUsername = config["SmtpSettings:Username"];
            service.SmtpPassword = config["SmtpSettings:Password"];
            // SecureSocketOptions: Auto (default), SslOnConnect (port 465), StartTls (port 587), None (port 25)
            var secureOption = config["SmtpSettings:SecureSocketOptions"] ?? "Auto";
            service.SecureSocketOptions = Enum.Parse<MailKit.Security.SecureSocketOptions>(secureOption, ignoreCase: true);
            service.SenderEmail  = config["SmtpSettings:SenderEmail"];
            service.SenderName   = config["SmtpSettings:SenderName"];

            // Email Settings
            service.TemplateFilePath = config["EmailSettings:TemplatePath"];
            service.Subject          = config["EmailSettings:Subject"];

            // Penerima (To, Cc, Bcc) — bisa diisi lebih dari satu di appsettings.json
            service.To  = config.GetSection("EmailSettings:To").Get<string[]>()  ?? Array.Empty<string>();
            service.Cc  = config.GetSection("EmailSettings:Cc").Get<string[]>()  ?? Array.Empty<string>();
            service.Bcc = config.GetSection("EmailSettings:Bcc").Get<string[]>() ?? Array.Empty<string>();

            // Parameters untuk template
            var parameters = config.GetSection("EmailSettings:Parameters").GetChildren();
            foreach (var param in parameters)
            {
                service.AddParameter(param.Key, param.Value ?? string.Empty);
            }

            try
            {
                Console.WriteLine("Mencoba mengirim email...");
                service.SendEmail();
                Console.WriteLine("Email berhasil dikirim!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Gagal mengirim email:");
                Console.WriteLine(ex.Message);
            }

            Console.WriteLine("Tekan sembarang tombol untuk keluar...");
            Console.ReadKey();
        }
    }
}
