using Diplom.Models;
using Diplom.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Diplom
{
    public partial class ScheduleView : UserControl
    {
        private ObservableCollection<Class> _classes;
        private ObservableCollection<Schedule> _schedule;
        private List<Schedule> _allSchedule;
        private DateTime _currentWeekStart;
        private int _selectedClassId;
        private Schedule _selectedLesson;
        private Schedule _lessonToSelectAfterLoad;

        private readonly Dictionary<int, (string Start, string End)> _lessonTimes = new Dictionary<int, (string, string)>
        {
            { 1, ("8:00", "9:30") },
            { 2, ("9:40", "11:10") },
            { 3, ("11:20", "12:50") },
            { 4, ("13:45", "15:15") },
            { 5, ("15:25", "16:55") },
            { 6, ("17:05", "18:35") },
            { 7, ("18:45", "20:15") }
        };

        public Schedule SelectedLesson
        {
            get => _selectedLesson;
            set
            {
                _selectedLesson = value;
                UpdateSelectionVisual();
            }
        }

        public ScheduleView()
        {
            InitializeComponent();
            _classes = new ObservableCollection<Class>();
            _schedule = new ObservableCollection<Schedule>();
            _allSchedule = new List<Schedule>();
            _currentWeekStart = GetStartOfWeek(DateTime.Now);

            _classes.CollectionChanged += (s, e) =>
            {
                Console.WriteLine($"Classes collection changed. Count: {_classes.Count}");
                if (_classes.Count > 0 && _selectedClassId > 0)
                {
                    RestoreSelectedClass();
                }
            };

            Loaded += async (s, e) => await InitializeAsync();
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            await LoadClassesAsync();
            UpdateWeekDisplay();
        }

        private Class _currentSelectedClass;

        private async System.Threading.Tasks.Task LoadClassesAsync()
        {
            try
            {
                var result = await SupabaseClient.GetClassesWithTeachers();
                _classes.Clear();

                foreach (var item in result)
                {
                    var classItem = new Class
                    {
                        Id = item["id"]?.Type == JTokenType.Null ? 0 : item["id"]?.Value<int>() ?? 0,
                        Name = item["name"]?.ToString() ?? "Не указано",
                        TeacherId = item["teacher_id"]?.Type == JTokenType.Null ? (int?)null : item["teacher_id"]?.Value<int?>(),
                        TeacherName = item["teachers"]?["full_name"]?.ToString() ?? "Не назначен"
                    };

                    if (classItem.Id > 0)
                        _classes.Add(classItem);
                }

                ClassComboBox.ItemsSource = _classes;

                if (_currentSelectedClass != null)
                {
                    ClassComboBox.SelectedItem = _classes.FirstOrDefault(c => c.Id == _currentSelectedClass.Id);
                }

                if (ClassComboBox.SelectedItem == null && _classes.Count > 0)
                    ClassComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки групп: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadScheduleAsync()
        {
            if (_selectedClassId == 0) return;

            try
            {
                var monday = _currentWeekStart.ToString("yyyy-MM-dd");
                var saturday = _currentWeekStart.AddDays(5).ToString("yyyy-MM-dd");

                var result = await SupabaseClient.ExecuteQuery("schedule",
                    $"class_id=eq.{_selectedClassId}&lesson_date=gte.{monday}&lesson_date=lte.{saturday}&select=*,subjects(name),teachers(full_name)&order=lesson_date,lesson_number");

                _allSchedule.Clear();
                _schedule.Clear();

                if (result == null || result.Count == 0)
                {
                    RenderSchedule();
                    return;
                }

                foreach (var item in result)
                {
                    try
                    {
                        if (item.Type != JTokenType.Object)
                            continue;

                        var schedule = new Schedule();

                        schedule.Id = item["id"]?.Value<int>() ?? 0;
                        schedule.ClassId = item["class_id"]?.Value<int>() ?? 0;
                        schedule.SubjectId = item["subject_id"]?.Value<int>() ?? 0;
                        schedule.TeacherId = item["teacher_id"]?.Value<int?>();
                        schedule.LessonNumber = item["lesson_number"]?.Value<int>() ?? 0;
                        schedule.Topic = item["topic"]?.ToString();

                        if (DateTime.TryParse(item["lesson_date"]?.ToString(), out DateTime date))
                            schedule.LessonDate = date;

                        var subjectsToken = item["subjects"];
                        if (subjectsToken?.Type == JTokenType.Object)
                            schedule.SubjectName = subjectsToken["name"]?.ToString() ?? "Не указано";
                        else
                            schedule.SubjectName = "Не указано";

                        var teachersToken = item["teachers"];
                        if (teachersToken?.Type == JTokenType.Object)
                            schedule.TeacherName = teachersToken["full_name"]?.ToString() ?? "Не назначен";
                        else
                            schedule.TeacherName = "Не назначен";

                        if (schedule.Id > 0 &&
                            schedule.ClassId > 0 &&
                            schedule.SubjectId > 0 &&
                            schedule.LessonDate != DateTime.MinValue &&
                            schedule.LessonNumber > 0)
                        {
                            _allSchedule.Add(schedule);
                            _schedule.Add(schedule);
                        }
                    }
                    catch (Exception innerEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка парсинга: {innerEx}");
                    }
                }

                RenderSchedule();

                if (_lessonToSelectAfterLoad != null)
                {
                    await System.Threading.Tasks.Task.Delay(100);
                    SelectLessonAfterLoad();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки расписания: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectLessonAfterLoad()
        {
            if (_lessonToSelectAfterLoad == null) return;

            var l = _lessonToSelectAfterLoad;
            Schedule lessonToSelect = null;

            if (l.Id > 0)
            {
                lessonToSelect = _allSchedule.FirstOrDefault(s => s.Id == l.Id);
            }

            if (lessonToSelect == null)
            {
                var targetDate = l.LessonDate.ToString("yyyy-MM-dd");
                lessonToSelect = _allSchedule.FirstOrDefault(s =>
                    s.LessonDate.ToString("yyyy-MM-dd") == targetDate &&
                    s.LessonNumber == l.LessonNumber);
            }

            if (lessonToSelect != null)
            {
                SelectedLesson = lessonToSelect;
                ScrollToLesson(lessonToSelect);
            }

            _lessonToSelectAfterLoad = null;
        }

        private void ScrollToLesson(Schedule lesson)
        {
            if (lesson == null) return;

            var lessonBorder = FindLessonBorder(lesson);
            if (lessonBorder != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    lessonBorder.BringIntoView();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void RenderSchedule()
        {
            var toRemove = new List<UIElement>();
            foreach (UIElement child in ScheduleGrid.Children)
            {
                if (child is Border b && b.Name.StartsWith("Cell_"))
                    toRemove.Add(child);
            }
            foreach (var ch in toRemove)
                ScheduleGrid.Children.Remove(ch);

            foreach (var lesson in _schedule)
            {
                int dayOfWeek = (int)lesson.LessonDate.DayOfWeek;
                int column = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
                int row = lesson.LessonNumber;

                if (row >= 1 && row <= 7 && column >= 0 && column <= 6)
                    CreateLessonCard(row, column + 1, lesson);
            }
        }

        private void CreateLessonCard(int row, int column, Schedule lesson)
        {
            var card = new Border
            {
                Style = (Style)Resources["LessonCardStyle"],
                Name = $"Cell_{row}_{column}",
                Tag = lesson
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            var subjectText = new TextBlock
            {
                Text = lesson.SubjectName,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap
            };

            var teacherText = new TextBlock
            {
                Text = lesson.TeacherName,
                FontSize = 10,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            };

            if (!string.IsNullOrEmpty(lesson.Topic))
            {
                var topicText = new TextBlock
                {
                    Text = lesson.Topic,
                    FontSize = 9,
                    Foreground = Brushes.DarkBlue,
                    FontStyle = FontStyles.Italic,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                stackPanel.Children.Add(topicText);
            }

            stackPanel.Children.Add(subjectText);
            stackPanel.Children.Add(teacherText);

            card.Child = stackPanel;

            card.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    if (e.ClickCount == 1)
                    {
                        SelectedLesson = lesson;
                        e.Handled = true;
                    }
                    else if (e.ClickCount == 2)
                    {
                        SelectedLesson = lesson;
                        EditLesson_Click(s, e);
                        e.Handled = true;
                    }
                }
            };

            Grid.SetRow(card, row);
            Grid.SetColumn(card, column);
            ScheduleGrid.Children.Add(card);
        }

        private void UpdateSelectionVisual()
        {
            foreach (UIElement child in ScheduleGrid.Children)
            {
                if (child is Border border && border.Name.StartsWith("Cell_"))
                {
                    border.Style = (Style)Resources["LessonCardStyle"];
                }
            }

            if (SelectedLesson != null)
            {
                var selectedBorder = FindLessonBorder(SelectedLesson);
                if (selectedBorder != null)
                {
                    selectedBorder.Style = (Style)Resources["SelectedLessonCardStyle"];
                    selectedBorder.BringIntoView();
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

        private DateTime GetStartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff).Date;
        }

        private void ClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassComboBox.SelectedItem is Class selectedClass)
            {
                Console.WriteLine($"=== ClassComboBox_SelectionChanged ===");
                Console.WriteLine($"Выбрана группа: {selectedClass.Name} (ID: {selectedClass.Id})");
                Console.WriteLine($"Предыдущий _selectedClassId: {_selectedClassId}");

                _selectedClassId = selectedClass.Id;
                _lessonToSelectAfterLoad = null;
                _ = LoadScheduleAsync();
            }
        }

        private void AddLesson_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClassId == 0)
            {
                MessageBox.Show("Выберите группу для добавления занятия", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowEditView();
        }

        private void EditLesson_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedLesson == null)
            {
                MessageBox.Show("Выберите занятие для редактирования", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowEditView(SelectedLesson);
        }

        private async void DeleteLesson_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedLesson == null)
            {
                MessageBox.Show("Выберите занятие для удаления", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Вы уверены, что хотите удалить занятие \"{SelectedLesson.SubjectName}\"?\n\nЭто действие нельзя отменить.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await SupabaseClient.Delete("schedule", $"id=eq.{SelectedLesson.Id}");
                    SelectedLesson = null;
                    await LoadScheduleAsync();

                    MessageBox.Show("Занятие успешно удалено", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _lessonToSelectAfterLoad = SelectedLesson;
            _ = LoadScheduleAsync();
        }

        private void PreviousWeek_Click(object sender, RoutedEventArgs e)
        {
            _currentWeekStart = _currentWeekStart.AddDays(-7);
            UpdateWeekDisplay();
            _lessonToSelectAfterLoad = null;
            _ = LoadScheduleAsync();
        }

        private void NextWeek_Click(object sender, RoutedEventArgs e)
        {
            _currentWeekStart = _currentWeekStart.AddDays(7);
            UpdateWeekDisplay();
            _lessonToSelectAfterLoad = null;
            _ = LoadScheduleAsync();
        }

        private void ScheduleGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Border border && border.Name.StartsWith("Cell_"))
            {
                return;
            }

            SelectedLesson = null;
        }

        private void ShowEditView(Schedule lesson = null)
        {
            Console.WriteLine($"=== ShowEditView ===");
            Console.WriteLine($"Текущая выбранная группа ID: {_selectedClassId}");
            Console.WriteLine($"Текущая выбранная группа в ComboBox: {(ClassComboBox.SelectedItem as Class)?.Name}");
            Console.WriteLine($"Занятие для редактирования: {lesson?.Id}");

            var parentContentControl = GetParentContentControl(this);
            if (parentContentControl == null)
            {
                MessageBox.Show("Ошибка навигации", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var currentView = parentContentControl.Content;
            ClassComboBox.IsEnabled = false;

            var editView = new ScheduleEditView(lesson, _selectedClassId, _currentWeekStart);
            editView.SaveCompleted += async (success, savedLesson) =>
            {
                ClassComboBox.IsEnabled = true;

                if (success && savedLesson != null)
                {
                    parentContentControl.Content = currentView;
                    _lessonToSelectAfterLoad = savedLesson;
                    await LoadScheduleAsync();
                    RestoreSelectedClass();
                }
                else
                {
                    parentContentControl.Content = editView;
                }
            };

            editView.GoBack += () =>
            {
                ClassComboBox.IsEnabled = true;
                parentContentControl.Content = currentView;
                RestoreSelectedClass();

                if (lesson != null)
                    SelectedLesson = lesson;
            };

            parentContentControl.Content = editView;
        }

        private void RestoreSelectedClass()
        {
            if (_selectedClassId > 0)
            {
                var classToSelect = _classes.FirstOrDefault(c => c.Id == _selectedClassId);
                if (classToSelect != null)
                {
                    ClassComboBox.SelectedItem = classToSelect;
                    Console.WriteLine($"Восстановлена группа: {classToSelect.Name}");
                }
                else
                {
                    Console.WriteLine($"Группа с ID {_selectedClassId} не найдена в списке");
                }
            }
        }

        private ContentControl GetParentContentControl(DependencyObject child)
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is ContentControl contentControl)
                    return contentControl;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
        private async void ExportToPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_classes.Count == 0)
            {
                MessageBox.Show("Нет доступных групп для экспорта", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Современная цветовая палитра
            var windowBgBrush = (SolidColorBrush)new BrushConverter().ConvertFrom("#F3F4F6"); // Мягкий светло-серый фон
            var cardBgBrush = Brushes.White;
            var textDarkBrush = (SolidColorBrush)new BrushConverter().ConvertFrom("#111827"); // Почти черный
            var textMutedBrush = (SolidColorBrush)new BrushConverter().ConvertFrom("#4B5563"); // Приглушенный серый
            var borderBrush = (SolidColorBrush)new BrushConverter().ConvertFrom("#E5E7EB"); // Светло-серая рамка

            var dialog = new Window
            {
                Title = "Экспорт расписания",
                Width = 420,
                Height = 580,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Background = windowBgBrush,
                FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
                WindowStyle = WindowStyle.None
            };

            // Главная белая карточка со скругленными углами
            var cardBorder = new Border
            {
                Background = cardBgBrush,
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(16),
                Padding = new Thickness(24),
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1)
            };

            // Добавляем легкую тень для карточки
            cardBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 270,
                ShadowDepth = 2,
                Opacity = 0.05,
                BlurRadius = 10
            };

            var mainPanel = new StackPanel();

            var titleText = new TextBlock
            {
                Text = "Экспорт расписания",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = textDarkBrush,
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            mainPanel.Children.Add(titleText);

            // Выбор месяца
            var monthLabel = new TextBlock
            {
                Text = "Выберите месяц",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = textMutedBrush,
                Margin = new Thickness(0, 0, 0, 8)
            };
            mainPanel.Children.Add(monthLabel);

            var monthComboBox = new ComboBox
            {
                Height = 38,
                Margin = new Thickness(0, 0, 0, 20),
                DisplayMemberPath = "Key",
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                FontSize = 14,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 8, 0),
                Style = (Style)FindResource("FormComboBox")
            };

            var months = new List<KeyValuePair<string, DateTime>>();
            for (int i = 1; i <= 12; i++)
            {
                var date = new DateTime(DateTime.Now.Year, i, 1);
                months.Add(new KeyValuePair<string, DateTime>(date.ToString("MMMM yyyy"), date));
            }
            monthComboBox.ItemsSource = months;
            monthComboBox.SelectedIndex = DateTime.Now.Month - 1;
            mainPanel.Children.Add(monthComboBox);

            // Выбор групп
            var groupsLabel = new TextBlock
            {
                Text = "Выберите группы для экспорта",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = textMutedBrush,
                Margin = new Thickness(0, 0, 0, 8)
            };
            mainPanel.Children.Add(groupsLabel);

            var groupsListBox = new ListBox
            {
                Height = 160,
                Margin = new Thickness(0, 0, 0, 24),
                SelectionMode = SelectionMode.Multiple,
                DisplayMemberPath = "Name",
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(4),
                Style = (Style)FindResource("FormListBox")
            };

            foreach (var classItem in _classes)
            {
                groupsListBox.Items.Add(classItem);
            }

            foreach (var item in groupsListBox.Items)
            {
                groupsListBox.SelectedItems.Add(item);
            }
            mainPanel.Children.Add(groupsListBox);

            // Прогресс бар и статус (обернуты в отдельную панель, чтобы занимать фиксированное место)
            var progressPanel = new StackPanel { Height = 50, Margin = new Thickness(0, 0, 0, 15) };

            var progressBar = new ProgressBar
            {
                Height = 6, // Тонкий современный прогресс-бар
                Margin = new Thickness(0, 0, 0, 8),
                Visibility = Visibility.Collapsed,
                BorderThickness = new Thickness(0),
                Background = borderBrush
            };
            progressPanel.Children.Add(progressBar);

            var statusText = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Foreground = textMutedBrush,
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };
            progressPanel.Children.Add(statusText);
            mainPanel.Children.Add(progressPanel);

            // Кнопки
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var cancelButton = new Button
            {
                Content = "Отмена",
                Width = 90,
                Height = 38,
                Margin = new Thickness(0, 0, 12, 0),
                Style = (Style)FindResource("SecondaryButton"),
                Cursor = Cursors.Hand
            };
            cancelButton.Click += (s, args) => dialog.Close();

            var exportButton = new Button
            {
                Content = "Экспортировать",
                Width = 140,
                Height = 38,
                Style = (Style)FindResource("PrimaryButton"),
                Cursor = Cursors.Hand
            };
            exportButton.Click += async (s, args) =>
            {
                if (monthComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Выберите месяц", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (groupsListBox.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Выберите хотя бы одну группу", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                exportButton.IsEnabled = false;
                cancelButton.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;
                statusText.Visibility = Visibility.Visible;
                progressBar.IsIndeterminate = true;
                statusText.Text = "Загрузка расписания...";

                try
                {
                    var selectedMonth = ((KeyValuePair<string, DateTime>)monthComboBox.SelectedItem).Value;
                    var selectedClasses = groupsListBox.SelectedItems.Cast<Class>().ToList();
                    var schedulesByClass = new Dictionary<Class, List<Schedule>>();

                    int totalClasses = selectedClasses.Count;
                    int currentClass = 0;

                    foreach (var classItem in selectedClasses)
                    {
                        currentClass++;
                        statusText.Text = $"Загрузка расписания для группы {classItem.Name} ({currentClass}/{totalClasses})...";
                        progressBar.Value = (currentClass * 50) / totalClasses;

                        var startDate = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
                        var endDate = startDate.AddMonths(1).AddDays(-1);

                        var startStr = startDate.ToString("yyyy-MM-dd");
                        var endStr = endDate.ToString("yyyy-MM-dd");

                        var result = await SupabaseClient.ExecuteQuery("schedule",
                            $"class_id=eq.{classItem.Id}&lesson_date=gte.{startStr}&lesson_date=lte.{endStr}&select=*,subjects(name),teachers(full_name)&order=lesson_date,lesson_number");

                        var schedules = new List<Schedule>();

                        if (result != null && result.Count > 0)
                        {
                            foreach (var item in result)
                            {
                                if (item.Type != JTokenType.Object) continue;

                                var schedule = new Schedule();
                                schedule.Id = item["id"]?.Value<int>() ?? 0;
                                schedule.ClassId = item["class_id"]?.Value<int>() ?? 0;
                                schedule.SubjectId = item["subject_id"]?.Value<int>() ?? 0;
                                schedule.TeacherId = item["teacher_id"]?.Value<int?>();
                                schedule.LessonNumber = item["lesson_number"]?.Value<int>() ?? 0;
                                schedule.Topic = item["topic"]?.ToString();

                                if (DateTime.TryParse(item["lesson_date"]?.ToString(), out DateTime date))
                                    schedule.LessonDate = date;

                                var subjectsToken = item["subjects"];
                                if (subjectsToken?.Type == JTokenType.Object)
                                    schedule.SubjectName = subjectsToken["name"]?.ToString() ?? "Не указано";

                                var teachersToken = item["teachers"];
                                if (teachersToken?.Type == JTokenType.Object)
                                    schedule.TeacherName = teachersToken["full_name"]?.ToString() ?? "Не назначен";

                                if (schedule.Id > 0 && schedule.ClassId > 0 && schedule.SubjectId > 0 &&
                                    schedule.LessonDate != DateTime.MinValue && schedule.LessonNumber > 0)
                                {
                                    schedules.Add(schedule);
                                }
                            }
                        }

                        schedulesByClass[classItem] = schedules;
                    }

                    statusText.Text = "Генерация PDF отчета...";
                    progressBar.IsIndeterminate = false;
                    progressBar.Maximum = 100;
                    progressBar.Value = 70;

                    var pdfBytes = ScheduleReportService.GenerateMonthlyScheduleReportBeautiful(
                        selectedMonth,
                        schedulesByClass);

                    progressBar.Value = 100;
                    statusText.Text = "Сохранение файла...";

                    var saveDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Filter = "PDF files (*.pdf)|*.pdf",
                        DefaultExt = ".pdf",
                        FileName = $"Расписание_{selectedMonth:MMMM_yyyy}.pdf"
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        System.IO.File.WriteAllBytes(saveDialog.FileName, pdfBytes);
                        MessageBox.Show($"PDF отчет успешно сохранен!\n\nФайл: {saveDialog.FileName}",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    dialog.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка экспорта: {ex.Message}\n\n{ex.StackTrace}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    exportButton.IsEnabled = true;
                    cancelButton.IsEnabled = true;
                }
            };

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(exportButton);
            mainPanel.Children.Add(buttonPanel);

            // Упаковываем панель в карточку, а карточку в окно
            cardBorder.Child = mainPanel;
            dialog.Content = cardBorder;
            dialog.ShowDialog();
        }
    }
}