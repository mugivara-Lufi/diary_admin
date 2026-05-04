using Diplom.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Diplom
{
    public partial class CurriculumSubjectsView : UserControl
    {
        private int _curriculumId;
        private string _curriculumName;
        private ObservableCollection<CurriculumSubjectViewModel> _subjects;

        public event Action GoBack;

        public CurriculumSubjectsView(int curriculumId, string curriculumName)
        {
            InitializeComponent();
            _curriculumId = curriculumId;
            _curriculumName = curriculumName;
            _subjects = new ObservableCollection<CurriculumSubjectViewModel>();
            SubjectsGrid.ItemsSource = _subjects;

            PlanNameText.Text = $"План: {curriculumName}";

            Loaded += async (s, e) => await LoadSubjectsAsync();
        }

        private async System.Threading.Tasks.Task LoadSubjectsAsync()
        {
            try
            {
                StatusText.Text = "Загрузка...";

                var result = await SupabaseClient.ExecuteQuery("curriculum_subjects",
                    $"curriculum_id=eq.{_curriculumId}&select=*,subjects(name)");

                _subjects.Clear();

                foreach (var item in result)
                {
                    var subject = new CurriculumSubjectViewModel
                    {
                        Id = item["id"].Value<int>(),
                        CurriculumId = item["curriculum_id"].Value<int>(),
                        SubjectId = item["subject_id"].Value<int>(),
                        SubjectName = item["subjects"]?["name"]?.ToString() ?? "Не указано",
                        Semester = item["semester"].Value<int>(),
                        HoursPerWeek = item["hours_per_week"].Value<int>(),
                        TotalHours = item["total_hours"].Value<int>(),
                        AttestationType = item["attestation_type"]?.ToString() == "exam" ? "Экзамен" : "Зачет"
                    };

                    // Загружаем преподавателей для этого предмета
                    var teachersResult = await SupabaseClient.ExecuteQuery("subject_teachers",
                        $"curriculum_subject_id=eq.{subject.Id}&select=*,teachers(full_name)");

                    var teacherNames = new List<string>();
                    foreach (var teacher in teachersResult)
                    {
                        teacherNames.Add(teacher["teachers"]?["full_name"]?.ToString() ?? "Не указан");
                    }
                    subject.TeacherNames = string.Join(", ", teacherNames);

                    _subjects.Add(subject);
                }

                CountText.Text = $"{_subjects.Count} предметов";
                StatusText.Text = "Готово";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
                StatusText.Text = "Ошибка загрузки";
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            GoBack?.Invoke();
        }

        private async void AddSubjectButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CurriculumSubjectEditView(_curriculumId);
            dialog.SubjectSaved += async () => await LoadSubjectsAsync();

            var window = new Window
            {
                Content = dialog,
                Width = 500,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false
            };
            window.ShowDialog();
        }

        private async void EditSubjectButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is CurriculumSubjectViewModel subject)
            {
                var dialog = new CurriculumSubjectEditView(_curriculumId, subject);
                dialog.SubjectSaved += async () => await LoadSubjectsAsync();

                var window = new Window
                {
                    Title = "Редактирование предмета",
                    Content = dialog,
                    Width = 500,
                    Height = 450,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false
                };
                window.ShowDialog();
            }
        }

        private async void DeleteSubjectButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is CurriculumSubjectViewModel subject)
            {
                var result = MessageBox.Show($"Удалить предмет '{subject.SubjectName}' из учебного плана?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await SupabaseClient.DeleteCurriculumSubject(subject.Id);
                    await LoadSubjectsAsync();
                }
            }
        }

        private async void ManageTeachersButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is CurriculumSubjectViewModel subject)
            {
                var dialog = new SubjectTeachersView(subject.Id, subject.SubjectName);

                var window = new Window
                {
                    Title = $"Преподаватели - {subject.SubjectName}",
                    Content = dialog,
                    Width = 550,
                    Height = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false
                };
                window.ShowDialog();

                // Обновляем список преподавателей
                await LoadSubjectsAsync();
            }
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T typedParent)
                    return typedParent;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }

    public class CurriculumSubjectViewModel
    {
        public int Id { get; set; }
        public int CurriculumId { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public int Semester { get; set; }
        public int HoursPerWeek { get; set; }
        public int TotalHours { get; set; }
        public string AttestationType { get; set; }
        public string TeacherNames { get; set; }
    }
}