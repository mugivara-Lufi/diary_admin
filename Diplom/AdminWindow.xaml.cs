using Supabase.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static Diplom.SupabaseClient;

namespace Diplom
{
    /// <summary>
    /// Логика взаимодействия для AdminWindow.xaml
    /// </summary>
    public partial class AdminWindow : Window
    {
        public AdminWindow()
        {
            InitializeComponent();
            LoadUserInfo();
            UpdateDate();
            ShowDashboard(); // Показываем дашборд по умолчанию
        }

        private void LoadUserInfo()
        {
            if (AuthService.CurrentUser != null)
            {
                string login = AuthService.CurrentUser["login"]?.ToString();
                AdminNameText.Text = login ?? "Администратор";
            }
        }

        private void UpdateDate()
        {
            CurrentDateText.Text = DateTime.Now.ToString("dd MMMM yyyy");
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите выйти?",
                "Подтверждение выхода",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                AuthService.Logout();

                var loginWindow = new MainWindow();
                loginWindow.Show();
                this.Close();
            }
        }

        // Обработчики навигации
        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDashboard();
        }

        private void StudentsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowStudents();
        }

        private void TeachersButton_Click(object sender, RoutedEventArgs e)
        {
            ShowTeachers();
        }

        private void ClassesButton_Click(object sender, RoutedEventArgs e)
        {
            ShowClasses();
        }

        private void SubjectsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSubjects();
        }

        private void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSchedule();
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowReports();
        }

        private void ShowDashboard()
        {
            var dashboard = new DaxhBoardView();
            dashboard.NavigateTo += OnNavigateTo;
            MainContent.Content = dashboard;
            ContentTitleText.Text = "Дашборд";
            ContentSubtitleText.Text = "Обзор системы электронного дневника";
            UpdateNavigationStyle("Dashboard");
        }

        private void ShowStudents()
        {
            MainContent.Content = new StudentsView();
            ContentTitleText.Text = "Управление учениками";
            ContentSubtitleText.Text = "Добавление, редактирование и удаление учеников";
            UpdateNavigationStyle("Students");
        }

        private void ShowTeachers()
        {
            MainContent.Content = new TeachersView();
            ContentTitleText.Text = "Управление учителями";
            ContentSubtitleText.Text = "Добавление, редактирование и удаление учителей";
            UpdateNavigationStyle("Teachers");
        }

        private void ShowClasses()
        {
            // Заглушка для классов
            MainContent.Content = new ClassesView();
            ContentTitleText.Text = "Управление классами";
            ContentSubtitleText.Text = "Добавление, редактирование и удаление классов";
            UpdateNavigationStyle("Classes");
        }

        private void ShowSubjects()
        {
            // Заглушка для предметов
            MainContent.Content = new SubjectsView();
            ContentTitleText.Text = "Управление предметами";
            ContentSubtitleText.Text = "Добавление, редактирование и удаление предметов";
            UpdateNavigationStyle("Subjects");
        }

        private void ShowSchedule()
        {
            // Заглушка для расписания
            MainContent.Content = new ScheduleView();
            ContentTitleText.Text = "Управление расписанием";
            ContentSubtitleText.Text = "Составление и редактирование расписания";
            UpdateNavigationStyle("Schedule");
        }

        private void ShowReports()
        {
            // Заглушка для расписания
            MainContent.Content = new ReportsView();
            ContentTitleText.Text = "Управление отчетами";
            ContentSubtitleText.Text = "Составление и редактирование отчетов";
            UpdateNavigationStyle("Reports");
        }

        private void OnNavigateTo(string page)
        {
            switch (page)
            {
                case "Students":
                    ShowStudents();
                    break;
                case "Teachers":
                    ShowTeachers();
                    break;
                case "Classes":
                    ShowClasses();
                    break;
            }
        }

        private UIElement CreatePlaceholder(string emoji, string description)
        {
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(40),
                Margin = new Thickness(20),
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = emoji,
                            FontSize = 48,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0,0,0,15)
                        },
                        new TextBlock
                        {
                            Text = "Раздел в разработке",
                            FontSize = 20,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = Brushes.DarkGray,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0,0,0,10)
                        },
                        new TextBlock
                        {
                            Text = description,
                            FontSize = 14,
                            Foreground = Brushes.Gray,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                }
            };
        }

        private void UpdateNavigationStyle(string activeButton)
        {
            // Сброс стилей всех кнопок
            var buttons = new[] { "Dashboard", "Students", "Teachers", "Classes", "Subjects", "Schedule", "Reports" };

            foreach (var buttonName in buttons)
            {
                if (FindName(buttonName + "Button") is Button button)
                {
                    button.Style = (Style)FindResource("NavButtonStyle");
                }
            }

            // Установка активного стиля
            if (FindName(activeButton + "Button") is Button activeBtn)
            {
                activeBtn.Style = (Style)FindResource("NavButtonActiveStyle");
            }
        }
    }

}
