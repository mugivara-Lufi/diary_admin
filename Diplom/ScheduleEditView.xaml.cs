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
using System.Windows.Media;

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
        private bool _isEditMode;

        public event Action<bool, Schedule> SaveCompleted;
        public event Action GoBack;

        public ScheduleEditView(Schedule schedule = null, int classId = 0, DateTime weekStart = default)
        {
            InitializeComponent();
            DataContext = this;

            _classId = classId;
            _weekStart = weekStart == default ? DateTime.Now.Date : weekStart.Date;
            _isEditMode = schedule != null && schedule.Id > 0;

            _subjects = new ObservableCollection<Subject>();
            _teachers = new ObservableCollection<Teacher>();
            _lessonNumbers = new ObservableCollection<KeyValuePair<int, string>>();

            _lessonTimes = new Dictionary<int, (string Start, string End)>
            {
                { 1, ("8:00", "9:30") },
                { 2, ("9:40", "11:10") },
                { 3, ("11:20", "12:50") },
                { 4, ("13:45", "15:15") },
                { 5, ("15:25", "16:55") },
                { 6, ("17:05", "18:35") },
                { 7, ("18:45", "20:15") }
            };

            for (int i = 1; i <= 7; i++)
            {
                var t = _lessonTimes[i];
                _lessonNumbers.Add(new KeyValuePair<int, string>(i, $"{i} пара ({t.Start} - {t.End})"));
            }

            if (_isEditMode)
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

        public string TitleText => _isEditMode ? "✏️ Редактирование занятия" : "➕ Добавление занятия";
        public ObservableCollection<KeyValuePair<int, string>> LessonNumbers => _lessonNumbers;

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            await LoadClassInfo();
            await LoadTeachersAsync();
            await LoadSubjectsAsync();
            InitializeControls();
            UpdateTimeInfo();
            ApplyDateRestrictions();
            await CheckForConflicts();
        }

        private async System.Threading.Tasks.Task LoadClassInfo()
        {
            try
            {
                var classes = await SupabaseClient.ExecuteQuery("classes", $"id=eq.{_classId}&select=name");
                if (classes.Count > 0)
                    ClassNameText.Text = classes[0]["name"]?.ToString() ?? "Неизвестная группа";
                else
                    ClassNameText.Text = "Неизвестная группа";
            }
            catch (Exception ex)
            {
                ClassNameText.Text = "Неизвестная группа";
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
                MessageBox.Show($"Ошибка загрузки дисциплин: {ex.Message}", "Ошибка",
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
                MessageBox.Show($"Ошибка загрузки преподавателей: {ex.Message}", "Ошибка",
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

        private void ApplyDateRestrictions()
        {
            try
            {
                var today = DateTime.Now.Date;

                // Очищаем существующие ограничения
                LessonDatePicker.BlackoutDates.Clear();
                LessonDatePicker.DisplayDateStart = null;
                LessonDatePicker.DisplayDateEnd = null;

                if (!_isEditMode)
                {
                    // Для новых занятий - можно выбирать только от сегодня и далее
                    LessonDatePicker.DisplayDateStart = today;

                    // Блокируем все воскресенья
                    BlockSundays(today, today.AddMonths(6));
                }
                else
                {
                    // Для редактирования - можно выбирать только от завтра и далее
                    LessonDatePicker.DisplayDateStart = today.AddDays(1);

                    // Блокируем все воскресенья
                    BlockSundays(today, today.AddMonths(6));

                    // Если редактируемое занятие на прошедшую дату, показываем предупреждение
                    if (CurrentSchedule.LessonDate < today)
                    {
                        ShowPastDateWarning();
                    }
                }

                // Если выбранная дата стала недоступной, сбрасываем её
                if (LessonDatePicker.SelectedDate.HasValue)
                {
                    var selectedDate = LessonDatePicker.SelectedDate.Value.Date;
                    if (LessonDatePicker.DisplayDateStart.HasValue && selectedDate < LessonDatePicker.DisplayDateStart.Value)
                    {
                        LessonDatePicker.SelectedDate = LessonDatePicker.DisplayDateStart.Value;
                        CurrentSchedule.LessonDate = LessonDatePicker.DisplayDateStart.Value;
                    }

                    if (selectedDate.DayOfWeek == DayOfWeek.Sunday)
                    {
                        // Находим следующий доступный день (понедельник)
                        var nextAvailable = LessonDatePicker.DisplayDateStart ?? today;
                        while (nextAvailable.DayOfWeek == DayOfWeek.Sunday)
                        {
                            nextAvailable = nextAvailable.AddDays(1);
                        }
                        LessonDatePicker.SelectedDate = nextAvailable;
                        CurrentSchedule.LessonDate = nextAvailable;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ApplyDateRestrictions: {ex.Message}");
            }
        }

        private void BlockSundays(DateTime startDate, DateTime endDate)
        {
            try
            {
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    if (date.DayOfWeek == DayOfWeek.Sunday)
                    {
                        LessonDatePicker.BlackoutDates.Add(new CalendarDateRange(date, date));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error blocking Sundays: {ex.Message}");
            }
        }

        private void ShowPastDateWarning()
        {
            // Проверяем, не добавлено ли уже предупреждение
            var parent = LessonDatePicker.Parent as StackPanel;
            if (parent != null && parent.Children.OfType<Border>().Any(b => b.Name == "PastDateWarning"))
                return;

            var warningBorder = new Border
            {
                Name = "PastDateWarning",
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF2F2")),
                Padding = new Thickness(15),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 10, 0, 20)
            };

            var warningText = new TextBlock
            {
                Text = "⚠️ Внимание! Вы редактируете занятие на прошедшую дату.\n" +
                       "Изменения могут повлиять на уже выставленные оценки и посещаемость.",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")),
                TextWrapping = TextWrapping.Wrap
            };

            warningBorder.Child = warningText;

            // Вставляем предупреждение в нужное место
            if (parent != null)
            {
                int index = parent.Children.IndexOf(LessonDatePicker);
                if (index >= 0)
                {
                    parent.Children.Insert(index + 1, warningBorder);
                }
            }
        }

        private void UpdateTimeInfo()
        {
            if (LessonNumberComboBox.SelectedValue is int lessonNumber && _lessonTimes.ContainsKey(lessonNumber))
            {
                var time = _lessonTimes[lessonNumber];
                TimeInfoText.Text = $"Занятие будет проходить с {time.Start} до {time.End}";
            }
            else
            {
                TimeInfoText.Text = string.Empty;
            }
        }

        private async System.Threading.Tasks.Task CheckForConflicts()
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
                var messages = new List<string>();

                // Ограничение: Нельзя ставить занятия в воскресенье
                if (CurrentSchedule.LessonDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    messages.Add("❌ Занятия по воскресеньям не проводятся. Выберите другой день.");
                    ConflictWarningPanel.Visibility = Visibility.Visible;
                    ConflictWarningText.Text = string.Join(Environment.NewLine, messages);
                    return;
                }

                // Ограничение: Нельзя ставить одного и того же преподавателя на занятия в одно время у разных групп
                string baseQuery = $"lesson_date=eq.{date}&lesson_number=eq.{CurrentSchedule.LessonNumber}&select=*,classes(name),subjects(name),teachers(full_name)";
                var teacherConflicts = await SupabaseClient.ExecuteQuery("schedule", $"{baseQuery}&teacher_id=eq.{CurrentSchedule.TeacherId}");

                if (_isEditMode && CurrentSchedule.Id > 0)
                {
                    teacherConflicts = new JArray(teacherConflicts.Where(t =>
                        t?["id"] != null &&
                        t["id"].Type != JTokenType.Null &&
                        t["id"].Value<int>() != CurrentSchedule.Id));
                }

                if (teacherConflicts.Count > 0)
                {
                    foreach (var conflict in teacherConflicts)
                    {
                        var className = conflict["classes"]?["name"]?.ToString() ?? "неизвестная группа";
                        var subjectName = conflict["subjects"]?["name"]?.ToString() ?? "неизвестная дисциплина";
                        messages.Add($"⚠️ Преподаватель уже ведет занятие в это же время в группе {className} ({subjectName})");
                    }
                }

                // Проверка на занятость группы в это время
                var classConflicts = await SupabaseClient.ExecuteQuery("schedule", $"{baseQuery}&class_id=eq.{_classId}");

                if (_isEditMode && CurrentSchedule.Id > 0)
                {
                    classConflicts = new JArray(classConflicts.Where(c =>
                        c?["id"] != null &&
                        c["id"].Type != JTokenType.Null &&
                        c["id"].Value<int>() != CurrentSchedule.Id));
                }

                if (classConflicts.Count > 0)
                {
                    var conflict = classConflicts[0];
                    var teacherName = conflict["teachers"]?["full_name"]?.ToString() ?? "неизвестный преподаватель";
                    var subjectName = conflict["subjects"]?["name"]?.ToString() ?? "неизвестная дисциплина";
                    messages.Add($"⚠️ В это время у группы уже есть занятие с преподавателем {teacherName} ({subjectName})");
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
                System.Diagnostics.Debug.WriteLine($"Conflict check error: {ex.Message}");
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

            _ = CheckForConflicts();
        }

        private bool ValidateForm()
        {
            var today = DateTime.Now.Date;

            // Проверка на дату
            if (CurrentSchedule.LessonDate == default)
            {
                MessageBox.Show("Выберите дату занятия", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Ограничение: Нельзя добавлять занятия на прошедшие даты
            if (!_isEditMode && CurrentSchedule.LessonDate < today)
            {
                MessageBox.Show("❌ Нельзя добавлять занятия на прошедшие даты.\nВыберите дату от сегодняшнего дня и позже.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Ограничение: Нельзя редактировать занятия на текущую дату (для режима редактирования)
            if (_isEditMode && CurrentSchedule.LessonDate == today)
            {
                var result = MessageBox.Show(
                    "⚠️ Вы редактируете занятие на сегодняшнюю дату.\n" +
                    "Изменения могут повлиять на текущий учебный процесс.\n\n" +
                    "Вы уверены, что хотите продолжить?",
                    "Предупреждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return false;
            }

            // Ограничение: Нельзя ставить занятия в воскресенье
            if (CurrentSchedule.LessonDate.DayOfWeek == DayOfWeek.Sunday)
            {
                MessageBox.Show("❌ Занятия по воскресеньям не проводятся.\nВыберите другой день недели (понедельник-суббота).",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!CurrentSchedule.SubjectId.HasValue || CurrentSchedule.SubjectId.Value <= 0)
            {
                MessageBox.Show("Выберите дисциплину", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!CurrentSchedule.TeacherId.HasValue || CurrentSchedule.TeacherId.Value <= 0)
            {
                MessageBox.Show("Выберите преподавателя", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (CurrentSchedule.LessonNumber <= 0)
            {
                MessageBox.Show("Выберите номер занятия", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Проверка на конфликты перед сохранением
            if (ConflictWarningPanel.Visibility == Visibility.Visible)
            {
                var result = MessageBox.Show(
                    "⚠️ Обнаружены конфликты в расписании!\n\n" +
                    ConflictWarningText.Text + "\n\n" +
                    "Вы уверены, что хотите сохранить занятие?",
                    "Конфликт расписания",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                return result == MessageBoxResult.Yes;
            }

            return true;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                CurrentSchedule.Topic = string.IsNullOrWhiteSpace(TopicTextBox.Text) ? null : TopicTextBox.Text.Trim();

                bool isNewLesson = !_isEditMode;
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

                var successMessage = isNewLesson ? "Занятие успешно добавлено!" : "Занятие успешно обновлено!";
                MessageBox.Show(successMessage, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

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
                _ = CheckForConflicts();
            }
        }

        private void TeacherComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TeacherComboBox.SelectedValue is int teacherId)
            {
                CurrentSchedule.TeacherId = teacherId;
                _ = CheckForConflicts();
            }
        }

        private void LessonDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LessonDatePicker.SelectedDate.HasValue)
            {
                var selectedDate = LessonDatePicker.SelectedDate.Value.Date;

                // Дополнительная проверка при выборе даты
                if (selectedDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    MessageBox.Show("❌ Занятия по воскресеньям не проводятся. Выберите другой день.",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LessonDatePicker.SelectedDate = CurrentSchedule.LessonDate;
                    return;
                }

                CurrentSchedule.LessonDate = selectedDate;
                _ = CheckForConflicts();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}