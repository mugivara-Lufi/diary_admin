using Diplom.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static Diplom.SupabaseClient;

namespace Diplom
{
    public partial class TeacherScheduleView : UserControl
    {
        private int _teacherId;
        private DateTime _currentWeekStart;
        private List<Schedule> _allSchedule;
        private Schedule _selectedLesson;

        public TeacherScheduleView()
        {
            InitializeComponent();

            _currentWeekStart = GetStartOfWeek(DateTime.Now);
            _allSchedule = new List<Schedule>();

            Loaded += async (s, e) => await LoadScheduleAsync();
        }

        private DateTime GetStartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
        }

        private void UpdateWeekDisplay()
        {
            var monday = _currentWeekStart;
            WeekText.Text = $"{monday:dd.MM.yyyy} - {monday.AddDays(5):dd.MM.yyyy}";

            MondayDate.Text = monday.ToString("dd.MM");
            TuesdayDate.Text = monday.AddDays(1).ToString("dd.MM");
            WednesdayDate.Text = monday.AddDays(2).ToString("dd.MM");
            ThursdayDate.Text = monday.AddDays(3).ToString("dd.MM");
            FridayDate.Text = monday.AddDays(4).ToString("dd.MM");
            SaturdayDate.Text = monday.AddDays(5).ToString("dd.MM");
        }

        private async System.Threading.Tasks.Task LoadScheduleAsync()
        {
            try
            {
                // Получаем ID преподавателя
                if (AuthService.CurrentTeacher != null)
                {
                    _teacherId = AuthService.CurrentTeacher.Id;
                }
                else
                {
                    await LoadTeacherProfile();
                }

                if (_teacherId == 0) return;

                var monday = _currentWeekStart.ToString("yyyy-MM-dd");
                var saturday = _currentWeekStart.AddDays(5).ToString("yyyy-MM-dd");

                var result = await SupabaseClient.ExecuteQuery("schedule",
                    $"teacher_id=eq.{_teacherId}&lesson_date=gte.{monday}&lesson_date=lte.{saturday}&select=*,classes(name),subjects(name)&order=lesson_date,lesson_number");

                _allSchedule.Clear();

                if (result != null && result.Count > 0)
                {
                    foreach (var item in result)
                    {
                        if (item.Type != JTokenType.Object) continue;

                        var schedule = new Schedule
                        {
                            Id = item["id"]?.Value<int>() ?? 0,
                            ClassId = item["class_id"]?.Value<int>() ?? 0,
                            SubjectId = item["subject_id"]?.Value<int>() ?? 0,
                            LessonNumber = item["lesson_number"]?.Value<int>() ?? 0,
                            Topic = item["topic"]?.ToString()
                        };

                        if (DateTime.TryParse(item["lesson_date"]?.ToString(), out DateTime date))
                            schedule.LessonDate = date;

                        var classToken = item["classes"];
                        if (classToken?.Type == JTokenType.Object)
                            schedule.ClassName = classToken["name"]?.ToString() ?? "Не указано";

                        var subjectToken = item["subjects"];
                        if (subjectToken?.Type == JTokenType.Object)
                            schedule.SubjectName = subjectToken["name"]?.ToString() ?? "Не указано";

                        if (schedule.Id > 0)
                            _allSchedule.Add(schedule);
                    }
                }

                RenderSchedule();
                UpdateWeekDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки расписания: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadTeacherProfile()
        {
            try
            {
                if (AuthService.CurrentUser != null)
                {
                    int userId = AuthService.CurrentUser["id"].Value<int>();
                    var result = await SupabaseClient.ExecuteQuery("teachers", $"user_id=eq.{userId}&select=id");

                    if (result != null && result.Count > 0)
                    {
                        _teacherId = result[0]["id"]?.Value<int>() ?? 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки профиля: {ex.Message}");
            }
        }

        private void RenderSchedule()
        {
            // Очищаем существующие ячейки с уроками
            var toRemove = new List<UIElement>();
            foreach (UIElement child in ScheduleGrid.Children)
            {
                if (child is Border b && b.Name != null && b.Name.StartsWith("Lesson_"))
                    toRemove.Add(child);
            }
            foreach (var child in toRemove)
                ScheduleGrid.Children.Remove(child);

            // Создаем ячейки для уроков
            for (int row = 1; row <= 7; row++) // 7 уроков
            {
                for (int col = 1; col <= 6; col++) // 6 дней (Пн-Сб)
                {
                    CreateEmptyCell(row, col);
                }
            }

            // Заполняем уроками
            foreach (var lesson in _allSchedule)
            {
                int dayOfWeek = (int)lesson.LessonDate.DayOfWeek;

                // В C# DayOfWeek: Sunday = 0, Monday = 1, Tuesday = 2, ..., Saturday = 6
                int column;
                if (dayOfWeek == 0) // Воскресенье - пропускаем (должно быть 7, но у нас только 6 дней)
                    continue;
                else
                    column = dayOfWeek; // Понедельник = 1, Вторник = 2, ..., Суббота = 6

                int row = lesson.LessonNumber;

                // Grid имеет колонки: 0 - время, 1-6 - дни недели
                if (row >= 1 && row <= 7 && column >= 1 && column <= 6)
                {
                    CreateLessonCard(row, column, lesson); // Убрали смещение +1
                }
            }
        }

        private void CreateEmptyCell(int row, int column)
        {
            var emptyCell = new Border
            {
                Style = (Style)FindResource("ScheduleCellStyle"),
                Name = $"Empty_{row}_{column}"
            };

            Grid.SetRow(emptyCell, row);
            Grid.SetColumn(emptyCell, column);
            ScheduleGrid.Children.Add(emptyCell);
        }

        private void CreateLessonCard(int row, int gridColumn, Schedule lesson)
        {
            var card = new Border
            {
                Style = (Style)FindResource("LessonCardStyle"),
                Name = $"Lesson_{row}_{gridColumn}",
                Tag = lesson
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            // Название группы
            var groupText = new TextBlock
            {
                Text = lesson.ClassName ?? "Группа",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5271FF")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stackPanel.Children.Add(groupText);

            // Название дисциплины
            var subjectText = new TextBlock
            {
                Text = lesson.SubjectName ?? "Дисциплина",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(subjectText);

            // Тема (если есть)
            if (!string.IsNullOrEmpty(lesson.Topic))
            {
                var topicText = new TextBlock
                {
                    Text = lesson.Topic,
                    FontSize = 10,
                    Foreground = Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0),
                    MaxHeight = 40
                };
                stackPanel.Children.Add(topicText);
            }

            card.Child = stackPanel;

            card.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    if (e.ClickCount == 1)
                    {
                        _selectedLesson = lesson;
                        UpdateSelectionVisual();
                        e.Handled = true;
                    }
                    else if (e.ClickCount == 2)
                    {
                        _selectedLesson = lesson;
                        ShowLessonDetails();
                        e.Handled = true;
                    }
                }
            };

            Grid.SetRow(card, row);
            Grid.SetColumn(card, gridColumn);
            ScheduleGrid.Children.Add(card);
        }

        private void UpdateSelectionVisual()
        {
            // Сбрасываем стиль всех карточек
            foreach (UIElement child in ScheduleGrid.Children)
            {
                if (child is Border border && border.Name != null && border.Name.StartsWith("Lesson_"))
                {
                    border.Style = (Style)FindResource("LessonCardStyle");
                }
            }

            // Выделяем выбранную карточку
            if (_selectedLesson != null)
            {
                var selectedBorder = FindLessonBorder(_selectedLesson);
                if (selectedBorder != null)
                {
                    selectedBorder.Style = (Style)FindResource("LessonCardStyle");
                    selectedBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D6E4FF"));
                    selectedBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3366FF"));
                    selectedBorder.BorderThickness = new Thickness(2);
                }
            }
        }

        private Border FindLessonBorder(Schedule lesson)
        {
            foreach (UIElement child in ScheduleGrid.Children)
            {
                if (child is Border border && border.Tag is Schedule borderLesson && borderLesson.Id == lesson.Id)
                {
                    return border;
                }
            }
            return null;
        }

        private void ShowLessonDetails()
        {
            if (_selectedLesson == null) return;

            string timeSlot = GetLessonTime(_selectedLesson.LessonNumber);
            string dateStr = _selectedLesson.LessonDate.ToString("dd.MM.yyyy (dddd)");

            MessageBox.Show(
                $"📅 Дата: {dateStr}\n" +
                $"🕐 Время: {timeSlot}\n" +
                $"🏫 Группа: {_selectedLesson.ClassName}\n" +
                $"📚 Дисциплина: {_selectedLesson.SubjectName}\n" +
                $"📖 Тема: {(!string.IsNullOrEmpty(_selectedLesson.Topic) ? _selectedLesson.Topic : "Не указана")}",
                "Детали занятия",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private string GetLessonTime(int lessonNumber)
        {
            return lessonNumber switch
            {
                1 => "8:00 - 9:30",
                2 => "9:40 - 11:10",
                3 => "11:20 - 12:50",
                4 => "13:45 - 15:15",
                5 => "15:25 - 16:55",
                6 => "17:05 - 18:35",
                7 => "18:45 - 20:15",
                _ => $"{lessonNumber} пара"
            };
        }

        #region Обработчики кнопок

        private async void PreviousWeek_Click(object sender, RoutedEventArgs e)
        {
            _currentWeekStart = _currentWeekStart.AddDays(-7);
            _selectedLesson = null;
            await LoadScheduleAsync();
        }

        private async void NextWeek_Click(object sender, RoutedEventArgs e)
        {
            _currentWeekStart = _currentWeekStart.AddDays(7);
            _selectedLesson = null;
            await LoadScheduleAsync();
        }

        private async void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            _currentWeekStart = GetStartOfWeek(DateTime.Now);
            _selectedLesson = null;
            await LoadScheduleAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadScheduleAsync();
        }

        #endregion
    }
}