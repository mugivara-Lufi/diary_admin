using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;

namespace Diplom
{
    public partial class SubjectTeachersView : UserControl
    {
        private int _curriculumSubjectId;
        private string _subjectName;
        private ObservableCollection<TeacherViewModel> _teachers;
        private ObservableCollection<TeacherViewModel> _allTeachers;

        public SubjectTeachersView(int curriculumSubjectId, string subjectName)
        {
            InitializeComponent();
            _curriculumSubjectId = curriculumSubjectId;
            _subjectName = subjectName;
            _teachers = new ObservableCollection<TeacherViewModel>();
            _allTeachers = new ObservableCollection<TeacherViewModel>();

            SubjectNameText.Text = $"Дисциплина: {subjectName}";
            TeachersGrid.ItemsSource = _teachers;

            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            // 1. Загружаем назначенных преподавателей
            var assigned = await SupabaseClient.GetTeachersForCurriculumSubject(_curriculumSubjectId);
            _teachers.Clear();
            foreach (var item in assigned)
            {
                _teachers.Add(new TeacherViewModel
                {
                    Id = item["teacher_id"].Value<int>(),
                    FullName = item["teachers"]?["full_name"]?.ToString()
                });
            }

            // 2. Загружаем всех преподавателей
            var all = await SupabaseClient.GetTeachersWithSubjects();
            _allTeachers.Clear();
            foreach (var item in all)
            {
                _allTeachers.Add(new TeacherViewModel
                {
                    Id = item["id"].Value<int>(),
                    FullName = item["full_name"]?.ToString(),
                    SubjectName = item["subjects"]?["name"]?.ToString()
                });
            }

            // 3. Фильтруем преподавателей ПО ТЕКУЩЕЙ ДИСЦИПЛИНЕ И ИСКЛЮЧАЕМ УЖЕ НАЗНАЧЕННЫХ
            var availableTeachers = _allTeachers
                .Where(t => !_teachers.Any(ta => ta.Id == t.Id) &&
                           t.SubjectName == _subjectName)  // Только те, кто ведет эту дисциплину
                .ToList();

            TeacherComboBox.ItemsSource = availableTeachers;

            // Дополнительно: показываем сообщение, если нет доступных преподавателей
            if (!availableTeachers.Any() && _allTeachers.Any())
            {
                // Можно вывести предупреждение, но не блокируем интерфейс
                System.Diagnostics.Debug.WriteLine($"Нет доступных преподавателей для дисциплины: {_subjectName}");
            }
        }

        private async void AddTeacher_Click(object sender, RoutedEventArgs e)
        {
            if (TeacherComboBox.SelectedValue == null)
            {
                MessageBox.Show("Выберите преподавателя", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int teacherId = (int)TeacherComboBox.SelectedValue;

            // Дополнительная проверка: убеждаемся, что выбранный преподаватель действительно ведет эту дисциплину
            var selectedTeacher = _allTeachers.FirstOrDefault(t => t.Id == teacherId);
            if (selectedTeacher != null && selectedTeacher.SubjectName != _subjectName)
            {
                MessageBox.Show($"Преподаватель {selectedTeacher.FullName} не ведет дисциплину '{_subjectName}'",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await SupabaseClient.AssignTeacherToSubject(_curriculumSubjectId, teacherId);
            await LoadDataAsync(); // Перезагружаем данные

            // Сбрасываем выбор в комбобоксе
            TeacherComboBox.SelectedIndex = -1;
        }

        private async void RemoveTeacher_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is TeacherViewModel teacher)
            {
                var result = MessageBox.Show($"Удалить преподавателя '{teacher.FullName}' из дисциплины '{_subjectName}'?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await SupabaseClient.RemoveTeacherFromSubject(teacher.Id);
                    await LoadDataAsync();
                }
            }
        }
    }

    public class TeacherViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string SubjectName { get; set; }
    }
}