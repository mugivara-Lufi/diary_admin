using Diplom.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Text.RegularExpressions;

namespace Diplom
{
    public partial class ParentEditView : UserControl
    {
        public Parent Parent { get; private set; }
        public event Action<Parent> ParentSaved;
        public event Action Canceled;

        public ParentEditView(Parent parent = null)
        {
            InitializeComponent();

            if (parent != null)
            {
                Parent = parent;
                LoadParentData();

                // Меняем заголовок окна при редактировании
                if (Application.Current.Windows.Count > 0)
                {
                    var owner = Application.Current.Windows[Application.Current.Windows.Count - 1];
                    if (owner is Window window)
                    {
                        window.Title = "Редактирование законного представителя";
                    }
                }
            }
            else
            {
                Parent = new Parent();
            }

            // Подписываемся на событие изменения текста для валидации
            EmailTextBox.TextChanged += EmailTextBox_TextChanged;
        }

        private void LoadParentData()
        {
            FullNameTextBox.Text = Parent.FullName;
            PhoneTextBox.Text = Parent.Phone;
            EmailTextBox.Text = Parent.Email;
        }

        private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string email = EmailTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                EmailTextBox.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(224, 224, 224));
                return;
            }

            bool isValidEmail = IsValidEmail(email);

            if (isValidEmail)
            {
                EmailTextBox.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(76, 175, 80)); // Зеленый
            }
            else
            {
                EmailTextBox.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(229, 62, 62)); // Красный
            }
        }

        private bool IsValidEmail(string email)
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

        private bool IsValidPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return true; // Телефон не обязателен

            string digits = new string(phone.Where(char.IsDigit).ToArray());
            return digits.Length >= 10 && digits.Length <= 12;
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(FullNameTextBox.Text))
            {
                MessageBox.Show("Введите ФИО законного представителя", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                FullNameTextBox.Focus();
                return false;
            }

            // Валидация email (если указан)
            if (!string.IsNullOrWhiteSpace(EmailTextBox.Text) && !IsValidEmail(EmailTextBox.Text))
            {
                MessageBox.Show("Введите корректный email адрес", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                EmailTextBox.Focus();
                return false;
            }

            // Валидация телефона (если указан)
            if (!string.IsNullOrWhiteSpace(PhoneTextBox.Text) && !IsValidPhone(PhoneTextBox.Text))
            {
                MessageBox.Show("Введите корректный номер телефона", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                PhoneTextBox.Focus();
                return false;
            }

            return true;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                if (Parent.Id == 0)
                {
                    // ✅ Генерируем случайный пароль
                    string generatedPassword = GenerateRandomPassword();

                    // ✅ Передаем пароль в метод
                    var (parentResult, userResult) = await SupabaseClient.AddParentWithUser(
                        FullNameTextBox.Text.Trim(),
                        PhoneTextBox.Text.Trim(),
                        EmailTextBox.Text.Trim(),
                        generatedPassword  // Передаем сгенерированный пароль
                    );

                    if (parentResult != null && parentResult.Count > 0)
                    {
                        Parent.Id = parentResult[0]["id"].Value<int>();
                        Parent.FullName = parentResult[0]["full_name"].Value<string>();
                        Parent.Phone = parentResult[0]["phone"]?.Value<string>();
                        Parent.Email = parentResult[0]["email"]?.Value<string>();

                        string login = userResult[0]["login"].Value<string>();
                        // generatedPassword уже содержит сгенерированный пароль

                        // Отправляем данные на email, если он указан
                        if (!string.IsNullOrEmpty(Parent.Email) && IsValidEmail(Parent.Email))
                        {
                            try
                            {
                                bool sent = await SupabaseClient.SendLoginCredentials(
                                    Parent.Email,
                                    login,
                                    generatedPassword,  // Используем сгенерированный пароль
                                    Parent.FullName,
                                    "parent"
                                );

                                if (sent)
                                {
                                    MessageBox.Show(
                                        $"Законный представитель успешно добавлен!\n\n" +
                                        $"Данные для входа отправлены на email: {Parent.Email}\n\n" +
                                        $"Логин: {login}\n" +
                                        $"Пароль: {generatedPassword}\n\n" +
                                        $"Если письмо не пришло, проверьте папку Спам.",
                                        "Успех",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                                }
                                else
                                {
                                    ShowCredentialsDialog(login, generatedPassword);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(
                                    $"Законный представитель успешно добавлен!\n\n" +
                                    $"НО: Не удалось отправить письмо: {ex.Message}\n\n" +
                                    $"Логин: {login}\n" +
                                    $"Пароль: {generatedPassword}\n\n" +
                                    $"Сообщите эти данные представителю вручную.",
                                    "Внимание",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            ShowCredentialsDialog(login, generatedPassword);
                        }
                    }
                }
                else
                {
                    // Обновление не требует изменения пароля
                    await SupabaseClient.UpdateParent(
                        Parent.Id,
                        FullNameTextBox.Text.Trim(),
                        PhoneTextBox.Text.Trim(),
                        EmailTextBox.Text.Trim()
                    );

                    Parent.FullName = FullNameTextBox.Text.Trim();
                    Parent.Phone = PhoneTextBox.Text.Trim();
                    Parent.Email = EmailTextBox.Text.Trim();

                    MessageBox.Show("Данные законного представителя успешно обновлены", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                ParentSaved?.Invoke(Parent);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowCredentialsDialog(string login, string password)
        {
            // Копируем данные в буфер обмена
            string credentials = $"Логин: {login}\nПароль: {password}";
            System.Windows.Clipboard.SetText(credentials);

            MessageBox.Show(
                $"✅ Законный представитель успешно добавлен!\n\n" +
                $"📋 ДАННЫЕ ДЛЯ ВХОДА:\n" +
                $"   Логин: {login}\n" +
                $"   Пароль: {password}\n\n" +
                $"📝 Данные скопированы в буфер обмена!\n" +
                $"Сообщите их представителю.\n\n" +
                $"✉️ Email не указан или указан неверно.\n" +
                $"Чтобы получать данные на почту, укажите корректный email.",
                "Успех",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private string GenerateRandomPassword(int length = 8)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Canceled?.Invoke();
        }
    }
}