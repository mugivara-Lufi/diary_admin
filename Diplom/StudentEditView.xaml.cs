using Diplom.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
    /// Логика взаимодействия для StudentEditView.xaml
    /// </summary>
    public partial class StudentEditView : UserControl
    {
        public Student Student { get; set; }
        public ObservableCollection<Class> Classes { get; set; }
        public ObservableCollection<Parent> Parents { get; set; }

        public event Action<bool> SaveCompleted;
        public event Action GoBack;

        public StudentEditView(Student student = null)
        {
            InitializeComponent();
            DataContext = this;

            Student = student ?? new Student();
            Classes = new ObservableCollection<Class>();
            Parents = new ObservableCollection<Parent>();

            Loaded += async (s, e) => await InitializeAsync();
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            try
            {
                await LoadClassesAsync();
                await LoadParentsAsync();
                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadClassesAsync()
        {
            try
            {
                var result = await SupabaseClient.ExecuteQuery("classes", "select=*&order=name");
                Classes.Clear();

                // Добавляем вариант "Не назначен" в коллекцию
                Classes.Add(new Class { Id = 0, Name = "Не назначен" });

                foreach (var item in result)
                {
                    var classItem = new Class
                    {
                        Id = item["id"].Value<int>(),
                        Name = item["name"]?.ToString()
                    };
                    Classes.Add(classItem);
                }

                // Установка ItemsSource - ЭТО БЫЛА ОСНОВНАЯ ПРОБЛЕМА
                ClassComboBox.ItemsSource = Classes;

                if (Student.ClassId.HasValue && Student.ClassId.Value > 0)
                {
                    ClassComboBox.SelectedValue = Student.ClassId.Value;
                }
                else
                {
                    ClassComboBox.SelectedValue = 0; // Выбираем "Не назначен"
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки классов: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadParentsAsync()
        {
            if (Student.Id == 0) return;

            try
            {
                var result = await SupabaseClient.GetStudentParents(Student.Id);
                Parents.Clear();

                foreach (var item in result)
                {
                    var parentData = item["parents"];
                    var parent = new Parent
                    {
                        Id = parentData["id"].Value<int>(),
                        FullName = parentData["full_name"]?.ToString(),
                        Phone = parentData["phone"]?.ToString(),
                        Email = parentData["email"]?.ToString()
                    };
                    Parents.Add(parent);
                }

                ParentsItemsControl.ItemsSource = Parents;
            }
            catch (Exception ex)
            {
                // Если нет родителей - просто игнорируем ошибку
            }
        }

        private void UpdateTitle()
        {
            TitleText.Text = Student.Id > 0 ? "Редактирование ученика" : "Добавление ученика";
            SaveButton.Content = Student.Id > 0 ? "Обновить" : "Создать";
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(Student.FullName))
            {
                MessageBox.Show("Введите ФИО ученика", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                FullNameTextBox.Focus();
                return false;
            }

            if (Student.FullName.Length < 2)
            {
                MessageBox.Show("ФИО должно содержать не менее 2 символов", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                FullNameTextBox.Focus();
                return false;
            }

            return true;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                SaveButton.IsEnabled = false;
                SaveButton.Content = "Сохранение...";

                if (Student.Id > 0)
                {
                    // Обновление существующего ученика
                    await SupabaseClient.UpdateStudent(
                        Student.Id,
                        Student.FullName,
                        Student.BirthDate?.ToString("yyyy-MM-dd"),
                        Student.ClassId,
                        Student.Contact
                    );
                }
                else
                {
                    // Добавление нового ученика
                    await SupabaseClient.AddStudent(
                        Student.FullName,
                        Student.BirthDate?.ToString("yyyy-MM-dd"),
                        Student.ClassId,
                        Student.Contact
                    );
                }

                SaveCompleted?.Invoke(true);
                MessageBox.Show(Student.Id > 0 ? "Ученик успешно обновлен" : "Ученик успешно добавлен",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SaveCompleted?.Invoke(false);
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveButton.Content = Student.Id > 0 ? "Обновить" : "Создать";
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

        private void AddParentButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция добавления родителей будет реализована в следующем обновлении",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RemoveParent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Parent parent)
            {
                var result = MessageBox.Show($"Удалить родителя {parent.FullName}?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // TODO: Реализовать удаление связи с родителем
                    MessageBox.Show("Функция удаления родителей будет реализована в следующем обновлении",
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }
}
