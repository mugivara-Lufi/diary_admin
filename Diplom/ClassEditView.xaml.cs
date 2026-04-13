using Diplom.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
    /// Логика взаимодействия для ClassEditView.xaml
    /// </summary>
    public partial class ClassEditView : UserControl
    {
        private Class _currentClass;
        private ObservableCollection<Teacher> _teachers;

        public event Action<bool> SaveCompleted;
        public event Action GoBack;

        public ClassEditView(Class classItem = null)
        {
            InitializeComponent();
            _teachers = new ObservableCollection<Teacher>();
            DataContext = this;

            if (classItem != null)
            {
                // Редактирование существующей группы
                CurrentClass = new Class
                {
                    Id = classItem.Id,
                    Name = classItem.Name,
                    TeacherId = classItem.TeacherId
                };
            }
            else
            {
                // Создание новой группы
                CurrentClass = new Class();
            }

            Loaded += async (s, e) => await LoadTeachersAsync();
        }

        public Class CurrentClass
        {
            get => _currentClass;
            set
            {
                _currentClass = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Teacher> Teachers
        {
            get => _teachers;
            set
            {
                _teachers = value;
                OnPropertyChanged();
            }
        }

        public string TitleText => CurrentClass?.Id > 0 ? "✏️ Редактирование группы" : "➕ Добавление группы";
        public string SubtitleText => CurrentClass?.Id > 0 ? "Изменение информации о группе" : "Создание новой группы";

        private async System.Threading.Tasks.Task LoadTeachersAsync()
        {
            try
            {
                var result = await SupabaseClient.GetTeachersWithSubjects();
                Teachers.Clear();

                // Добавляем пустой элемент для возможности снятия выбора
                Teachers.Add(new Teacher { Id = 0, FullName = "Не назначен" });

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
                    Teachers.Add(teacher);
                }

                TeacherComboBox.ItemsSource = Teachers;

                // Устанавливаем выбранного преподавателя если есть
                if (CurrentClass.TeacherId.HasValue && CurrentClass.TeacherId > 0)
                {
                    TeacherComboBox.SelectedValue = CurrentClass.TeacherId.Value;
                }
                else
                {
                    TeacherComboBox.SelectedValue = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки преподавателей: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(CurrentClass.Name))
            {
                MessageBox.Show("Введите название группы", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return false;
            }

            if (CurrentClass.Name.Length < 2)
            {
                MessageBox.Show("Название группы должно содержать минимум 2 символа", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return false;
            }

            return true;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                if (CurrentClass.Id > 0)
                {
                    // Обновление существующей группы
                    await SupabaseClient.UpdateClass(
                        CurrentClass.Id,
                        CurrentClass.Name,
                        CurrentClass.TeacherId == 0 ? null : CurrentClass.TeacherId
                    );

                    MessageBox.Show("Группа успешно обновлена", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Создание новой группы
                    await SupabaseClient.AddClass(
                        CurrentClass.Name,
                        CurrentClass.TeacherId == 0 ? null : CurrentClass.TeacherId
                    );

                    MessageBox.Show("Группа успешно создана", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                SaveCompleted?.Invoke(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SaveCompleted?.Invoke(false);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            GoBack?.Invoke();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            GoBack?.Invoke();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}