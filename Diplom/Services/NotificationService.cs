using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Diplom.Services
{
    public class NotificationService
    {


           // ✅ НАСТРОЙКИ ДЛЯ YANDEX (используйте ЭТИ параметры)
        private readonly string _smtpServer = "smtp.yandex.ru";
        private readonly int _smtpPort = 465;  // ИЛИ 587 - попробуйте оба
        private readonly string _senderEmail = "maxtrub1n@yandex.ru";  // ЗАМЕНИТЕ
        private readonly string _senderPassword = "sytdfjloehekzldi";  // 478a1e0b67f6... (без пробелов)
        private readonly string _senderName = "Электронный дневник";

        public async Task<bool> SendLoginCredentials(string contact, string login, string password, string fullName, string role)
        {
            Debug.WriteLine($"=== НАЧАЛО ОТПРАВКИ ===");
            Debug.WriteLine($"Контакт: {contact}");
            Debug.WriteLine($"Имя: {fullName}");
            Debug.WriteLine($"Роль: {role}");

            if (IsValidEmail(contact))
            {
                Debug.WriteLine($"Email валидный: {contact}");
                return await SendEmailAsync(contact, login, password, fullName, role);
            }
            else
            {
                Debug.WriteLine($"Email НЕ валидный: {contact}");
                throw new Exception("Укажите корректный email");
            }
        }

        private async Task<bool> SendEmailAsync(string email, string login, string userPassword, string fullName, string role)
        {
            try
            {
                Debug.WriteLine($"=== ОТПРАВКА ПИСЬМА ===");
                Debug.WriteLine($"SMTP сервер: {_smtpServer}:{_smtpPort}");
                Debug.WriteLine($"Отправитель: {_senderEmail}");
                Debug.WriteLine($"Получатель: {email}");
                Debug.WriteLine($"Тема: Данные для входа в систему - {fullName}");

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_senderName, _senderEmail));
                message.To.Add(new MailboxAddress(fullName, email));
                message.Subject = $"Данные для входа в систему - {fullName}";

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = GenerateEmailBody(login, userPassword, fullName, role),
                    TextBody = $"Логин: {login}\nПароль: {userPassword}"
                };
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    Debug.WriteLine("Подключаюсь к SMTP серверу...");
                    await client.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.SslOnConnect);
                    Debug.WriteLine("Подключено!");

                    Debug.WriteLine("Аутентификация...");
                    await client.AuthenticateAsync(_senderEmail, _senderPassword);
                    Debug.WriteLine("Аутентификация пройдена!");

                    Debug.WriteLine("Отправка письма...");
                    await client.SendAsync(message);
                    Debug.WriteLine("Письмо отправлено!");

                    await client.DisconnectAsync(true);
                    Debug.WriteLine("Отключено!");
                }

                Debug.WriteLine("=== ОТПРАВКА УСПЕШНО ЗАВЕРШЕНА ===");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"=== ОШИБКА ОТПРАВКИ ===");
                Debug.WriteLine($"Тип: {ex.GetType().Name}");
                Debug.WriteLine($"Сообщение: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Внутренняя ошибка: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        private string GenerateEmailBody(string login, string password, string fullName, string role)
        {
            string roleText = role == "student" ? "студента" : "пользователя";

            return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='UTF-8'>
                <style>
                    body {{ font-family: 'Segoe UI', Arial, sans-serif; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; background: #f8f9fa; }}
                    .header {{ background: #5271FF; color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0; }}
                    .content {{ background: white; padding: 30px; border-radius: 0 0 10px 10px; }}
                    .credentials {{ background: #f0f4ff; padding: 15px; border-left: 4px solid #5271FF; margin: 20px 0; }}
                    .login {{ color: #5271FF; font-weight: bold; font-size: 16px; }}
                    .password {{ color: #E53E3E; font-weight: bold; font-size: 16px; }}
                    .warning {{ background: #FFF3E0; padding: 10px; border-radius: 5px; margin: 15px 0; font-size: 12px; }}
                    .footer {{ text-align: center; margin-top: 20px; font-size: 11px; color: #999; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h2>📚 Электронный дневник</h2>
                    </div>
                    <div class='content'>
                        <h3>Здравствуйте, {fullName}!</h3>
                        <p>Для вас был создан аккаунт в системе <strong>Электронный дневник</strong> в качестве {roleText}.</p>
                        
                        <div class='credentials'>
                            <p><strong>🔑 Логин:</strong> <span class='login'>{login}</span></p>
                            <p><strong>🔐 Пароль:</strong> <span class='password'>{password}</span></p>
                        </div>
                        
                        <div class='warning'>
                            <strong>⚠️ Важно:</strong><br>
                            • Рекомендуем сменить пароль после первого входа<br>
                            • Никому не сообщайте свои данные для входа<br>
                            • При проблемах обратитесь к администратору
                        </div>
                        
                        <p style='text-align: center; margin-top: 20px;'>
                            <a href='#' style='background: #5271FF; color: white; padding: 10px 25px; text-decoration: none; border-radius: 5px; display: inline-block;'>Перейти в систему</a>
                        </p>
                    </div>
                    <div class='footer'>
                        <p>Это письмо создано автоматически, отвечать на него не нужно.</p>
                        <p>© 2024 Электронный дневник</p>
                    </div>
                </div>
            </body>
            </html>";
        }


        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsValidPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;
            string digits = new string(phone.Where(char.IsDigit).ToArray());
            return digits.Length >= 10 && digits.Length <= 12;
        }

        private string CleanPhoneNumber(string phone)
        {
            string digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 11 && digits.StartsWith("8"))
                digits = "7" + digits.Substring(1);
            if (digits.Length == 10)
                digits = "7" + digits;
            return digits;
        }
    }
}