using Diplom.Models;
using Newtonsoft.Json.Linq;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Diplom
{
    /// <summary>
    /// Логика взаимодействия для TeachersView.xaml
    /// </summary>
    public partial class TeachersView : UserControl
    {
        private ObservableCollection<Teacher> _teachers;
        private List<Teacher> _allTeachers;
        private DateTime _lastUpdate;

        public TeachersView()
        {
            InitializeComponent();
            _teachers = new ObservableCollection<Teacher>();
            _allTeachers = new List<Teacher>();
            TeachersGrid.ItemsSource = _teachers;

            Loaded += async (s, e) => await LoadTeachersAsync();
        }

        private async System.Threading.Tasks.Task LoadTeachersAsync()
        {
            try
            {
                StatusText.Text = "Загрузка данных...";
                TeachersGrid.IsEnabled = false;

                var result = await SupabaseClient.GetTeachersWithSubjects();

                _allTeachers.Clear();
                _teachers.Clear();

                foreach (var item in result)
                {
                    var teacher = new Teacher
                    {
                        Id = item["id"].Value<int>(),
                        FullName = item["full_name"]?.ToString() ?? "Не указано",
                        SubjectId = item["subject_id"]?.ToObject<int?>(),
                        Email = item["email"]?.ToString() ?? "Не указано",
                        SubjectName = item["subjects"]?["name"]?.ToString() ?? "Не назначен"
                    };

                    _allTeachers.Add(teacher);
                    _teachers.Add(teacher);
                }

                UpdateStatus();
                UpdateLastUpdateTime();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки преподавателей: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Ошибка загрузки";
            }
            finally
            {
                TeachersGrid.IsEnabled = true;
            }
        }

        private void UpdateStatus()
        {
            CountText.Text = $"{_teachers.Count} преподавателей";
            StatusText.Text = "Готово";
        }

        private void UpdateLastUpdateTime()
        {
            _lastUpdate = DateTime.Now;
            LastUpdateText.Text = "только что";
        }

        private void AddTeacher_Click(object sender, RoutedEventArgs e)
        {
            ShowEditView();
        }

        private void EditTeacher_Click(object sender, RoutedEventArgs e)
        {
            var selectedTeacher = TeachersGrid.SelectedItem as Teacher;
            if (selectedTeacher == null)
            {
                MessageBox.Show("Выберите преподавателя для редактирования", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowEditView(selectedTeacher);
        }

        private async void DeleteTeacher_Click(object sender, RoutedEventArgs e)
        {
            var selectedTeacher = TeachersGrid.SelectedItem as Teacher;
            if (selectedTeacher == null)
            {
                MessageBox.Show("Выберите преподавателя для удаления", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Вы уверены, что хотите удалить преподавателя \"{selectedTeacher.FullName}\"?\n\nЭто действие нельзя отменить.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    StatusText.Text = "Удаление...";
                    await SupabaseClient.DeleteTeacher(selectedTeacher.Id);
                    await LoadTeachersAsync();

                    MessageBox.Show("Учитель успешно удален", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadTeachersAsync();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(searchText))
            {
                // Показываем всех учителей если поиск пустой
                _teachers.Clear();
                foreach (var teacher in _allTeachers)
                {
                    _teachers.Add(teacher);
                }
            }
            else
            {
                // Фильтруем по ФИО, предмету и email
                var filteredTeachers = _allTeachers
                    .Where(t => t.FullName.ToLower().Contains(searchText) ||
                               (t.SubjectName?.ToLower().Contains(searchText) ?? false) ||
                               (t.Email?.ToLower().Contains(searchText) ?? false))
                    .ToList();

                _teachers.Clear();
                foreach (var teacher in filteredTeachers)
                {
                    _teachers.Add(teacher);
                }
            }

            UpdateStatus();
        }

        private void TeachersGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EditTeacher_Click(sender, e);
        }

        private void ShowEditView(Teacher teacher = null)
        {
            // Получаем родительский ContentControl (из AdminWindow)
            var parentContentControl = GetParentContentControl(this);
            if (parentContentControl == null)
            {
                MessageBox.Show("Ошибка навигации", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Сохраняем текущий view для возврата
            var currentView = parentContentControl.Content;

            var editView = new TeacherEditView(teacher);
            editView.SaveCompleted += async (success) =>
            {
                if (success)
                {
                    // Возвращаемся к списку и обновляем данные
                    parentContentControl.Content = currentView;
                    await LoadTeachersAsync();
                }
                else
                {
                    // Остаемся в форме редактирования при ошибке
                    parentContentControl.Content = editView;
                }
            };

            editView.GoBack += () =>
            {
                // Просто возвращаемся к списку без обновления
                parentContentControl.Content = currentView;
            };

            // Переключаемся на форму редактирования
            parentContentControl.Content = editView;
        }

        // Вспомогательный метод для поиска родительского ContentControl
        private ContentControl GetParentContentControl(DependencyObject child)
        {
            var parent = VisualTreeHelper.GetParent(child);

            while (parent != null)
            {
                if (parent is ContentControl contentControl)
                {
                    return contentControl;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

        private void ShowListView(Grid parentGrid)
        {
            parentGrid.Children.Clear();
            parentGrid.Children.Add(this);
        }

        // Метод для обновления времени последнего обновления
        private string GetTimeAgo(DateTime date)
        {
            var timeSpan = DateTime.Now - date;

            if (timeSpan.TotalMinutes < 1)
                return "только что";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} мин назад";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} ч назад";

            return date.ToString("dd.MM.yyyy HH:mm");
        }

        // Обновляем время при взаимодействии
        private void TeachersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LastUpdateText.Text = GetTimeAgo(_lastUpdate);
        }


    }
}