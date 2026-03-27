using Diplom.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Windows;
using System.Windows.Controls;

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
            }
            else
            {
                Parent = new Parent();
            }
        }

        private void LoadParentData()
        {
            FullNameTextBox.Text = Parent.FullName;
            PhoneTextBox.Text = Parent.Phone;
            EmailTextBox.Text = Parent.Email;
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(FullNameTextBox.Text))
            {
                MessageBox.Show("Введите ФИО родителя", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                FullNameTextBox.Focus();
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
                    // Создаем родителя с пользователем
                    var (parentResult, userResult) = await SupabaseClient.AddParentWithUser(
                        FullNameTextBox.Text.Trim(),
                        PhoneTextBox.Text.Trim(),
                        EmailTextBox.Text.Trim()
                    );

                    if (parentResult != null && parentResult.Count > 0)
                    {
                        Parent.Id = parentResult[0]["id"].Value<int>();
                        Parent.FullName = parentResult[0]["full_name"].Value<string>();
                        Parent.Phone = parentResult[0]["phone"]?.Value<string>();
                        Parent.Email = parentResult[0]["email"]?.Value<string>();

                        string login = userResult[0]["login"].Value<string>();

                        MessageBox.Show(
                            $"Родитель успешно добавлен!\n\n" +
                            $"Данные для входа:\n" +
                            $"Логин: {login}\n" +
                            $"Пароль: password123\n\n" +
                            $"Сообщите эти данные родителю для входа в систему.",
                            "Успех",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                else
                {
                    // Обновляем существующего родителя
                    await SupabaseClient.UpdateParent(
                        Parent.Id,
                        FullNameTextBox.Text.Trim(),
                        PhoneTextBox.Text.Trim(),
                        EmailTextBox.Text.Trim()
                    );

                    Parent.FullName = FullNameTextBox.Text.Trim();
                    Parent.Phone = PhoneTextBox.Text.Trim();
                    Parent.Email = EmailTextBox.Text.Trim();
                }

                ParentSaved?.Invoke(Parent);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Canceled?.Invoke();
        }
    }
}