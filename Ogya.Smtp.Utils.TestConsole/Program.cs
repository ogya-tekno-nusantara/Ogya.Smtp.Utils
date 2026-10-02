using System;

namespace Ogya.Smtp.Utils
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var service = new MailService();
            service.SmtpHost = "smtp.gmail.com";
            service.SmtpPort = 587;
            service.SmtpUsername = "[EMAIL_ADDRESS]";
            service.SmtpPassword = "[PASSWORD]";
            service.SmtpEnableSsl = true;
            service.SenderEmail = "noreplay@gmail.com";
            service.SenderName = "noreplay";
            // Menggunakan properti TemplateFilePath untuk membaca file template HTML
            service.TemplateFilePath = "EmailTemplate/EmailTemplate.html";
            
            // Mengganti placeholder dengan data asli
            service.AddParameter("{{Nama}}", "Samsul");
            service.AddParameter("{{Pesan}}", "Ini hanyalah test saja menggunakan HTML template.");

            // Konfigurasi tujuan dan subject menggunakan property baru
            service.To = new[] {"Samsul@quadras.co.id" };
            service.Subject = "Test Email Ogya.Smtp.Utils";

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
