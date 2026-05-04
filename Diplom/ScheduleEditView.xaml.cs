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

        // Замените метод LoadSubjectsAsync и LoadTeachersAsync

        private async System.Threading.Tasks.Task LoadSubjectsAsync()
        {
            try
            {
                // Получаем текущий семестр группы
                int semester = await SupabaseClient.GetCurrentSemester(_classId);

                // Получаем предметы только из учебного плана
                var result = await SupabaseClient.GetSubjectsByCurriculum(_classId, semester);
                _subjects.Clear();

                if (result == null || result.Count == 0)
                {
                    // Если нет учебного плана, показываем сообщение
                    MessageBox.Show("Для этой группы не настроен учебный план. Сначала добавьте предметы в учебный план.",
                        "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SubjectComboBox.IsEnabled = false;
                    TeacherComboBox.IsEnabled = false;
                    return;
                }

                SubjectComboBox.IsEnabled = true;

                foreach (var item in result)
                {
                    var subjectToken = item["subjects"];
                    if (subjectToken == null || subjectToken.Type == JTokenType.Null) continue;

                    int id = subjectToken["id"]?.Value<int>() ?? 0;
                    if (id <= 0) continue;

                    var subject = new Subject
                    {
                        Id = id,
                        Name = subjectToken["name"]?.ToString() ?? "Не указано",
                        // Сохраняем curriculum_subject_id для доп. информации
                        TotalHours = item["total_hours"]?.Value<int>() ?? 0,
                        HoursPerWeek = item["hours_per_week"]?.Value<int>() ?? 0
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
                else if (_subjects.Any())
                {
                    SubjectComboBox.SelectedIndex = 0;
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
                _teachers.Clear();

                JArray teachersResult = null;
                int semester = 1;

                try
                {
                    semester = await SupabaseClient.GetCurrentSemester(_classId);
                    // Попытка загрузить через учебный план
                    teachersResult = await SupabaseClient.GetTeachersByCurriculum(_classId, semester);
                }
                catch
                {
                    // fallback
                }

                // 👇 ДИАГНОСТИКА — покажет, что реально вернул API
                System.Diagnostics.Debug.WriteLine($"[DEBUG] GetTeachersByCurriculum returned: {(teachersResult == null ? "null" : teachersResult.ToString())}");

                // Если не получили через учебный план — загружаем всех преподавателей
                if (teachersResult == null || teachersResult.Count == 0)
                {
                    teachersResult = await SupabaseClient.ExecuteQuery("teachers", "select=*");
                }

                var uniqueTeachers = new Dictionary<int, Teacher>();

                foreach (var item in teachersResult)
                {
                    // Обрабатываем оба случая: плоская структура или вложенная
                    JToken teacherToken = null;

                    if (item["teachers"] != null && item["teachers"].Type != JTokenType.Null)
                        teacherToken = item["teachers"];
                    else if (item["full_name"] != null)
                        teacherToken = item;

                    if (teacherToken == null) continue;

                    int id = teacherToken["id"]?.Value<int>() ?? 0;
                    if (id <= 0) continue;

                    if (!uniqueTeachers.ContainsKey(id))
                    {
                        uniqueTeachers.Add(id, new Teacher
                        {
                            Id = id,
                            FullName = teacherToken["full_name"]?.ToString() ?? "Не указано",
                            SubjectId = teacherToken["subject_id"]?.Value<int?>(),
                            Email = teacherToken["email"]?.ToString() ?? ""
                        });
                    }
                }

                foreach (var teacher in uniqueTeachers.Values)
                    _teachers.Add(teacher);

                TeacherComboBox.DisplayMemberPath = "FullName";
                TeacherComboBox.SelectedValuePath = "Id";
                TeacherComboBox.ItemsSource = _teachers;

                if (CurrentSchedule.TeacherId.HasValue && CurrentSchedule.TeacherId.Value > 0)
                {
                    var exists = _teachers.Any(t => t.Id == CurrentSchedule.TeacherId.Value);
                    if (exists)
                        TeacherComboBox.SelectedValue = CurrentSchedule.TeacherId.Value;
                    else
                        CurrentSchedule.TeacherId = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] LoadTeachers: {ex}");
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

        private async void SubjectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SubjectComboBox.SelectedItem is not Subject selectedSubject) return;

            CurrentSchedule.SubjectId = selectedSubject.Id;

            // Показываем информацию из учебного плана
            await UpdateSubjectInfo(selectedSubject);

            // Фильтруем преподавателей, которые закреплены за выбранным предметом в учебном плане
            var filteredTeachers = _teachers.Where(t => t.SubjectId == selectedSubject.Id).ToList();

            // Если нет преподавателей по предмету, показываем всех из плана
            if (filteredTeachers.Any())
            {
                TeacherComboBox.ItemsSource = filteredTeachers;
            }
            else
            {
                TeacherComboBox.ItemsSource = _teachers;
            }

            if (CurrentSchedule.TeacherId.HasValue)
            {
                var existing = TeacherComboBox.ItemsSource.Cast<Teacher>()
                    .FirstOrDefault(t => t.Id == CurrentSchedule.TeacherId.Value);

                if (existing != null)
                {
                    TeacherComboBox.SelectedValue = existing.Id;
                }
                else if (TeacherComboBox.ItemsSource.Cast<Teacher>().Any())
                {
                    TeacherComboBox.SelectedIndex = 0;
                    CurrentSchedule.TeacherId = (TeacherComboBox.SelectedItem as Teacher)?.Id;
                }
                else
                {
                    TeacherComboBox.SelectedIndex = -1;
                    CurrentSchedule.TeacherId = null;
                }
            }
            else if (TeacherComboBox.ItemsSource.Cast<Teacher>().Any())
            {
                TeacherComboBox.SelectedIndex = 0;
                CurrentSchedule.TeacherId = (TeacherComboBox.SelectedItem as Teacher)?.Id;
            }
            else
            {
                TeacherComboBox.SelectedIndex = -1;
                CurrentSchedule.TeacherId = null;
            }

            _ = CheckForConflicts();
        }

        /// <summary>
        /// Обновляет информационную панель с данными из учебного плана
        /// </summary>
        private async System.Threading.Tasks.Task UpdateSubjectInfo(Subject selectedSubject)
        {
            try
            {
                int semester = await SupabaseClient.GetCurrentSemester(_classId);

                // Получаем данные из curriculum_subjects для выбранного предмета
                var curricula = await SupabaseClient.ExecuteQuery("curricula",
                    $"class_id=eq.{_classId}&is_current=eq.true&select=id");

                if (curricula == null || curricula.Count == 0)
                {
                    SubjectInfoPanel.Visibility = Visibility.Collapsed;
                    return;
                }

                int curriculumId = curricula[0]["id"].Value<int>();

                var curriculumSubjects = await SupabaseClient.ExecuteQuery("curriculum_subjects",
                    $"curriculum_id=eq.{curriculumId}&semester=eq.{semester}&subject_id=eq.{selectedSubject.Id}&select=*");

                if (curriculumSubjects == null || curriculumSubjects.Count == 0)
                {
                    SubjectInfoPanel.Visibility = Visibility.Collapsed;
                    return;
                }

                var curriculumSubject = curriculumSubjects[0];
                int totalHours = curriculumSubject["total_hours"]?.Value<int>() ?? 0;
                int hoursPerWeek = curriculumSubject["hours_per_week"]?.Value<int>() ?? 0;
                string attestationType = curriculumSubject["attestation_type"]?.ToString() ?? "зачёт";

                // Считаем, сколько занятий уже поставлено по этому предмету для группы в этом семестре
                int usedHours = await GetUsedHoursCount(selectedSubject.Id, semester);
                int remainingHours = Math.Max(0, totalHours - usedHours * 2); // каждое занятие = 2 часа (пара)
                int usedPairs = usedHours;
                int totalPairs = totalHours / 2;
                int remainingPairs = Math.Max(0, totalPairs - usedPairs);

                // Формируем информационное сообщение
                var infoLines = new List<string>();

                // Прогресс-бар (текстовый)
                double progressPercent = totalPairs > 0 ? Math.Min(100.0, (double)usedPairs / totalPairs * 100) : 0;
                string progressBar = GetProgressBar(progressPercent);

                infoLines.Add($"📚 {selectedSubject.Name}");
                infoLines.Add($"📋 План: {totalHours} часов ({totalPairs} пар) • {hoursPerWeek} ч/нед");
                infoLines.Add($"✅ Проведено: {usedPairs} пар ({usedHours} часов)");
                infoLines.Add($"⏳ Осталось: {remainingPairs} пар ({remainingHours} часов)");
                infoLines.Add($"📊 Прогресс: {progressBar} {progressPercent:F0}%");
                infoLines.Add($"📝 Аттестация: {GetAttestationTypeName(attestationType)}");

                // Предупреждение, если часов почти не осталось
                if (remainingPairs <= 2 && totalPairs > 0)
                {
                    infoLines.Add($"⚠️ Внимание! Осталось мало занятий по плану.");
                }

                // Предупреждение, если превышен план
                if (usedPairs > totalPairs && totalPairs > 0)
                {
                    infoLines.Add($"⚠️ Превышение плана на {usedPairs - totalPairs} пар!");
                    SubjectInfoPanel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0"));
                }
                else
                {
                    SubjectInfoPanel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9"));
                }

                SubjectHoursText.Text = string.Join("\n", infoLines);
                SubjectInfoPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating subject info: {ex.Message}");
                SubjectInfoPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Подсчитывает количество уже проведенных занятий по предмету в этом семестре
        /// </summary>
        private async System.Threading.Tasks.Task<int> GetUsedHoursCount(int subjectId, int semester)
        {
            try
            {
                // Определяем даты начала семестра
                DateTime semesterStart;
                DateTime semesterEnd;

                if (semester == 1)
                {
                    // 1 семестр: сентябрь - январь
                    semesterStart = new DateTime(DateTime.Now.Year, 9, 1);
                    if (DateTime.Now.Month < 9)
                        semesterStart = new DateTime(DateTime.Now.Year - 1, 9, 1);
                    semesterEnd = new DateTime(semesterStart.Year + 1, 1, 31);
                }
                else
                {
                    // 2 семестр: февраль - июнь
                    semesterStart = new DateTime(DateTime.Now.Year, 2, 1);
                    semesterEnd = new DateTime(DateTime.Now.Year, 6, 30);
                }

                string dateFrom = semesterStart.ToString("yyyy-MM-dd");
                string dateTo = semesterEnd.ToString("yyyy-MM-dd");

                // Считаем количество записей в расписании для этого предмета и класса
                var scheduleCount = await SupabaseClient.ExecuteQuery("schedule",
                    $"class_id=eq.{_classId}&subject_id=eq.{subjectId}&lesson_date=gte.{dateFrom}&lesson_date=lte.{dateTo}&select=id");

                return scheduleCount?.Count ?? 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error counting used hours: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Создает текстовый прогресс-бар
        /// </summary>
        private string GetProgressBar(double percent, int length = 10)
        {
            int filledBlocks = (int)Math.Round(percent / 100 * length);
            int emptyBlocks = length - filledBlocks;

            string filled = new string('█', filledBlocks);
            string empty = new string('░', emptyBlocks);

            return $"[{filled}{empty}]";
        }

        /// <summary>
        /// Возвращает читаемое название типа аттестации
        /// </summary>
        private string GetAttestationTypeName(string type)
        {
            return type?.ToLower() switch
            {
                "credit" => "Зачёт",
                "exam" => "Экзамен",
                "diff_credit" => "Дифф. зачёт",
                "coursework" => "Курсовая работа",
                _ => type ?? "Не указано"
            };
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