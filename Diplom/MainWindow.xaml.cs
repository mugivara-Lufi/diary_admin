using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Diplom.Models;
using Supabase;
using static Diplom.SupabaseClient;

namespace Diplom
{
    public partial class MainWindow : Window
    { 
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox != null)
            {
                var placeholder = passwordBox.Template.FindName("PlaceholderText", passwordBox) as System.Windows.Controls.TextBlock;
                if (placeholder != null)
                {
                    placeholder.Visibility = string.IsNullOrEmpty(passwordBox.Password)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // Устанавливаем фокус на поле логина при загрузке
            Loaded += (s, e) => LoginTextBox.Focus();

            // Обработка нажатия Enter для авторизации
            LoginTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                    LoginButton_Click(s, e);
            };

            PasswordBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                    LoginButton_Click(s, e);
            };
        }


        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginTextBox.Text.Trim();
            string password = PasswordBox.Password;

            // Валидация
            if (string.IsNullOrEmpty(login))
            {
                ShowError("Введите логин");
                LoginTextBox.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Введите пароль");
                PasswordBox.Focus();
                return;
            }

            // Блокируем UI на время авторизации
            SetLoadingState(true);

            try
            {
                // Выполняем авторизацию
                var user = await SupabaseClient.Login(login, password);

                if (user != null)
                {
                    // Проверяем роль пользователя
                    string role = user["role"]?.ToString();

                    if (role == "admin")
                    {
                        // Сохраняем данные пользователя
                        AuthService.CurrentUser = user;

                        // Открываем главное окно администратора
                        OpenAdminDashboard();
                    }
                    else
                    {
                        ShowError("Недостаточно прав. Требуется роль администратора.");
                    }
                }
                else
                {
                    ShowError("Неверный логин или пароль");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка авторизации: {ex.Message}");
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private void SetLoadingState(bool isLoading)
        {
            LoginButton.IsEnabled = !isLoading;
            LoginTextBox.IsEnabled = !isLoading;
            PasswordBox.IsEnabled = !isLoading;

            if (isLoading)
            {
                LoginButton.Content = "ПОДКЛЮЧЕНИЕ...";
                LoginButton.Background = System.Windows.Media.Brushes.Gray;
            }
            else
            {
                LoginButton.Content = "ВОЙТИ";
                LoginButton.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(82, 113, 255));
            }
        }

        private void ShowError(string message)
        {
            // Можно добавить красивый вывод ошибок
            MessageBox.Show(message, "Ошибка авторизации",
                MessageBoxButton.OK, MessageBoxImage.Error);

            // Очищаем пароль и устанавливаем фокус
            PasswordBox.Password = "";
            PasswordBox.Focus();
        }

        private void OpenAdminDashboard()
        {
            // Создаем и показываем окно администратора
            var adminWindow = new AdminWindow();
            adminWindow.Show();

            // Закрываем окно авторизации
            this.Close();
        }

        // Обработчик для "Забыли пароль?"
        private void ForgotPassword_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MessageBox.Show("Функция восстановления пароля временно недоступна.\nОбратитесь к системному администратору.",
                "Восстановление пароля",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
