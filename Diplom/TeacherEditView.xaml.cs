using Diplom.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    /// Логика взаимодействия для TeacherEditView.xaml
    /// </summary>
    public partial class TeacherEditView : UserControl
    {
        public Teacher Teacher { get; set; }
        public ObservableCollection<Subject> Subjects { get; set; }

        public event Action<bool> SaveCompleted;
        public event Action GoBack;

        public TeacherEditView(Teacher teacher = null)
        {
            InitializeComponent();
            DataContext = this;

            Teacher = teacher ?? new Teacher();
            Subjects = new ObservableCollection<Subject>();

            Loaded += async (s, e) => await InitializeAsync();
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            try
            {
                await LoadSubjectsAsync();
                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadSubjectsAsync()
        {
            try
            {
                var result = await SupabaseClient.ExecuteQuery("subjects", "select=*&order=name");
                Subjects.Clear();

                // Добавляем вариант "Не назначен"
                Subjects.Add(new Subject { Id = 0, Name = "Не назначен" });

                foreach (var item in result)
                {
                    var subject = new Subject
                    {
                        Id = item["id"].Value<int>(),
                        Name = item["name"]?.ToString()
                    };
                    Subjects.Add(subject);
                }

                SubjectComboBox.ItemsSource = Subjects;

                if (Teacher.SubjectId > 0)
                {
                    SubjectComboBox.SelectedValue = Teacher.SubjectId;
                }
                else
                {
                    SubjectComboBox.SelectedValue = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки предметов: {ex.Message}");
            }
        }

        private void UpdateTitle()
        {
            TitleText.Text = Teacher.Id > 0 ? "Редактирование учителя" : "Добавление учителя";
            SaveButton.Content = Teacher.Id > 0 ? "Обновить" : "Создать";
        }



        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                SaveButton.IsEnabled = false;
                SaveButton.Content = "Сохранение...";

                // Если выбран "Не назначен", устанавливаем null
                int? subjectId = Teacher.SubjectId > 0 ? Teacher.SubjectId : null;

                // Для нового учителя предмет обязателен
                if (Teacher.Id == 0 && subjectId == null)
                {
                    ShowValidationError("Для нового учителя необходимо выбрать предмет");
                    return;
                }

                // Очищаем email если он пустой или состоит из пробелов
                if (string.IsNullOrWhiteSpace(Teacher.Email))
                {
                    Teacher.Email = null;
                }

                if (Teacher.Id > 0)
                {
                    await SupabaseClient.UpdateTeacher(
                        Teacher.Id,
                        Teacher.FullName.Trim(),
                        subjectId,
                        Teacher.Email
                    );
                }
                else
                {
                    await SupabaseClient.AddTeacher(
                        Teacher.FullName.Trim(),
                        subjectId,
                        Teacher.Email
                    );
                }

                SaveCompleted?.Invoke(true);
                MessageBox.Show(Teacher.Id > 0 ? "Учитель успешно обновлен" : "Учитель успешно добавлен",
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
                SaveButton.Content = Teacher.Id > 0 ? "Обновить" : "Создать";
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



        private const int MAX_FULLNAME_LENGTH = 100;
        private const int MAX_EMAIL_LENGTH = 100;

        // Разрешаем буквы (латиница/кириллица), пробел, дефис, апостроф
        private readonly Regex _nameRegex = new Regex(@"^[а-яА-ЯёЁ\s\-']+$", RegexOptions.Compiled);
        private readonly Regex _emailRegex = new Regex(@"^[a-zA-Z0-9@\.\-_]+$");

        // Флаг, чтобы не ловить TextChanged, пока сами правим Text
        private bool _isInternalTextChange;

        // Валидация ФИО при вводе (по одному символу)
        private void FullNameTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только 1 символ из допустимого набора
            if (!_nameRegex.IsMatch(e.Text))
            {
                e.Handled = true;
                return;
            }

            var textBox = (TextBox)sender;

            // Проверка длины с учётом выделенного текста (перезапись)
            var selectionStart = textBox.SelectionStart;
            var selectionLength = textBox.SelectionLength;

            string newText = textBox.Text.Remove(selectionStart, selectionLength)
                                         .Insert(selectionStart, e.Text);

            if (newText.Length > MAX_FULLNAME_LENGTH)
            {
                e.Handled = true;
                ShowValidationError("ФИО не может превышать 100 символов");
            }
        }

        private void FullNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInternalTextChange)
                return;

            var textBox = (TextBox)sender;

            // Убираем лишние пробелы (несколько подряд -> один, обрезаем по краям)
            string cleaned = Regex.Replace(textBox.Text, @"\s+", " ").TrimStart();

            if (cleaned != textBox.Text)
            {
                _isInternalTextChange = true;
                int oldCaret = textBox.CaretIndex;
                textBox.Text = cleaned;
                textBox.CaretIndex = Math.Min(cleaned.Length, oldCaret);
                _isInternalTextChange = false;
            }

            // Во время ввода не показываем MessageBox, только финальная проверка в ValidateForm.
            // Можно подсвечивать контрол, если хочешь, но без окон.
        }

        // Валидация email при потере фокуса
        private void EmailTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = (TextBox)sender;

            if (!string.IsNullOrWhiteSpace(textBox.Text) && !IsValidEmail(textBox.Text))
            {
                ShowValidationError("Введите корректный email адрес");
                //textBox.Focus();
            }
        }

        private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInternalTextChange)
                return;

            var textBox = (TextBox)sender;

            if (textBox.Text.Length > MAX_EMAIL_LENGTH)
            {
                _isInternalTextChange = true;
                textBox.Text = textBox.Text.Substring(0, MAX_EMAIL_LENGTH);
                textBox.CaretIndex = textBox.Text.Length;
                _isInternalTextChange = false;

                ShowValidationError($"Email не может превышать {MAX_EMAIL_LENGTH} символов");
            }
        }

        // Методы валидации
        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return true; // email не обязателен

            email = email.Trim();

            return _emailRegex.IsMatch(email) &&
                   !email.Contains(" ") &&
                   email.Count(c => c == '@') == 1;
        }

        private bool IsValidFullName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return false;

            fullName = Regex.Replace(fullName, @"\s+", " ").Trim();

            if (fullName.Length > MAX_FULLNAME_LENGTH)
                return false;

            var parts = fullName.Split(' ').Where(p => !string.IsNullOrEmpty(p)).ToArray();
            if (parts.Length < 2)
                return false;

            // Проверяем, что каждая часть состоит только из допустимых символов
            foreach (var part in parts)
            {
                if (!_nameRegex.IsMatch(part))
                    return false;
            }

            return true;
        }

        private void ShowValidationError(string message)
        {
            MessageBox.Show(message, "Ошибка валидации",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Итоговая проверка формы (например, перед сохранением)
        private bool ValidateForm()
        {
            if (!IsValidFullName(Teacher.FullName))
            {
                ShowValidationError("Введите корректное ФИО учителя (фамилия и имя через пробел, только буквы, пробелы, дефисы и апострофы)");
                FullNameTextBox.Focus();
                return false;
            }

            if (!IsValidEmail(Teacher.Email))
            {
                ShowValidationError("Введите корректный email адрес или оставьте поле пустым");
                EmailTextBox.Focus();
                return false;
            }

            // Проверяем, что выбран предмет (если для нового учителя обязателен)
            if (Teacher.Id == 0 && (Teacher.SubjectId == 0 || Teacher.SubjectId == null))
            {
                ShowValidationError("Выберите предмет для нового учителя");
                SubjectComboBox.Focus();
                return false;
            }

            return true;
        }


    }
}
