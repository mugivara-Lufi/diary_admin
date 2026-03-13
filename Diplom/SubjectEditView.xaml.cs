using System;
using System.Collections.Generic;
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
using Diplom.Models;

namespace Diplom
{
    /// <summary>
    /// Логика взаимодействия для SubjectEditView.xaml
    /// </summary>
    public partial class SubjectEditView : UserControl
    {
        private Subject _currentSubject;

        public event Action<bool> SaveCompleted;
        public event Action GoBack;

        public SubjectEditView(Subject subject = null)
        {
            InitializeComponent();
            DataContext = this;

            if (subject != null)
            {
                // Редактирование существующего предмета
                CurrentSubject = new Subject
                {
                    Id = subject.Id,
                    Name = subject.Name
                };
            }
            else
            {
                // Создание нового предмета
                CurrentSubject = new Subject();
            }

            Loaded += async (s, e) => await LoadSubjectStats();
        }

        public Subject CurrentSubject
        {
            get => _currentSubject;
            set
            {
                _currentSubject = value;
                OnPropertyChanged();
            }
        }

        public string TitleText => CurrentSubject?.Id > 0 ? "✏️ Редактирование предмета" : "➕ Добавление предмета";
        public string SubtitleText => CurrentSubject?.Id > 0 ? "Изменение информации о предмете" : "Создание нового учебного предмета";

        private async System.Threading.Tasks.Task LoadSubjectStats()
        {
            if (CurrentSubject?.Id > 0)
            {
                try
                {
                    // Показываем панель статистики только при редактировании
                    StatsPanel.Visibility = Visibility.Visible;

                    // Получаем количество учителей по этому предмету
                    var teachersCount = await GetTeachersCount(CurrentSubject.Id);

                    TeachersCountText.Text = $"• Преподают {teachersCount} учителей\n" +
                                            "• Используется в расписании занятий\n" +
                                            "• По предмету выставляются оценки";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка загрузки статистики: {ex.Message}");
                }
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

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(CurrentSubject.Name))
            {
                MessageBox.Show("Введите название предмета", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return false;
            }

            if (CurrentSubject.Name.Length < 2)
            {
                MessageBox.Show("Название предмета должно содержать минимум 2 символа", "Ошибка",
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
                if (CurrentSubject.Id > 0)
                {
                    // Обновление существующего предмета
                    await SupabaseClient.UpdateSubject(CurrentSubject.Id, CurrentSubject.Name);

                    MessageBox.Show("Предмет успешно обновлен", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Создание нового предмета
                    await SupabaseClient.AddSubject(CurrentSubject.Name);

                    MessageBox.Show("Предмет успешно создан", "Успех",
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
