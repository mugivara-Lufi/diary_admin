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
    /// <summary>
    /// Логика взаимодействия для ClassesView.xaml
    /// </summary>
    public partial class ClassesView : UserControl
    {
        private ObservableCollection<Class> _classes;
        private List<Class> _allClasses;
        private DateTime _lastUpdate;

        public ClassesView()
        {
            InitializeComponent();
            _classes = new ObservableCollection<Class>();
            _allClasses = new List<Class>();
            ClassesGrid.ItemsSource = _classes;

            Loaded += async (s, e) => await LoadClassesAsync();
        }

        private async System.Threading.Tasks.Task LoadClassesAsync()
        {
            try
            {
                StatusText.Text = "Загрузка данных...";
                ClassesGrid.IsEnabled = false;

                var result = await SupabaseClient.GetClassesWithTeachers();

                // Используем альтернативный метод для подсчета студентов
                var countsDict = await SupabaseClient.GetStudentsCountByClassAlternative();

                _allClasses.Clear();
                _classes.Clear();

                foreach (var item in result)
                {
                    int classId = item["id"].Value<int>();

                    var classItem = new Class
                    {
                        Id = classId,
                        Name = item["name"]?.ToString() ?? "Не указано",
                        TeacherId = item["teacher_id"]?.ToObject<int?>(),
                        TeacherName = item["teachers"]?["full_name"]?.ToString() ?? "Не назначен",
                        StudentsCount = countsDict.ContainsKey(classId) ? countsDict[classId] : 0
                    };

                    _allClasses.Add(classItem);
                    _classes.Add(classItem);
                }

                UpdateStatus();
                UpdateLastUpdateTime();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки групп: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Ошибка загрузки";
            }
            finally
            {
                ClassesGrid.IsEnabled = true;
            }
        }

        private void UpdateStatus()
        {
            CountText.Text = $"{_classes.Count} групп";
            StatusText.Text = "Готово";
        }

        private void UpdateLastUpdateTime()
        {
            _lastUpdate = DateTime.Now;
            LastUpdateText.Text = "только что";
        }

        private void AddClass_Click(object sender, RoutedEventArgs e)
        {
            ShowEditView();
        }

        private void EditClass_Click(object sender, RoutedEventArgs e)
        {
            var selectedClass = ClassesGrid.SelectedItem as Class;
            if (selectedClass == null)
            {
                MessageBox.Show("Выберите группу для редактирования", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowEditView(selectedClass);
        }

        private async void DeleteClass_Click(object sender, RoutedEventArgs e)
        {
            var selectedClass = ClassesGrid.SelectedItem as Class;
            if (selectedClass == null)
            {
                MessageBox.Show("Выберите группу для удаления", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Вы уверены, что хотите удалить группу \"{selectedClass.Name}\"?\n\nЭто действие нельзя отменить.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    StatusText.Text = "Удаление...";
                    await SupabaseClient.DeleteClass(selectedClass.Id);
                    await LoadClassesAsync();

                    MessageBox.Show("Группа успешно удалена", "Успех",
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
            _ = LoadClassesAsync();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(searchText))
            {
                _classes.Clear();
                foreach (var c in _allClasses)
                {
                    _classes.Add(c);
                }
            }
            else
            {
                var filteredClasses = _allClasses
                    .Where(c =>
                        c.Name.ToLower().Contains(searchText) ||
                        (c.TeacherName?.ToLower().Contains(searchText) ?? false))
                    .ToList();

                _classes.Clear();
                foreach (var c in filteredClasses)
                {
                    _classes.Add(c);
                }
            }

            UpdateStatus();
        }

        private void ClassesGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EditClass_Click(sender, e);
        }

        private void ShowEditView(Class classItem = null)
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

            var editView = new ClassEditView(classItem);
            editView.SaveCompleted += async (success) =>
            {
                if (success)
                {
                    // Возвращаемся к списку и обновляем данные
                    parentContentControl.Content = currentView;
                    await LoadClassesAsync();
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
        private void ClassesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LastUpdateText.Text = GetTimeAgo(_lastUpdate);
        }
    }
}