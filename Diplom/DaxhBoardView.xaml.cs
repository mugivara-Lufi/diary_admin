using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Diplom
{
    /// <summary>
    /// Логика взаимодействия для DaxhBoardView.xaml
    /// </summary>
    public partial class DaxhBoardView : UserControl
    {
        public DaxhBoardView()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadStatisticsAsync();
        }
        public event Action<string> NavigateTo;

        private async System.Threading.Tasks.Task LoadStatisticsAsync()
        {
            try
            {
                // Загружаем основную статистику
                var studentsCount = await SupabaseClient.Count("students");
                var teachersCount = await SupabaseClient.Count("teachers");
                var classesCount = await SupabaseClient.Count("classes");
                var subjectsCount = await SupabaseClient.Count("subjects");

                StudentsCountText.Text = studentsCount.ToString();
                TeachersCountText.Text = teachersCount.ToString();
                ClassesCountText.Text = classesCount.ToString();
                SubjectsCountText.Text = subjectsCount.ToString();

                // Загружаем дополнительную статистику
                await LoadAdditionalStats();

                UpdateChangeTexts();
            }
            catch (Exception ex)
            {
                ShowErrorStats();
            }
        }

        private async System.Threading.Tasks.Task LoadAdditionalStats()
        {
            try
            {
                // Оценки за последнюю неделю
                var weekAgo = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
                var gradesCount = await SupabaseClient.Count("grades", $"date=gte.{weekAgo}");
                GradesCountText.Text = gradesCount.ToString();

                // Посещаемость за сегодня
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var totalAttendance = await SupabaseClient.Count("attendance", $"date=eq.{today}");
                var presentCount = await SupabaseClient.Count("attendance", $"date=eq.{today}&present=eq.true");

                var attendancePercent = totalAttendance > 0 ? (presentCount * 100 / totalAttendance) : 0;
                AttendanceText.Text = $"{attendancePercent}%";

                // Активные домашние задания
                var activeHomework = await SupabaseClient.Count("homework", $"deadline=gte.{today}");
                HomeworkCountText.Text = activeHomework.ToString();
            }
            catch (Exception ex)
            {
                // В случае ошибки устанавливаем значения по умолчанию
                GradesCountText.Text = "0";
                AttendanceText.Text = "0%";
                HomeworkCountText.Text = "0";
            }
        }

        private void UpdateChangeTexts()
        {
            // Здесь можно добавить логику для расчета изменений
            // Пока используем заглушки
            StudentsChangeText.Text = "Все студенты";
            TeachersChangeText.Text = "Все преподаватели";
            ClassesChangeText.Text = "Все группы";
            SubjectsChangeText.Text = "Все дисциплины";
            GradesChangeText.Text = "за неделю";
            AttendanceChangeText.Text = "на сегодня";
            HomeworkChangeText.Text = "активных";
        }

        private void ShowErrorStats()
        {
            StudentsCountText.Text = "0";
            TeachersCountText.Text = "0";
            ClassesCountText.Text = "0";
            SubjectsCountText.Text = "0";
            GradesCountText.Text = "0";
            AttendanceText.Text = "0%";
            HomeworkCountText.Text = "0";

            StudentsChangeText.Text = "Ошибка загрузки";
            TeachersChangeText.Text = "Ошибка загрузки";
            ClassesChangeText.Text = "Ошибка загрузки";
            SubjectsChangeText.Text = "Ошибка загрузки";
            GradesChangeText.Text = "Ошибка загрузки";
            AttendanceChangeText.Text = "Ошибка загрузки";
            HomeworkChangeText.Text = "Ошибка загрузки";
        }

        // Обработчики быстрых действий
        private void StudentsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo?.Invoke("Students");
        }

        private void TeachersButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo?.Invoke("Teachers");
        }

        private void ClassesButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo?.Invoke("Classes");
        }
    }
}
