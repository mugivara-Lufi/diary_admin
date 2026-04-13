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
using Diplom.Models;
using Newtonsoft.Json.Linq;

namespace Diplom
{
    /// <summary>
    /// Логика взаимодействия для SubjectsView.xaml
    /// </summary>
    public partial class SubjectsView : UserControl
    {
        private ObservableCollection<Subject> _subjects;
        private List<Subject> _allSubjects;
        private DateTime _lastUpdate;

        public SubjectsView()
        {
            InitializeComponent();
            _subjects = new ObservableCollection<Subject>();
            _allSubjects = new List<Subject>();
            SubjectsGrid.ItemsSource = _subjects;

            Loaded += async (s, e) => await LoadSubjectsAsync();
        }

        private async System.Threading.Tasks.Task LoadSubjectsAsync()
        {
            try
            {
                StatusText.Text = "Загрузка данных...";
                SubjectsGrid.IsEnabled = false;

                var result = await SupabaseClient.GetAllSubjects();

                // Используем альтернативный метод для подсчета преподавателей
                var countsDict = await SupabaseClient.GetTeachersCountBySubjectAlternative();

                _allSubjects.Clear();
                _subjects.Clear();

                foreach (var item in result)
                {
                    int subjectId = item["id"].Value<int>();

                    var subject = new Subject
                    {
                        Id = subjectId,
                        Name = item["name"]?.ToString() ?? "Не указано",
                        TeachersCount = countsDict.ContainsKey(subjectId) ? countsDict[subjectId] : 0
                    };

                    _allSubjects.Add(subject);
                    _subjects.Add(subject);
                }

                UpdateStatus();
                UpdateLastUpdateTime();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки дисциплин: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Ошибка загрузки";
            }
            finally
            {
                SubjectsGrid.IsEnabled = true;
            }
        }

        private async Task<int> GetTeachersCount(int subjectId)
        {
            try
            {
                var teachers = await SupabaseClient.ExecuteQuery("teachers", $"subject_id=eq.{subjectId}&select=count");
                return teachers.Count > 0 ? (int)teachers[0]["count"] : 0;
            }
            catch
            {
                return 0;
            }
        }

        private void UpdateStatus()
        {
            CountText.Text = $"{_subjects.Count} дисциплин";
            StatusText.Text = "Готово";
        }

        private void UpdateLastUpdateTime()
        {
            _lastUpdate = DateTime.Now;
            LastUpdateText.Text = "только что";
        }

        private void AddSubject_Click(object sender, RoutedEventArgs e)
        {
            ShowEditView();
        }

        private void EditSubject_Click(object sender, RoutedEventArgs e)
        {
            var selectedSubject = SubjectsGrid.SelectedItem as Subject;
            if (selectedSubject == null)
            {
                MessageBox.Show("Выберите дисциплину для редактирования", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowEditView(selectedSubject);
        }

        private async void DeleteSubject_Click(object sender, RoutedEventArgs e)
        {
            var selectedSubject = SubjectsGrid.SelectedItem as Subject;
            if (selectedSubject == null)
            {
                MessageBox.Show("Выберите дисциплину для удаления", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Вы уверены, что хотите удалить дисциплину \"{selectedSubject.Name}\"?\n\nЭто действие нельзя отменить.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    StatusText.Text = "Удаление...";
                    await SupabaseClient.DeleteSubject(selectedSubject.Id);
                    await LoadSubjectsAsync();

                    MessageBox.Show("Дисциплина успешно удалена", "Успех",
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
            _ = LoadSubjectsAsync();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(searchText))
            {
                _subjects.Clear();
                foreach (var subject in _allSubjects)
                {
                    _subjects.Add(subject);
                }
            }
            else
            {
                var filteredSubjects = _allSubjects
                    .Where(s => s.Name.ToLower().Contains(searchText))
                    .ToList();

                _subjects.Clear();
                foreach (var subject in filteredSubjects)
                {
                    _subjects.Add(subject);
                }
            }

            UpdateStatus();
        }

        private void SubjectsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EditSubject_Click(sender, e);
        }

        private void ShowEditView(Subject subject = null)
        {
            var parentContentControl = GetParentContentControl(this);
            if (parentContentControl == null)
            {
                MessageBox.Show("Ошибка навигации", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var currentView = parentContentControl.Content;

            var editView = new SubjectEditView(subject);
            editView.SaveCompleted += async (success) =>
            {
                if (success)
                {
                    parentContentControl.Content = currentView;
                    await LoadSubjectsAsync();
                }
                else
                {
                    parentContentControl.Content = editView;
                }
            };

            editView.GoBack += () =>
            {
                parentContentControl.Content = currentView;
            };

            parentContentControl.Content = editView;
        }

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

        private void SubjectsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LastUpdateText.Text = GetTimeAgo(_lastUpdate);
        }
    }
}