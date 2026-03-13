using Diplom.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Diplom
{
    public partial class ScheduleEditView : UserControl, INotifyPropertyChanged
    {
        private Schedule _currentSchedule;
        private readonly ObservableCollection<Subject> _subjects;
        private readonly ObservableCollection<Teacher> _teachers;
        private readonly ObservableCollection<KeyValuePair<int, string>> _lessonNumbers;
        private readonly Dictionary<int, (string Start, string End)> _lessonTimes;

        private int _classId;
        private DateTime _weekStart;

        public event Action<bool, Schedule> SaveCompleted;
        public event Action GoBack;

        public ScheduleEditView(Schedule schedule = null, int classId = 0, DateTime weekStart = default)
        {
            InitializeComponent();
            DataContext = this;

            _classId = classId;
            _weekStart = weekStart == default ? DateTime.Now.Date : weekStart.Date;

            _subjects = new ObservableCollection<Subject>();
            _teachers = new ObservableCollection<Teacher>();
            _lessonNumbers = new ObservableCollection<KeyValuePair<int, string>>();

            _lessonTimes = new Dictionary<int, (string Start, string End)>
            {
                { 1, ("8:00", "8:45") },
                { 2, ("8:55", "9:40") },
                { 3, ("9:50", "10:35") },
                { 4, ("10:45", "11:30") },
                { 5, ("11:40", "12:25") },
                { 6, ("12:35", "13:20") },
                { 7, ("13:30", "14:15") },
                { 8, ("14:25", "15:10") }
            };

            for (int i = 1; i <= 8; i++)
            {
                var t = _lessonTimes[i];
                _lessonNumbers.Add(new KeyValuePair<int, string>(i, $"{i} урок ({t.Start} - {t.End})"));
            }

            if (schedule != null && schedule.Id > 0)
            {
                CurrentSchedule = new Schedule
                {
                    Id = schedule.Id,
                    ClassId = classId,
                    SubjectId = schedule.SubjectId,
                    TeacherId = schedule.TeacherId,
                    LessonDate = schedule.LessonDate,
                    LessonNumber = schedule.LessonNumber,
                    Topic = schedule.Topic
                };
            }
            else
            {
                CurrentSchedule = new Schedule
                {
                    ClassId = classId,
                    LessonDate = _weekStart,
                    LessonNumber = 1
                };
            }

            Loaded += async (s, e) => await InitializeAsync();
        }

        public Schedule CurrentSchedule
        {
            get => _currentSchedule;
            set
            {
                _currentSchedule = value;
                OnPropertyChanged();
            }
        }

        public string TitleText => CurrentSchedule?.Id > 0 ? "✏️ Редактирование урока" : "➕ Добавление урока";
        public ObservableCollection<KeyValuePair<int, string>> LessonNumbers => _lessonNumbers;

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            await LoadClassInfo();
            await LoadTeachersAsync();
            await LoadSubjectsAsync();
            InitializeControls();
            UpdateTimeInfo();
            CheckForConflicts();
        }

        private async System.Threading.Tasks.Task LoadClassInfo()
        {
            try
            {
                var classes = await SupabaseClient.ExecuteQuery("classes", $"id=eq.{_classId}&select=name");
                if (classes.Count > 0)
                    ClassNameText.Text = classes[0]["name"]?.ToString() ?? "Неизвестный класс";
                else
                    ClassNameText.Text = "Неизвестный класс";
            }
            catch (Exception ex)
            {
                ClassNameText.Text = "Неизвестный класс";
            }
        }

        private async System.Threading.Tasks.Task LoadSubjectsAsync()
        {
            try
            {
                var result = await SupabaseClient.GetAllSubjects();
                _subjects.Clear();

                foreach (var item in result)
                {
                    if (item?["id"] == null || item["id"].Type == JTokenType.Null) continue;

                    int id = item["id"].Value<int>();
                    if (id <= 0) continue;

                    var subject = new Subject
                    {
                        Id = id,
                        Name = item["name"]?.ToString() ?? "Не указано"
                    };
                    _subjects.Add(subject);
                }

                SubjectComboBox.ItemsSource = _subjects;
                SubjectComboBox.DisplayMemberPath = "Name";
                SubjectComboBox.SelectedValuePath = "Id";

                if (CurrentSchedule.SubjectId.HasValue && CurrentSchedule.SubjectId.Value > 0)
                {
                    SubjectComboBox.SelectedValue = CurrentSchedule.SubjectId.Value;
                    SubjectComboBox_SelectionChanged(SubjectComboBox, null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки предметов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadTeachersAsync()
        {
            try
            {
                var result = await SupabaseClient.GetTeachersWithSubjects();
                _teachers.Clear();

                foreach (var item in result)
                {
                    if (item?["id"] == null || item["id"].Type == JTokenType.Null) continue;

                    int id = item["id"].Value<int>();
                    if (id <= 0) continue;

                    var teacher = new Teacher
                    {
                        Id = id,
                        FullName = item["full_name"]?.ToString() ?? "Не указано",
                        SubjectId = item["subject_id"]?.Type == JTokenType.Null ? (int?)null : item["subject_id"]?.Value<int?>(),
                        Email = item["email"]?.ToString() ?? ""
                    };
                    _teachers.Add(teacher);
                }

                TeacherComboBox.DisplayMemberPath = "FullName";
                TeacherComboBox.SelectedValuePath = "Id";
                TeacherComboBox.ItemsSource = _teachers;

                if (CurrentSchedule.TeacherId.HasValue && CurrentSchedule.TeacherId.Value > 0)
                    TeacherComboBox.SelectedValue = CurrentSchedule.TeacherId.Value;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки учителей: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeControls()
        {
            LessonNumberComboBox.ItemsSource = _lessonNumbers;
            LessonNumberComboBox.DisplayMemberPath = "Value";
            LessonNumberComboBox.SelectedValuePath = "Key";

            if (CurrentSchedule.LessonNumber > 0)
                LessonNumberComboBox.SelectedValue = CurrentSchedule.LessonNumber;
            else
                LessonNumberComboBox.SelectedIndex = 0;

            if (CurrentSchedule.LessonDate == default)
                LessonDatePicker.SelectedDate = _weekStart;
            else
                LessonDatePicker.SelectedDate = CurrentSchedule.LessonDate;

            TopicTextBox.Text = CurrentSchedule.Topic ?? string.Empty;
        }

        private void UpdateTimeInfo()
        {
            if (LessonNumberComboBox.SelectedValue is int lessonNumber && _lessonTimes.ContainsKey(lessonNumber))
            {
                var time = _lessonTimes[lessonNumber];
                TimeInfoText.Text = $"Урок будет проходить с {time.Start} до {time.End}";
            }
            else
            {
                TimeInfoText.Text = string.Empty;
            }
        }

        private async void CheckForConflicts()
        {
            if (CurrentSchedule == null || CurrentSchedule.LessonDate == default ||
                CurrentSchedule.LessonNumber <= 0 || !CurrentSchedule.TeacherId.HasValue || _classId <= 0)
            {
                ConflictWarningPanel.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                var date = CurrentSchedule.LessonDate.ToString("yyyy-MM-dd");
                string baseQuery = $"lesson_date=eq.{date}&lesson_number=eq.{CurrentSchedule.LessonNumber}&select=*,classes(name),subjects(name),teachers(full_name)";

                var teacherConflicts = await SupabaseClient.ExecuteQuery("schedule", $"{baseQuery}&teacher_id=eq.{CurrentSchedule.TeacherId}");
                var classConflicts = await SupabaseClient.ExecuteQuery("schedule", $"{baseQuery}&class_id=eq.{_classId}");

                if (CurrentSchedule.Id > 0)
                {
                    teacherConflicts = new JArray(teacherConflicts.Where(t => t?["id"] != null && t["id"].Type != JTokenType.Null && t["id"].Value<int>() != CurrentSchedule.Id));
                    classConflicts = new JArray(classConflicts.Where(c => c?["id"] != null && c["id"].Type != JTokenType.Null && c["id"].Value<int>() != CurrentSchedule.Id));
                }

                var messages = new List<string>();

                if (teacherConflicts.Count > 0)
                {
                    var conflict = teacherConflicts[0];
                    var className = conflict["classes"]?["name"]?.ToString() ?? "неизвестный класс";
                    var subjectName = conflict["subjects"]?["name"]?.ToString() ?? "неизвестный предмет";
                    messages.Add($"Учитель уже ведет урок в классе {className} ({subjectName})");
                }

                if (classConflicts.Count > 0)
                {
                    var conflict = classConflicts[0];
                    var teacherName = conflict["teachers"]?["full_name"]?.ToString() ?? "неизвестный учитель";
                    var subjectName = conflict["subjects"]?["name"]?.ToString() ?? "неизвестный предмет";
                    messages.Add($"В классе уже есть урок с учителем {teacherName} ({subjectName})");
                }

                if (messages.Any())
                {
                    ConflictWarningPanel.Visibility = Visibility.Visible;
                    ConflictWarningText.Text = string.Join(Environment.NewLine, messages);
                }
                else
                {
                    ConflictWarningPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                // Игнорируем ошибки проверки конфликтов
            }
        }

        private void SubjectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SubjectComboBox.SelectedItem is not Subject selectedSubject) return;

            CurrentSchedule.SubjectId = selectedSubject.Id;

            var subjectTeachers = _teachers.Where(t => t.SubjectId.HasValue && t.SubjectId.Value == selectedSubject.Id).ToList();
            TeacherComboBox.ItemsSource = subjectTeachers;

            if (CurrentSchedule.TeacherId.HasValue)
            {
                var existing = subjectTeachers.FirstOrDefault(t => t.Id == CurrentSchedule.TeacherId.Value);
                if (existing != null)
                {
                    TeacherComboBox.SelectedValue = existing.Id;
                }
                else if (subjectTeachers.Any())
                {
                    TeacherComboBox.SelectedIndex = 0;
                    CurrentSchedule.TeacherId = subjectTeachers[0].Id;
                }
                else
                {
                    TeacherComboBox.SelectedIndex = -1;
                    CurrentSchedule.TeacherId = null;
                }
            }
            else if (subjectTeachers.Any())
            {
                TeacherComboBox.SelectedIndex = 0;
                CurrentSchedule.TeacherId = subjectTeachers[0].Id;
            }
            else
            {
                TeacherComboBox.SelectedIndex = -1;
                CurrentSchedule.TeacherId = null;
            }

            CheckForConflicts();
        }

        private bool ValidateForm()
        {
            if (!CurrentSchedule.SubjectId.HasValue || CurrentSchedule.SubjectId.Value <= 0)
            {
                MessageBox.Show("Выберите предмет", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!CurrentSchedule.TeacherId.HasValue || CurrentSchedule.TeacherId.Value <= 0)
            {
                MessageBox.Show("Выберите учителя", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (CurrentSchedule.LessonDate == default)
            {
                MessageBox.Show("Выберите дату урока", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (CurrentSchedule.LessonNumber <= 0)
            {
                MessageBox.Show("Выберите номер урока", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                CurrentSchedule.Topic = string.IsNullOrWhiteSpace(TopicTextBox.Text) ? null : TopicTextBox.Text.Trim();

                bool isNewLesson = CurrentSchedule.Id <= 0;
                JArray result;

                if (!isNewLesson)
                {
                    result = await SupabaseClient.UpdateSchedule(
                        CurrentSchedule.Id,
                        CurrentSchedule.SubjectId!.Value,
                        CurrentSchedule.TeacherId!.Value,
                        CurrentSchedule.LessonDate,
                        CurrentSchedule.LessonNumber,
                        CurrentSchedule.Topic
                    );
                }
                else
                {
                    result = await SupabaseClient.AddSchedule(
                        _classId,
                        CurrentSchedule.SubjectId!.Value,
                        CurrentSchedule.TeacherId!.Value,
                        CurrentSchedule.LessonDate,
                        CurrentSchedule.LessonNumber,
                        CurrentSchedule.Topic
                    );

                    if (result.Count > 0 && result[0]?["id"] != null)
                    {
                        CurrentSchedule.Id = result[0]["id"].Value<int>();
                    }

                    CurrentSchedule.ClassId = _classId;
                }

                SaveCompleted?.Invoke(true, CurrentSchedule);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                SaveCompleted?.Invoke(false, null);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => GoBack?.Invoke();
        private void BackButton_Click(object sender, RoutedEventArgs e) => GoBack?.Invoke();

        private void LessonNumberComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LessonNumberComboBox.SelectedValue is int lessonNumber)
            {
                CurrentSchedule.LessonNumber = lessonNumber;
                UpdateTimeInfo();
                CheckForConflicts();
            }
        }

        private void TeacherComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TeacherComboBox.SelectedValue is int teacherId)
            {
                CurrentSchedule.TeacherId = teacherId;
                CheckForConflicts();
            }
        }

        private void LessonDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LessonDatePicker.SelectedDate.HasValue)
            {
                CurrentSchedule.LessonDate = LessonDatePicker.SelectedDate.Value.Date;
                CheckForConflicts();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

            

    }
}