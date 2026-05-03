using Diplom.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Diplom
{
    /// <summary>
    /// Журнал оценок преподавателя
    /// </summary>
    public partial class TeacherGradesView : UserControl
    {
        // Основные данные
        private DataTable _journalData;
        private List<Student> _students;
        private List<Class> _classes;
        private List<Subject> _subjects;
        private List<DateTime> _lessonDates;
        private HashSet<string> _changedCells = new HashSet<string>();

        // Выбранные фильтры
        private Class _selectedClass;
        private Subject _selectedSubject;

        // Валидные значения
        private static readonly HashSet<string> ValidGrades = new HashSet<string>
        { "", "2", "3", "4", "5", "H", "h", "Н", "н" };

        public TeacherGradesView()
        {
            InitializeComponent();
            _journalData = new DataTable();
            _students = new List<Student>();
            _lessonDates = new List<DateTime>();
            _changedCells = new HashSet<string>();

            Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                StatusText.Text = "Загрузка данных...";
                GradesGrid.IsEnabled = false;

                await LoadFilters();
                await Task.Delay(100); // Небольшая задержка для UI

                if (_classes != null && _classes.Any())
                {
                    ClassFilter.SelectedIndex = 0; // или установите выбранный класс другим способом
                }

                // Если оба фильтра уже заполнены — загружаем данные
                if (_selectedClass != null && _selectedSubject != null)
                {
                    await LoadJournalData();
                }
                StatusText.Text = "Готово к работе";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Ошибка загрузки";
            }
            finally
            {
                GradesGrid.IsEnabled = true;
            }
        }

        #region Загрузка фильтров

        private async Task LoadFilters()
        {
            // Загружаем классы
            var classesResult = await SupabaseClient.GetClassesWithTeachers();
            _classes = new List<Class>();
            foreach (var item in classesResult)
            {
                _classes.Add(new Class
                {
                    Id = item["id"].Value<int>(),
                    Name = item["name"]?.ToString() ?? "Без названия",
                    TeacherId = item["teacher_id"]?.ToObject<int?>()
                });
            }
            ClassFilter.ItemsSource = _classes;

            // Загружаем предметы
            var subjectsResult = await SupabaseClient.GetAllSubjects();
            _subjects = new List<Subject>();

            // Получаем текущего преподавателя
            var currentTeacher = SupabaseClient.AuthService.CurrentTeacher;

            foreach (var item in subjectsResult)
            {
                var subjectId = item["id"].Value<int>();

                // Если преподаватель авторизован и у него есть предмет, 
                // пропускаем все предметы, ID которых не совпадает с его SubjectId
                if (currentTeacher?.SubjectId != null && subjectId != currentTeacher.SubjectId)
                {
                    continue;
                }

                _subjects.Add(new Subject
                {
                    Id = subjectId,
                    Name = item["name"]?.ToString() ?? "Без названия"
                });
            }

            SubjectFilter.ItemsSource = _subjects;

            // Так как в списке теперь только предмет(ы) преподавателя, 
            // автоматически выбираем первый элемент, чтобы не заставлять кликать лишний раз
            if (_subjects.Any())
            {
                SubjectFilter.SelectedIndex = 0;
                _selectedSubject = _subjects.First();
            }
        }

        #endregion

        #region Фильтры

        private async void ClassFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedClass = ClassFilter.SelectedItem as Class;
            if (_selectedClass != null && _selectedSubject != null)
            {
                await LoadJournalData();
            }
        }

        private async void SubjectFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedSubject = SubjectFilter.SelectedItem as Subject;
            if (_selectedClass != null && _selectedSubject != null)
            {
                await LoadJournalData();
            }
        }

        #endregion

        #region Загрузка данных журнала

        private async Task LoadJournalData()
        {
            try
            {
                StatusText.Text = "Загрузка журнала...";
                GradesGrid.IsEnabled = false;
                _changedCells.Clear();
                UpdateChangesStatus();

                // Загружаем учеников класса
                var studentsResult = await SupabaseClient.ExecuteQuery("students",
                    $"class_id=eq.{_selectedClass.Id}&select=*&order=full_name");

                _students.Clear();
                foreach (var item in studentsResult)
                {
                    _students.Add(new Student
                    {
                        Id = item["id"].Value<int>(),
                        FullName = item["full_name"]?.ToString() ?? "Не указано"
                    });
                }

                // Загружаем расписание (даты занятий по предмету)
                var scheduleResult = await SupabaseClient.ExecuteQuery("schedule",
                    $"class_id=eq.{_selectedClass.Id}&subject_id=eq.{_selectedSubject.Id}" +
                    $"&select=lesson_date&order=lesson_date.asc");

                _lessonDates.Clear();
                foreach (var item in scheduleResult)
                {
                    var date = item["lesson_date"]?.ToObject<DateTime>();
                    if (date != null)
                    {
                        _lessonDates.Add(date.Value.Date);
                    }
                }

                // Убираем дубликаты дат
                _lessonDates = _lessonDates.Distinct().OrderBy(d => d).ToList();

                // Загружаем существующие оценки
                var gradesResult = await SupabaseClient.ExecuteQuery("grades",
                    $"subject_id=eq.{_selectedSubject.Id}&select=student_id,grade,date");

                var existingGrades = new Dictionary<(int studentId, DateTime date), string>();
                foreach (var item in gradesResult)
                {
                    var studentId = item["student_id"]?.Value<int>() ?? 0;
                    var date = item["date"]?.ToObject<DateTime>() ?? DateTime.MinValue;
                    var grade = item["grade"]?.ToString() ?? "";

                    if (studentId > 0 && date != DateTime.MinValue)
                    {
                        existingGrades[(studentId, date.Date)] = grade;
                    }
                }

                // Строим DataTable
                BuildJournalTable(existingGrades);
                UpdateLastUpdateTime();
                StatusText.Text = "Готово";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки журнала: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Ошибка загрузки";
            }
            finally
            {
                GradesGrid.IsEnabled = true;
            }
        }

        private void BuildJournalTable(Dictionary<(int studentId, DateTime date), string> existingGrades)
        {
            _journalData = new DataTable();
            _journalData.Columns.Add("StudentId", typeof(int));
            _journalData.Columns.Add("StudentName", typeof(string));

            if (_students.Count == 0)
            {
                StatusText.Text = "Нет учеников в классе";
                GradesGrid.Columns.Clear();
                GradesGrid.ItemsSource = null;
                return;
            }

            if (_lessonDates.Count == 0)
            {
                StatusText.Text = "Нет занятий в расписании для этого класса и предмета";
                GradesGrid.Columns.Clear();
                GradesGrid.ItemsSource = null;
                return;
            }

            // Безопасные имена колонок: "Col_0", "Col_1", ...
            foreach (var i in Enumerable.Range(0, _lessonDates.Count))
            {
                _journalData.Columns.Add(GetColumnName(i), typeof(string));
            }
            _journalData.Columns.Add("Average", typeof(string));

            foreach (var student in _students)
            {
                var row = _journalData.NewRow();
                row["StudentId"] = student.Id;
                row["StudentName"] = student.FullName;

                var studentGrades = new List<int>();
                for (int i = 0; i < _lessonDates.Count; i++)
                {
                    var date = _lessonDates[i];
                    var key = (student.Id, date);
                    var grade = existingGrades.ContainsKey(key) ? existingGrades[key] : "";
                    row[GetColumnName(i)] = grade;

                    if (int.TryParse(grade, out int ng))
                        studentGrades.Add(ng);
                }

                row["Average"] = studentGrades.Count > 0
                    ? studentGrades.Average().ToString("F2") : "—";

                _journalData.Rows.Add(row);
            }

            BindDataGrid();
        }

        // Вспомогательный метод
        private static string GetColumnName(int index) => $"Col_{index}";

        private void BindDataGrid()
        {
            GradesGrid.Columns.Clear();

            // ФИО
            var nameCol = new DataGridTextColumn
            {
                Header = "👤 Студент",
                Binding = new Binding("StudentName"),
                Width = 200,
                IsReadOnly = true
            };
            var nameStyle = new Style(typeof(TextBlock));
            nameStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            nameStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            nameStyle.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(12, 0, 0, 0)));
            nameCol.ElementStyle = nameStyle;
            GradesGrid.Columns.Add(nameCol);

            // Колонки дат
            for (int i = 0; i < _lessonDates.Count; i++)
            {
                var colName = GetColumnName(i);
                var header = _lessonDates[i].ToString("dd.MM");

                var gradeCol = new DataGridTextColumn
                {
                    Header = header,
                    Binding = new Binding(colName),
                    Width = 56,
                    IsReadOnly = false
                };

                var displayStyle = new Style(typeof(TextBlock));
                displayStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
                displayStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
                displayStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
                displayStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 14.0));
                displayStyle.Setters.Add(new Setter(TextBlock.BackgroundProperty, new Binding
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.Self),
                    Path = new PropertyPath("Text"),
                    Converter = new GradeColorConverter(),
                    ConverterParameter = "background"
                }));
                displayStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new Binding
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.Self),
                    Path = new PropertyPath("Text"),
                    Converter = new GradeColorConverter(),
                    ConverterParameter = "foreground"
                }));
                gradeCol.ElementStyle = displayStyle;
                GradesGrid.Columns.Add(gradeCol);
            }

            // Средний балл
            var avgCol = new DataGridTextColumn
            {
                Header = "📊 Средний балл",
                Binding = new Binding("Average"),
                Width = 140,
                IsReadOnly = true
            };
            var avgStyle = new Style(typeof(TextBlock));
            avgStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            avgStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            avgStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
            avgStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 15.0));
            avgStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5271FF"))));
            avgCol.ElementStyle = avgStyle;
            GradesGrid.Columns.Add(avgCol);

            GradesGrid.ItemsSource = _journalData.DefaultView;
        }

        #endregion

        #region Редактирование

        private void GradesGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            // Подсвечиваем ячейку при начале редактирования
            if (e.Column != null && e.Row != null)
            {
                var cell = e.Column.GetCellContent(e.Row)?.Parent as DataGridCell;
                if (cell != null)
                {
                    cell.Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#EEF2FF"));
                }
            }
        }

        private void GradesGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            // Настраиваем TextBox для редактирования
            if (e.EditingElement is TextBox textBox)
            {
                textBox.TextAlignment = TextAlignment.Center;
                textBox.FontSize = 14;
                textBox.FontWeight = FontWeights.SemiBold;
                textBox.MaxLength = 1;
                textBox.BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#5271FF"));
                textBox.BorderThickness = new Thickness(2);
                textBox.Background = Brushes.White;
                textBox.VerticalContentAlignment = VerticalAlignment.Center;
                textBox.HorizontalContentAlignment = HorizontalAlignment.Center;

                // Выделяем весь текст при входе в редактирование
                textBox.Loaded += (s, args) =>
                {
                    textBox.SelectAll();
                    textBox.Focus();
                };
            }
        }

        private void GradesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
                return;

            var textBox = e.EditingElement as TextBox;
            if (textBox == null) return;

            var newValue = textBox.Text.Trim().ToUpper();
            if (newValue == "Н")
                newValue = "H";

            if (!IsValidGrade(newValue))
            {
                MessageBox.Show(
                    "Допустимые значения:\n" +
                    "• 2, 3, 4, 5 — оценки\n" +
                    "• H — отсутствие\n" +
                    "• Пусто — нет оценки",
                    "Некорректное значение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Cancel = true;
                return;
            }

            var dataView = GradesGrid.ItemsSource as DataView;
            if (dataView == null || e.Row == null) return;

            var rowView = e.Row.Item as DataRowView;
            if (rowView == null) return;

            var binding = (e.Column as DataGridTextColumn)?.Binding as System.Windows.Data.Binding;
            var columnName = binding?.Path?.Path;
            if (string.IsNullOrEmpty(columnName)) return;

            rowView[columnName] = newValue;

            var studentId = rowView["StudentId"].ToString();
            var cellKey = $"{studentId}_{columnName}";
            _changedCells.Add(cellKey);

            RecalculateAverage(rowView);
            UpdateChangesStatus();
        }

        private bool IsValidGrade(string value)
        {
            return ValidGrades.Contains(value);
        }

        private void RecalculateAverage(DataRowView rowView)
        {
            var grades = new List<int>();
            for (int i = 0; i < _lessonDates.Count; i++)
            {
                var g = rowView[GetColumnName(i)]?.ToString() ?? "";
                if (int.TryParse(g, out int ng))
                    grades.Add(ng);
            }
            rowView["Average"] = grades.Count > 0 ? grades.Average().ToString("F2") : "—";
        }

        private void UpdateChangesStatus()
        {
            if (_changedCells.Count > 0)
            {
                ChangesText.Text = $"⚡ Несохраненных изменений: {_changedCells.Count}";
                ChangesText.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#E65100"));
            }
            else
            {
                ChangesText.Text = "Нет несохраненных изменений";
                ChangesText.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#999"));
            }
        }

        #endregion

        #region Сохранение

        private async void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (_changedCells.Count == 0)
            {
                MessageBox.Show("Нет изменений для сохранения", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                StatusText.Text = "Сохранение изменений...";
                SaveButton.IsEnabled = false;
                GradesGrid.IsEnabled = false;

                var dataView = GradesGrid.ItemsSource as DataView;
                if (dataView == null) return;

                int savedCount = 0;
                int errorCount = 0;

                foreach (DataRowView rowView in dataView)
                {
                    var studentId = Convert.ToInt32(rowView["StudentId"]);

                    for (int i = 0; i < _lessonDates.Count; i++)
                    {
                        var columnName = GetColumnName(i);
                        var date = _lessonDates[i];
                        var cellKey = $"{studentId}_{columnName}";

                        if (!_changedCells.Contains(cellKey))
                            continue;

                        var gradeValue = rowView[columnName]?.ToString() ?? "";

                        try
                        {
                            if (string.IsNullOrEmpty(gradeValue))
                            {
                                // Удаляем оценку, если она существовала
                                await SupabaseClient.Delete("grades",
                                    $"student_id=eq.{studentId}&subject_id=eq.{_selectedSubject.Id}" +
                                    $"&date=eq.{date:yyyy-MM-dd}");
                            }
                            else
                            {
                                // Проверяем, существует ли уже оценка
                                var existingGrade = await SupabaseClient.ExecuteQuery("grades",
                                    $"student_id=eq.{studentId}&subject_id=eq.{_selectedSubject.Id}" +
                                    $"&date=eq.{date:yyyy-MM-dd}&select=id");

                                if (existingGrade.Count > 0)
                                {
                                    // Обновляем существующую
                                    var gradeId = existingGrade[0]["id"].Value<int>();
                                    var updateData = new { grade = gradeValue };
                                    await SupabaseClient.Update("grades",
                                        $"id=eq.{gradeId}", updateData);
                                }
                                else
                                {
                                    // Создаём новую
                                    await SupabaseClient.AddGrade(
                                        studentId,
                                        _selectedSubject.Id,
                                        SupabaseClient.AuthService.CurrentTeacher?.Id ?? 0,
                                        gradeValue,
                                        "current",
                                        null,
                                        date);
                                }
                            }

                            savedCount++;
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            System.Diagnostics.Debug.WriteLine(
                                $"Ошибка сохранения оценки: {ex.Message}");
                        }
                    }
                }

                _changedCells.Clear();
                UpdateChangesStatus();

                if (errorCount > 0)
                {
                    MessageBox.Show(
                        $"Сохранено: {savedCount}\nОшибок: {errorCount}",
                        "Сохранение с ошибками",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    StatusText.Text = $"Успешно сохранено {savedCount} оценок";
                }

                // Обновляем данные из БД
                await LoadJournalData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Ошибка сохранения";
            }
            finally
            {
                SaveButton.IsEnabled = true;
                GradesGrid.IsEnabled = true;
            }
        }

        #endregion

        #region Кнопки управления

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadJournalData();
        }

        private void AddDate_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка для добавления новой даты (открытие диалога)
            MessageBox.Show(
                "Функционал добавления даты будет реализован\n" +
                "через окно управления расписанием",
                "Информация",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateLastUpdateTime()
        {
            LastUpdateText.Text = "только что";
        }

        #endregion

        private void GradesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}