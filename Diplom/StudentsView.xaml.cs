using Diplom.Models;
using Microsoft.IdentityModel.Tokens;
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
    /// Логика взаимодействия для StudentsView.xaml
    /// </summary>
    public partial class StudentsView : UserControl
    {
        private ObservableCollection<Student> _students;
        private List<Student> _allStudents;
        private DateTime _lastUpdate;

        public StudentsView()
        {
            InitializeComponent();
            _students = new ObservableCollection<Student>();
            _allStudents = new List<Student>();
            StudentsGrid.ItemsSource = _students;

            Loaded += async (s, e) => await LoadStudentsAsync();
        }

        private async System.Threading.Tasks.Task LoadStudentsAsync()
        {
            try
            {
                StatusText.Text = "Загрузка данных...";
                StudentsGrid.IsEnabled = false;

                var result = await SupabaseClient.GetStudentsWithClasses();

                _allStudents.Clear();
                _students.Clear();

                foreach (var item in result)
                {
                    var student = new Student
                    {
                        Id = item["id"].Value<int>(),
                        FullName = item["full_name"]?.ToString() ?? "Не указано",
                        BirthDate = item["birth_date"]?.ToObject<DateTime?>(),
                        ClassId = item["class_id"]?.ToObject<int?>(),
                        Contact = item["contact"]?.ToString() ?? "Не указано",
                        ClassName = item["classes"]?["name"]?.ToString() ?? "Не назначен"
                    };

                    _allStudents.Add(student);
                    _students.Add(student);
                }

                UpdateStatus();
                UpdateLastUpdateTime();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки учеников: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Ошибка загрузки";
            }
            finally
            {
                StudentsGrid.IsEnabled = true;
            }
        }

        private void UpdateStatus()
        {
            CountText.Text = $"{_students.Count} учеников";
            StatusText.Text = "Готово";
        }

        private void UpdateLastUpdateTime()
        {
            _lastUpdate = DateTime.Now;
            LastUpdateText.Text = "только что";
        }

        private void AddStudent_Click(object sender, RoutedEventArgs e)
        {
            ShowEditView();
        }

        private void EditStudent_Click(object sender, RoutedEventArgs e)
        {
            var selectedStudent = StudentsGrid.SelectedItem as Student;
            if (selectedStudent == null)
            {
                MessageBox.Show("Выберите ученика для редактирования", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowEditView(selectedStudent);
        }

        private async void DeleteStudent_Click(object sender, RoutedEventArgs e)
        {
            var selectedStudent = StudentsGrid.SelectedItem as Student;
            if (selectedStudent == null)
            {
                MessageBox.Show("Выберите ученика для удаления", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Вы уверены, что хотите удалить ученика \"{selectedStudent.FullName}\"?\n\nЭто действие нельзя отменить.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    StatusText.Text = "Удаление...";
                    await SupabaseClient.DeleteStudent(selectedStudent.Id);
                    await LoadStudentsAsync();

                    MessageBox.Show("Ученик успешно удален", "Успех",
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
            _ = LoadStudentsAsync();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(searchText))
            {
                // Показываем всех учеников если поиск пустой
                _students.Clear();
                foreach (var student in _allStudents)
                {
                    _students.Add(student);
                }
            }
            else
            {
                // Фильтруем по ФИО и классу
                var filteredStudents = _allStudents
                    .Where(s => s.FullName.ToLower().Contains(searchText) ||
                               (s.ClassName?.ToLower().Contains(searchText) ?? false) ||
                               (s.Contact?.ToLower().Contains(searchText) ?? false))
                    .ToList();

                _students.Clear();
                foreach (var student in filteredStudents)
                {
                    _students.Add(student);
                }
            }

            UpdateStatus();
        }

        private void StudentsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EditStudent_Click(sender, e);
        }

        private void ShowEditView(Student student = null)
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

            var editView = new StudentEditView(student);
            editView.SaveCompleted += async (success) =>
            {
                if (success)
                {
                    // Возвращаемся к списку и обновляем данные
                    parentContentControl.Content = currentView;
                    await LoadStudentsAsync();
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
        private void StudentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LastUpdateText.Text = GetTimeAgo(_lastUpdate);
        }
    }
}
