using System;
using System.Windows;
using System.Windows.Controls;
using static Diplom.SupabaseClient;

namespace Diplom
{
    public partial class TeacherWindow : Window
    {
        public TeacherWindow()
        {
            InitializeComponent();
            UpdateDate();
            LoadTeacherInfo();
            ShowDashboard();
        }

        private void UpdateDate()
        {
            CurrentDateText.Text = DateTime.Now.ToString("dd MMMM yyyy");
        }

        private void LoadTeacherInfo()
        {
            if (AuthService.CurrentTeacher != null)
            {
                TeacherNameText.Text = AuthService.CurrentTeacher.FullName ?? "Преподаватель";
                TeacherSubjectText.Text = AuthService.CurrentTeacher.SubjectName ?? "Дисциплина не указана";
            }
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
        public void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDashboard();
        }

        public void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSchedule();
        }

        public void GradesButton_Click(object sender, RoutedEventArgs e)
        {
            ShowGrades();
        }

        public void AttendanceButton_Click(object sender, RoutedEventArgs e)
        {
            ShowAttendance();
        }

        public void HomeworkButton_Click(object sender, RoutedEventArgs e)
        {
            ShowHomework();
        }

        private void ShowDashboard()
        {
            // Временная заглушка для дашборда преподавателя
            MainContent.Content = new TeacherDashboardView();
            ContentTitleText.Text = "Дашборд преподавателя";
            ContentSubtitleText.Text = "Обзор учебного процесса и статистика";
            UpdateNavigationStyle("Dashboard");
        }

        private void ShowSchedule()
        {
            MainContent.Content = new TeacherScheduleView();
            ContentTitleText.Text = "Расписание занятий";
            ContentSubtitleText.Text = "Просмотр расписания ваших занятий";
            UpdateNavigationStyle("Schedule");
        }

        private void ShowGrades()
        {
            MainContent.Content = new TeacherGradesView();
            ContentTitleText.Text = "Управление оценками";
            ContentSubtitleText.Text = "Выставление и редактирование оценок";
            UpdateNavigationStyle("Grades");
        }

        private void ShowAttendance()
        {
            MainContent.Content = new TeacherAttendanceView();
            ContentTitleText.Text = "Учет посещаемости";
            ContentSubtitleText.Text = "Отметка посещаемости студентов";
            UpdateNavigationStyle("Attendance");
        }

        private void ShowHomework()
        {
            MainContent.Content = new TeacherHomeworkView();
            ContentTitleText.Text = "Домашние задания";
            ContentSubtitleText.Text = "Создание и просмотр домашних заданий";
            UpdateNavigationStyle("Homework");
        }

        private void UpdateNavigationStyle(string activeButton)
        {
            var buttons = new[] { "Dashboard", "Schedule", "Grades", "Attendance", "Homework" };

            foreach (var buttonName in buttons)
            {
                if (FindName(buttonName + "Button") is Button button)
                {
                    button.Style = (Style)FindResource("NavButtonStyle");
                }
            }

            if (FindName(activeButton + "Button") is Button activeBtn)
            {
                activeBtn.Style = (Style)FindResource("NavButtonActiveStyle");
            }
        }
    }
}