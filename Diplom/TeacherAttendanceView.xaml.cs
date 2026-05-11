using Diplom.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static Diplom.SupabaseClient;

namespace Diplom
{
    public partial class TeacherAttendanceView : UserControl
    {
        private int _teacherId;
        private ObservableCollection<AttendanceStudentViewModel> _students;
        private ObservableCollection<ScheduleLessonDate> _lessonDates;
        private int _selectedGroupId;
        private int _selectedSubjectId;
        private DateTime? _selectedLessonDate;

        public TeacherAttendanceView()
        {
            InitializeComponent();
            _students = new ObservableCollection<AttendanceStudentViewModel>();
            _lessonDates = new ObservableCollection<ScheduleLessonDate>();
            AttendanceGrid.ItemsSource = _students;
            CurrentDateText.Text = DateTime.Now.ToString("dd MMMM yyyy");

            Loaded += async (s, e) => await InitializeAsync();
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            await LoadTeacherProfile();
            await LoadGroupsAsync();
        }

        private async System.Threading.Tasks.Task LoadTeacherProfile()
        {
            try
            {
                if (AuthService.CurrentTeacher != null)
                {
                    _teacherId = AuthService.CurrentTeacher.Id;
                }
                else if (AuthService.CurrentUser != null)
                {
                    int userId = AuthService.CurrentUser["id"].Value<int>();
                    var result = await SupabaseClient.ExecuteQuery("teachers", $"user_id=eq.{userId}&select=id");
                    if (result != null && result.Count > 0)
                    {
                        _teacherId = result[0]["id"].Value<int>();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки профиля: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadGroupsAsync()
        {
            try
            {
                var result = await SupabaseClient.ExecuteQuery("schedule",
                    $"teacher_id=eq.{_teacherId}&select=class_id,classes(name)");

                var uniqueGroups = new Dictionary<int, string>();
                foreach (var item in result)
                {
                    var classId = item["class_id"]?.Value<int>();
                    var className = item["classes"]?["name"]?.ToString();
                    if (classId.HasValue && !uniqueGroups.ContainsKey(classId.Value) && !string.IsNullOrEmpty(className))
                    {
                        uniqueGroups.Add(classId.Value, className);
                    }
                }

                var groups = uniqueGroups.Select(g => new Class { Id = g.Key, Name = g.Value }).ToList();
                GroupComboBox.ItemsSource = groups;

                if (groups.Count > 0)
                {
                    GroupComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки групп: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupComboBox.SelectedItem is Class selectedGroup)
            {
                _selectedGroupId = selectedGroup.Id;
                await LoadSubjectsAsync(selectedGroup.Id);
            }
        }

        private async System.Threading.Tasks.Task LoadSubjectsAsync(int groupId)
        {
            try
            {
                var result = await SupabaseClient.ExecuteQuery("schedule",
                    $"teacher_id=eq.{_teacherId}&class_id=eq.{groupId}&select=subject_id,subjects(name)");

                var uniqueSubjects = new Dictionary<int, string>();
                foreach (var item in result)
                {
                    var subjectId = item["subject_id"]?.Value<int>();
                    var subjectName = item["subjects"]?["name"]?.ToString();
                    if (subjectId.HasValue && !uniqueSubjects.ContainsKey(subjectId.Value) && !string.IsNullOrEmpty(subjectName))
                    {
                        uniqueSubjects.Add(subjectId.Value, subjectName);
                    }
                }

                var subjects = uniqueSubjects.Select(s => new Subject { Id = s.Key, Name = s.Value }).ToList();
                SubjectComboBox.ItemsSource = subjects;
                SubjectComboBox.IsEnabled = subjects.Count > 0;

                if (subjects.Count > 0)
                {
                    SubjectComboBox.SelectedIndex = 0;
                }
                else
                {
                    DatePickerComboBox.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки дисциплин: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SubjectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SubjectComboBox.SelectedItem is Subject selectedSubject)
            {
                _selectedSubjectId = selectedSubject.Id;
                await LoadLessonDatesAsync();
            }
        }

        private async System.Threading.Tasks.Task LoadLessonDatesAsync()
        {
            if (_selectedGroupId == 0 || _selectedSubjectId == 0) return;

            try
            {
                // Получаем фактические даты занятий из расписания
                var result = await SupabaseClient.ExecuteQuery("schedule",
                    $"teacher_id=eq.{_teacherId}&class_id=eq.{_selectedGroupId}&subject_id=eq.{_selectedSubjectId}&select=lesson_date&order=lesson_date");

                _lessonDates.Clear();

                foreach (var item in result)
                {
                    if (DateTime.TryParse(item["lesson_date"]?.ToString(), out DateTime date))
                    {
                        _lessonDates.Add(new ScheduleLessonDate
                        {
                            Date = date,
                            DisplayText = $"{date:dd.MM.yyyy} ({GetDayOfWeekAbbreviation(date)})"
                        });
                    }
                }

                DatePickerComboBox.ItemsSource = _lessonDates;
                DatePickerComboBox.DisplayMemberPath = "DisplayText";
                DatePickerComboBox.SelectedValuePath = "Date";
                DatePickerComboBox.IsEnabled = _lessonDates.Count > 0;

                if (_lessonDates.Count > 0)
                {
                    DatePickerComboBox.SelectedIndex = 0;
                }
                else
                {
                    StatusText.Text = "Нет занятий по выбранной дисциплине";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки дат занятий: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DatePickerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DatePickerComboBox.SelectedItem is ScheduleLessonDate selectedDate)
            {
                _selectedLessonDate = selectedDate.Date;

                // Проверка, можно ли редактировать прошедшие даты
                if (_selectedLessonDate.Value.Date < DateTime.Now.Date)
                {
                    CanEditWarning.Visibility = Visibility.Visible;
                }
                else
                {
                    CanEditWarning.Visibility = Visibility.Collapsed;
                }

                await LoadAttendanceAsync();
            }
        }

        private string GetDayOfWeekAbbreviation(DateTime date)
        {
            return date.DayOfWeek switch
            {
                DayOfWeek.Monday => "Пн",
                DayOfWeek.Tuesday => "Вт",
                DayOfWeek.Wednesday => "Ср",
                DayOfWeek.Thursday => "Чт",
                DayOfWeek.Friday => "Пт",
                DayOfWeek.Saturday => "Сб",
                DayOfWeek.Sunday => "Вс",
                _ => ""
            };
        }

        private async System.Threading.Tasks.Task LoadAttendanceAsync()
        {
            if (_selectedGroupId == 0 || _selectedSubjectId == 0 || !_selectedLessonDate.HasValue) return;

            try
            {
                var date = _selectedLessonDate.Value.ToString("yyyy-MM-dd");

                // Загружаем студентов группы
                var studentsResult = await SupabaseClient.ExecuteQuery("students",
                    $"class_id=eq.{_selectedGroupId}&select=id,full_name&order=full_name");

                // Загружаем существующие записи посещаемости
                var attendanceResult = await SupabaseClient.ExecuteQuery("attendance",
                    $"date=eq.{date}&subject_id=eq.{_selectedSubjectId}&select=*");

                var attendanceDict = new Dictionary<int, (bool Present, string Comment)>();
                foreach (var item in attendanceResult)
                {
                    int studentId = item["student_id"].Value<int>();
                    bool present = item["present"].Value<bool>();
                    string comment = item["comment"]?.ToString() ?? "";
                    attendanceDict[studentId] = (present, comment);
                }

                _students.Clear();
                int number = 1;

                foreach (var student in studentsResult)
                {
                    int studentId = student["id"].Value<int>();
                    bool isPresent = attendanceDict.ContainsKey(studentId) && attendanceDict[studentId].Present;
                    string comment = attendanceDict.ContainsKey(studentId) ? attendanceDict[studentId].Comment : "";

                    _students.Add(new AttendanceStudentViewModel
                    {
                        Id = studentId,
                        Number = number++,
                        StudentName = student["full_name"]?.ToString() ?? "",
                        IsPresent = isPresent,
                        IsAbsent = !isPresent,
                        Comment = comment,
                        CanEdit = _selectedLessonDate.Value.Date <= DateTime.Now.Date // Можно редактировать прошлые и сегодня, но не будущие
                    });
                }

                UpdateStatistics();

                if (_selectedLessonDate.Value.Date > DateTime.Now.Date)
                {
                    StatusText.Text = $"❌ Нельзя выставлять посещаемость на будущие даты ({_selectedLessonDate.Value:dd.MM.yyyy})";
                    SaveButton.IsEnabled = false;
                }
                else
                {
                    StatusText.Text = $"Данные за {_selectedLessonDate.Value:dd.MM.yyyy} загружены";
                    SaveButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateStatistics()
        {
            int presentCount = _students.Count(s => s.IsPresent);
            int absentCount = _students.Count - presentCount;
            StatisticsText.Text = $"👥 Всего: {_students.Count} | ✅ Присутствуют: {presentCount} | ❌ Отсутствуют: {absentCount}";
        }

        private void AttendanceRadio_Checked(object sender, RoutedEventArgs e)
        {
            var radio = sender as RadioButton;
            if (radio?.DataContext is AttendanceStudentViewModel student)
            {
                if (!student.CanEdit)
                {
                    MessageBox.Show("Нельзя изменять посещаемость на будущие даты", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool isPresent = radio.Tag?.ToString() == "present";
                student.IsPresent = isPresent;
                student.IsAbsent = !isPresent;
                UpdateStatistics();
            }
        }

        private void MarkAllPresent_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLessonDate.HasValue && _selectedLessonDate.Value.Date > DateTime.Now.Date)
            {
                MessageBox.Show("Нельзя изменять посещаемость на будущие даты", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var student in _students)
            {
                if (student.CanEdit)
                {
                    student.IsPresent = true;
                    student.IsAbsent = false;
                }
            }
            UpdateStatistics();
            StatusText.Text = "Все студенты отмечены как присутствующие";
        }

        private void MarkAllAbsent_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLessonDate.HasValue && _selectedLessonDate.Value.Date > DateTime.Now.Date)
            {
                MessageBox.Show("Нельзя изменять посещаемость на будущие даты", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var student in _students)
            {
                if (student.CanEdit)
                {
                    student.IsPresent = false;
                    student.IsAbsent = true;
                }
            }
            UpdateStatistics();
            StatusText.Text = "Все студенты отмечены как отсутствующие";
        }

        private async void SaveAttendance_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGroupId == 0 || _selectedSubjectId == 0 || !_selectedLessonDate.HasValue)
            {
                MessageBox.Show("Выберите группу, дисциплину и дату занятия", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selectedLessonDate.Value.Date > DateTime.Now.Date)
            {
                MessageBox.Show("Нельзя сохранять посещаемость на будущие даты", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SaveButton.IsEnabled = false;
                SaveButton.Content = "Сохранение...";

                var date = _selectedLessonDate.Value.ToString("yyyy-MM-dd");

                foreach (var student in _students)
                {
                    // Проверяем, есть ли уже запись
                    var existing = await SupabaseClient.ExecuteQuery("attendance",
                        $"student_id=eq.{student.Id}&subject_id=eq.{_selectedSubjectId}&date=eq.{date}&select=id");

                    if (existing.Count > 0)
                    {
                        // Обновляем существующую запись
                        int attendanceId = existing[0]["id"].Value<int>();
                        await SupabaseClient.Update("attendance", $"id=eq.{attendanceId}",
                            new { present = student.IsPresent, comment = student.Comment ?? "" });
                    }
                    else
                    {
                        // Создаем новую запись
                        await SupabaseClient.MarkAttendance(
                            student.Id,
                            _selectedSubjectId,
                            student.IsPresent,
                            student.Comment
                        );
                    }
                }

                StatusText.Text = $"✅ Посещаемость за {_selectedLessonDate.Value:dd.MM.yyyy} сохранена!";
                await LoadAttendanceAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "❌ Ошибка сохранения";
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveButton.Content = "💾 Сохранить";
            }
        }
    }

    public class ScheduleLessonDate
    {
        public DateTime Date { get; set; }
        public string DisplayText { get; set; }
    }

    public class AttendanceStudentViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isPresent;
        private bool _isAbsent;
        private string _comment;

        public int Id { get; set; }
        public int Number { get; set; }
        public string StudentName { get; set; }
        public bool CanEdit { get; set; } = true;

        public bool IsPresent
        {
            get => _isPresent;
            set
            {
                _isPresent = value;
                OnPropertyChanged(nameof(IsPresent));
                if (value) IsAbsent = false;
            }
        }

        public bool IsAbsent
        {
            get => _isAbsent;
            set
            {
                _isAbsent = value;
                OnPropertyChanged(nameof(IsAbsent));
                if (value) IsPresent = false;
            }
        }

        public string Comment
        {
            get => _comment;
            set
            {
                _comment = value;
                OnPropertyChanged(nameof(Comment));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}