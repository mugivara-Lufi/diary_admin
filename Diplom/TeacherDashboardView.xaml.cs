using Diplom.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using static Diplom.SupabaseClient;

namespace Diplom
{
    public partial class TeacherDashboardView : UserControl
    {
        private int _teacherId;
        private List<Schedule> _todaySchedule;

        public TeacherDashboardView()
        {
            InitializeComponent();

            Loaded += async (s, e) => await LoadDashboardDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDashboardDataAsync()
        {
            try
            {
                // Получаем ID преподавателя из AuthService
                if (AuthService.CurrentTeacher != null)
                {
                    _teacherId = AuthService.CurrentTeacher.Id;
                }
                else
                {
                    // Если CurrentTeacher не установлен, пробуем получить по user_id
                    await LoadTeacherProfileFromUser();
                }

                // Загружаем все данные параллельно
                await Task.WhenAll(
                    LoadStatisticsAsync(),
                    LoadTodayScheduleAsync(),
                    LoadRecentActivitiesAsync()
                );

                SetupGreeting();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки дашборда: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadTeacherProfileFromUser()
        {
            try
            {
                if (AuthService.CurrentUser != null)
                {
                    int userId = AuthService.CurrentUser["id"].Value<int>();
                    var result = await SupabaseClient.ExecuteQuery("teachers", $"user_id=eq.{userId}&select=*,subjects(name)");

                    if (result != null && result.Count > 0)
                    {
                        var teacher = result[0];
                        _teacherId = teacher["id"]?.Value<int>() ?? 0;

                        AuthService.CurrentTeacher = new Teacher
                        {
                            Id = _teacherId,
                            FullName = teacher["full_name"]?.ToString() ?? "",
                            SubjectId = teacher["subject_id"]?.Value<int?>(),
                            Email = teacher["email"]?.ToString() ?? "",
                            SubjectName = teacher["subjects"]?["name"]?.ToString() ?? ""
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки профиля: {ex.Message}");
            }
        }

        private void SetupGreeting()
        {
            var hour = DateTime.Now.Hour;
            string greeting = hour switch
            {
                >= 5 and < 12 => "Доброе утро",
                >= 12 and < 18 => "Добрый день",
                >= 18 and < 22 => "Добрый вечер",
                _ => "Доброй ночи"
            };

            string teacherName = AuthService.CurrentTeacher?.FullName?.Split(' ')[0] ?? "Преподаватель";
            GreetingText.Text = $"{greeting}, {teacherName}!";
            TodayDateText.Text = DateTime.Now.ToString("dd MMMM yyyy, dddd");
        }

        private async System.Threading.Tasks.Task LoadStatisticsAsync()
        {
            try
            {
                // Получаем занятия преподавателя на сегодня
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var teacherLessons = await SupabaseClient.ExecuteQuery("schedule",
                    $"teacher_id=eq.{_teacherId}&lesson_date=eq.{today}&select=id");
                TodayLessonsCount.Text = teacherLessons?.Count.ToString() ?? "0";

                // Получаем группы, которые ведет преподаватель
                var groupsResult = await SupabaseClient.ExecuteQuery("schedule",
                    $"teacher_id=eq.{_teacherId}&select=class_id,classes(name)");

                var uniqueGroups = new HashSet<int>();
                foreach (var item in groupsResult ?? new JArray())
                {
                    var classId = item["class_id"]?.Value<int>();
                    if (classId.HasValue && classId.Value > 0)
                        uniqueGroups.Add(classId.Value);
                }
                TotalGroupsCount.Text = uniqueGroups.Count.ToString();

                // Получаем студентов из групп преподавателя
                if (uniqueGroups.Count > 0)
                {
                    string groupFilter = string.Join(",", uniqueGroups);
                    var studentsResult = await SupabaseClient.ExecuteQuery("students",
                        $"class_id=in.({groupFilter})&select=id");
                    TotalStudentsCount.Text = studentsResult?.Count.ToString() ?? "0";
                }

                // Получаем средний балл
                var gradesResult = await SupabaseClient.ExecuteQuery("grades",
                    $"teacher_id=eq.{_teacherId}&select=grade");

                if (gradesResult != null && gradesResult.Count > 0)
                {
                    double total = 0;
                    int count = 0;

                    foreach (var item in gradesResult)
                    {
                        string gradeStr = item["grade"]?.ToString();
                        if (double.TryParse(gradeStr, out double grade))
                        {
                            total += grade;
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        double avg = total / count;
                        AverageGrade.Text = avg.ToString("F1");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки статистики: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadTodayScheduleAsync()
        {
            try
            {
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var result = await SupabaseClient.ExecuteQuery("schedule",
                    $"teacher_id=eq.{_teacherId}&lesson_date=eq.{today}&select=*,classes(name),subjects(name)&order=lesson_number");

                _todaySchedule = new List<Schedule>();
                TodaySchedulePanel.Children.Clear();

                if (result == null || result.Count == 0)
                {
                    NoLessonsText.Visibility = Visibility.Visible;
                    return;
                }

                NoLessonsText.Visibility = Visibility.Collapsed;

                foreach (var item in result)
                {
                    var schedule = new Schedule
                    {
                        Id = item["id"]?.Value<int>() ?? 0,
                        LessonNumber = item["lesson_number"]?.Value<int>() ?? 0,
                        Topic = item["topic"]?.ToString(),
                        SubjectName = item["subjects"]?["name"]?.ToString() ?? "Не указано",
                        TeacherName = AuthService.CurrentTeacher?.FullName ?? ""
                    };

                    var classToken = item["classes"];
                    if (classToken != null && classToken.Type == JTokenType.Object)
                    {
                        schedule.ClassName = classToken["name"]?.ToString() ?? "Не указано";
                    }

                    _todaySchedule.Add(schedule);
                    TodaySchedulePanel.Children.Add(CreateScheduleItem(schedule));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки расписания: {ex.Message}");
            }
        }

        private Border CreateScheduleItem(Schedule lesson)
        {
            var timeSlot = GetLessonTime(lesson.LessonNumber);

            var border = new Border
            {
                Style = (Style)FindResource("ScheduleItemStyle")
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Время
            var timeText = new TextBlock
            {
                Text = timeSlot,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5271FF")),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(timeText, 0);
            grid.Children.Add(timeText);

            // Информация о занятии
            var infoStack = new StackPanel();

            var groupSubjectText = new TextBlock
            {
                Text = $"{lesson.ClassName} • {lesson.SubjectName}",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333"))
            };
            infoStack.Children.Add(groupSubjectText);

            if (!string.IsNullOrEmpty(lesson.Topic))
            {
                var topicText = new TextBlock
                {
                    Text = lesson.Topic,
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")),
                    Margin = new Thickness(0, 3, 0, 0)
                };
                infoStack.Children.Add(topicText);
            }

            Grid.SetColumn(infoStack, 1);
            grid.Children.Add(infoStack);

            // Кнопка "Начать занятие"
            var startButton = new Button
            {
                Content = "Начать",
                Height = 28,
                Width = 70,
                FontSize = 11,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEF2FF")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5271FF")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = lesson
            };
            startButton.Click += StartLesson_Click;

            Grid.SetColumn(startButton, 2);
            grid.Children.Add(startButton);

            border.Child = grid;
            return border;
        }

        private string GetLessonTime(int lessonNumber)
        {
            return lessonNumber switch
            {
                1 => "8:00-9:30",
                2 => "9:40-11:10",
                3 => "11:20-12:50",
                4 => "13:45-15:15",
                5 => "15:25-16:55",
                6 => "17:05-18:35",
                7 => "18:45-20:15",
                _ => $"{lessonNumber} пара"
            };
        }

        private async System.Threading.Tasks.Task LoadRecentActivitiesAsync()
        {
            try
            {
                RecentActivitiesPanel.Children.Clear();

                // Загружаем последние оценки
                var recentGrades = await SupabaseClient.ExecuteQuery("grades",
                    $"teacher_id=eq.{_teacherId}&order=date.desc&limit=10&select=*,students(full_name),subjects(name)");

                if (recentGrades == null || recentGrades.Count == 0)
                {
                    NoActivitiesText.Visibility = Visibility.Visible;
                    return;
                }

                NoActivitiesText.Visibility = Visibility.Collapsed;

                foreach (var item in recentGrades)
                {
                    var studentName = item["students"]?["full_name"]?.ToString() ?? "Студент";
                    var subjectName = item["subjects"]?["name"]?.ToString() ?? "Дисциплина";
                    var grade = item["grade"]?.ToString() ?? "";
                    var date = item["date"]?.ToString()?.Split('T')[0] ?? "";

                    var activityItem = CreateActivityItem(studentName, subjectName, grade, date);
                    RecentActivitiesPanel.Children.Add(activityItem);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки активностей: {ex.Message}");
            }
        }

        private Border CreateActivityItem(string studentName, string subjectName, string grade, string date)
        {
            var border = new Border
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(0, 5, 0, 5)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Иконка
            var iconText = new TextBlock
            {
                Text = "📝",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(iconText, 0);
            grid.Children.Add(iconText);

            // Информация
            var infoText = new TextBlock
            {
                Text = $"{studentName} • {subjectName}",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333")),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(infoText, 1);
            grid.Children.Add(infoText);

            // Оценка и дата
            var rightStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var gradeText = new TextBlock
            {
                Text = grade,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5271FF")),
                Margin = new Thickness(0, 0, 10, 0)
            };
            rightStack.Children.Add(gradeText);

            var dateText = new TextBlock
            {
                Text = date,
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999"))
            };
            rightStack.Children.Add(dateText);

            Grid.SetColumn(rightStack, 2);
            grid.Children.Add(rightStack);

            border.Child = grid;
            return border;
        }

        #region Обработчики кнопок

        private void ViewFullSchedule_Click(object sender, RoutedEventArgs e)
        {
            var parentWindow = FindParent<TeacherWindow>(this);
            if (parentWindow != null)
            {
                parentWindow.ScheduleButton_Click(sender, e);
            }
        }

        private void MarkAttendance_Click(object sender, RoutedEventArgs e)
        {
            var parentWindow = FindParent<TeacherWindow>(this);
            if (parentWindow != null)
            {
                parentWindow.AttendanceButton_Click(sender, e);
            }
        }

        private void AddGrades_Click(object sender, RoutedEventArgs e)
        {
            var parentWindow = FindParent<TeacherWindow>(this);
            if (parentWindow != null)
            {
                parentWindow.GradesButton_Click(sender, e);
            }
        }

        private void AddHomework_Click(object sender, RoutedEventArgs e)
        {
            var parentWindow = FindParent<TeacherWindow>(this);
            if (parentWindow != null)
            {
                parentWindow.HomeworkButton_Click(sender, e);
            }
        }

        private void StartLesson_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Schedule lesson)
            {
                MessageBox.Show($"Начинаем занятие:\n{lesson.ClassName} • {lesson.SubjectName}\n\nФункция будет реализована в следующей версии.",
                    "Начало занятия", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T typedParent)
                    return typedParent;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}