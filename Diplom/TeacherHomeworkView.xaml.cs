using Diplom.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Diplom
{
    public partial class TeacherHomeworkView : UserControl
    {
        private List<HomeworkItem> _allHomework;
        private ObservableCollection<HomeworkItem> _homeworkList;
        private List<Class> _classes;
        private List<Subject> _subjects;
        private Class _selectedClass;
        private Subject _selectedSubject;

        public TeacherHomeworkView()
        {
            InitializeComponent();
            _allHomework = new List<HomeworkItem>();
            _homeworkList = new ObservableCollection<HomeworkItem>();
            HomeworkGrid.ItemsSource = _homeworkList;

            Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                StatusText.Text = "Загрузка данных...";
                await LoadFilters();

                if (_selectedClass != null && _selectedSubject != null)
                    await LoadHomeworkAsync();

                StatusText.Text = "Готово к работе";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Ошибка загрузки";
            }
        }

        private async Task LoadFilters()
        {
            var classesResult = await SupabaseClient.GetClassesWithTeachers();
            _classes = new List<Class>();
            foreach (var item in classesResult)
            {
                _classes.Add(new Class
                {
                    Id = item["id"].Value<int>(),
                    Name = item["name"]?.ToString() ?? "Без названия"
                });
            }
            ClassFilter.ItemsSource = _classes;

            var subjectsResult = await SupabaseClient.GetAllSubjects();
            _subjects = new List<Subject>();

            // Получаем текущего преподавателя
            var currentTeacher = SupabaseClient.AuthService.CurrentTeacher;

            foreach (var item in subjectsResult)
            {
                var subjectId = item["id"].Value<int>();

                // Если преподаватель авторизован и у него есть предмет, 
                // пропускаем все предметы, ID которых не совпадает с его SubjectId
                if (currentTeacher?.SubjectId != null && subjectId != currentTeacher.SubjectId)
                {
                    continue;
                }

                _subjects.Add(new Subject
                {
                    Id = subjectId,
                    Name = item["name"]?.ToString() ?? "Без названия"
                });
            }
            SubjectFilter.ItemsSource = _subjects;

            // Автоматически выбираем предмет, так как в списке остаётся только предмет преподавателя
            if (_subjects.Any())
            {
                SubjectFilter.SelectedIndex = 0;
                _selectedSubject = _subjects.First();
            }

            if (_classes.Any() && ClassFilter.SelectedItem == null)
                ClassFilter.SelectedIndex = 0;
        }

        private async void ClassFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedClass = ClassFilter.SelectedItem as Class;
            if (_selectedClass != null && _selectedSubject != null)
                await LoadHomeworkAsync();
        }

        private async void SubjectFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedSubject = SubjectFilter.SelectedItem as Subject;
            if (_selectedClass != null && _selectedSubject != null)
                await LoadHomeworkAsync();
        }

        private async Task LoadHomeworkAsync()
        {
            try
            {
                StatusText.Text = "Загрузка заданий...";
                HomeworkGrid.IsEnabled = false;

                // Загружаем домашние задания
                var homeworkResult = await SupabaseClient.ExecuteQuery("homework",
                    $"class_id=eq.{_selectedClass.Id}&subject_id=eq.{_selectedSubject.Id}" +
                    $"&select=*,subjects(name)&order=deadline.desc");

                // Загружаем статусы выполнения
                var statusResult = await SupabaseClient.ExecuteQuery("homework_status",
                    $"select=*");

                _allHomework.Clear();
                foreach (var item in homeworkResult)
                {
                    var hwId = item["id"].Value<int>();
                    var hwItem = new HomeworkItem
                    {
                        Id = hwId,
                        SubjectId = item["subject_id"]?.Value<int>() ?? 0,
                        ClassId = item["class_id"]?.Value<int>() ?? 0,
                        Task = item["task"]?.ToString() ?? "",
                        Deadline = item["deadline"]?.ToObject<DateTime>() ?? DateTime.MinValue,
                        PublishDate = item["publish_date"]?.ToObject<DateTime>() ?? DateTime.MinValue,
                        FileLink = item["file_link"]?.ToString(),
                        Comment = item["comment"]?.ToString(),
                        ClassName = _selectedClass?.Name ?? "",
                        SubjectName = _selectedSubject?.Name ?? ""
                    };

                    // Статистика
                    var completedCount = statusResult.Count(s =>
                        s["homework_id"]?.Value<int>() == hwId &&
                        s["status"]?.ToString() == "done");
                    var totalCount = statusResult.Count(s =>
                        s["homework_id"]?.Value<int>() == hwId);

                    hwItem.CompletedCount = completedCount;
                    hwItem.TotalCount = totalCount;

                    _allHomework.Add(hwItem);
                }

                UpdateGrid();
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки заданий: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Ошибка загрузки";
            }
            finally
            {
                HomeworkGrid.IsEnabled = true;
            }
        }

        private void UpdateGrid()
        {
            _homeworkList.Clear();
            foreach (var hw in _allHomework.OrderByDescending(h => h.Deadline))
                _homeworkList.Add(hw);
        }

        private void UpdateStatus()
        {
            HomeworkCountText.Text = $"{_homeworkList.Count} заданий";
            StatusText.Text = "Готово";
        }

        private void AddHomework_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClass == null || _selectedSubject == null)
            {
                MessageBox.Show("Выберите класс и предмет", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowEditView();
        }

        private void EditHomework_Click(object sender, RoutedEventArgs e)
        {
            var selected = HomeworkGrid.SelectedItem as HomeworkItem;
            if (selected == null)
            {
                MessageBox.Show("Выберите задание для редактирования", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowEditView(selected);
        }

        private void ShowEditView(HomeworkItem homework = null)
        {
            var parentContentControl = FindParentOfType<ContentControl>(this);
            if (parentContentControl == null)
            {
                MessageBox.Show("Ошибка навигации", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var currentView = parentContentControl.Content;

            var editView = new HomeworkEditView(_selectedClass, _selectedSubject, homework);
            editView.SaveCompleted += async (success) =>
            {
                if (success)
                {
                    parentContentControl.Content = currentView;
                    await LoadHomeworkAsync();
                }
            };

            editView.GoBack += () =>
            {
                parentContentControl.Content = currentView;
            };

            parentContentControl.Content = editView;
        }

        private static T FindParentOfType<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T result)
                    return result;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
        private async void DeleteHomework_Click(object sender, RoutedEventArgs e)
        {
            var selected = HomeworkGrid.SelectedItem as HomeworkItem;
            if (selected == null)
            {
                MessageBox.Show("Выберите задание для удаления", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Удалить задание?\n\n{selected.Task}",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await SupabaseClient.Delete("homework", $"id=eq.{selected.Id}");
                    await LoadHomeworkAsync();
                    StatusText.Text = "Задание удалено";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadHomeworkAsync();
        }

        private void HomeworkGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EditHomework_Click(sender, e);
        }

        private void HomeworkGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }

    /// <summary>
    /// Модель для отображения домашнего задания в таблице
    /// </summary>
    public class HomeworkItem
    {
        public int Id { get; set; }
        public int SubjectId { get; set; }
        public int ClassId { get; set; }
        public string Task { get; set; }
        public DateTime Deadline { get; set; }
        public DateTime PublishDate { get; set; }
        public string FileLink { get; set; }
        public string Comment { get; set; }
        public string ClassName { get; set; }
        public string SubjectName { get; set; }
        public int CompletedCount { get; set; }
        public int TotalCount { get; set; }
        public bool IsOverdue => Deadline.Date < DateTime.Now.Date;
    }
}