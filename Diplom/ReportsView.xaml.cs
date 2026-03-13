using iTextSharp.text.pdf;
using iTextSharp.text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
using Paragraph = iTextSharp.text.Paragraph;
using Path = System.IO.Path;
using System.Printing;
using Newtonsoft.Json.Linq;

namespace Diplom
{
    public partial class ReportsView : UserControl
    {
        private void InitDefaultPeriod()
        {
            var today = DateTime.Today;
            StartDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
            EndDatePicker.SelectedDate = today;
        }

        private List<Diplom.Models.Class> _classes = new List<Diplom.Models.Class>();

        public string CurrentDateTime => DateTime.Now.ToString("dd.MM.yyyy HH:mm");

        public ReportsView()
        {
            InitializeComponent();
            UpdateClassFilterVisibility();
            DataContext = this;
            InitDefaultPeriod();
            _ = LoadClassesAndSummaryAsync();
        }

        private async System.Threading.Tasks.Task LoadClassesAndSummaryAsync()
        {
            await LoadClassesAsync();
            await LoadSummaryAsync();
        }

        private async System.Threading.Tasks.Task LoadClassesAsync()
        {
            try
            {
                var result = await SupabaseClient.ExecuteQuery(
                    "classes",
                    "select=id,name&order=name"
                );

                _classes.Clear();
                foreach (var item in result)
                {
                    _classes.Add(new Diplom.Models.Class
                    {
                        Id = item["id"]?.ToObject<int>() ?? 0,
                        Name = item["name"]?.ToString() ?? ""
                    });
                }

                ClassComboBox.ItemsSource = _classes;
                if (_classes.Count > 0)
                    ClassComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки классов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadSummaryAsync()
        {
            try
            {
                // количество преподавателей
                var teachers = await SupabaseClient.ExecuteQuery("teachers", "select=id");
                TeachersCountText.Text = teachers?.Count.ToString() ?? "0";

                // количество студентов
                var students = await SupabaseClient.ExecuteQuery("students", "select=id");
                StudentsCountText.Text = students?.Count.ToString() ?? "0";

                // количество занятий за период
                if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue)
                {
                    string from = StartDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd");
                    string to = EndDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd");
                    var lessons = await SupabaseClient.ExecuteQuery(
                        "schedule",
                        $"lesson_date=gte.{from}&lesson_date=lte.{to}&select=id"
                    );
                    LessonsCountText.Text = lessons?.Count.ToString() ?? "0";
                }

                StatusText.Text = "Статистика обновлена";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка загрузки статистики";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshData_Click(object sender, RoutedEventArgs e)
        {
            await LoadSummaryAsync();
            await GenerateReportInternalAsync();
        }

        private async void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            await GenerateReportInternalAsync();
        }

        private async System.Threading.Tasks.Task GenerateReportInternalAsync()
        {
            if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Укажите период отчёта.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string from = StartDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd");
            string to = EndDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd");

            string type = ((ReportTypeComboBox.SelectedItem as ComboBoxItem)?.Content as string) ?? "Общая статистика";

            // ДЕБАГ: Вывод базовой информации
            string debugInfo = $"Тип отчета: {type}\nПериод: {from} - {to}\n";
            Console.WriteLine(debugInfo);

            try
            {
                if (type == "Посещаемость")
                {
                    int? selectedClassId = (ClassComboBox.SelectedItem as Diplom.Models.Class)?.Id;
                    if (selectedClassId == null || selectedClassId == 0)
                    {
                        MessageBox.Show("Выберите класс для отчёта по посещаемости.",
                            "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // Сначала получаем студентов выбранного класса
                    var studentsResult = await SupabaseClient.ExecuteQuery(
                        "students",
                        $"class_id=eq.{selectedClassId}&select=id,full_name"
                    );

                    if (studentsResult == null || studentsResult.Count == 0)
                    {
                        ReportGrid.ItemsSource = null;
                        StatusText.Text = "В выбранном классе нет студентов";
                        return;
                    }

                    var studentIds = studentsResult.Select(s => s["id"]?.ToObject<int>()).Where(id => id.HasValue).Cast<int>().ToList();

                    if (studentIds.Count == 0)
                    {
                        ReportGrid.ItemsSource = null;
                        StatusText.Text = "Не удалось получить список студентов";
                        return;
                    }

                    // Формируем запрос для посещаемости
                    string studentFilter = string.Join(",", studentIds);
                    var attendanceResult = await SupabaseClient.ExecuteQuery(
                        "attendance",
                        $"date=gte.{from}&date=lte.{to}&student_id=in.({studentFilter})" +
                        "&select=date,present,comment,student_id,students(full_name),subjects(name)" +
                        "&order=date.desc"
                    );

                    if (attendanceResult == null || attendanceResult.Count == 0)
                    {
                        ReportGrid.ItemsSource = null;
                        StatusText.Text = "Записей по посещаемости за выбранный период нет";
                        return;
                    }

                    var rows = attendanceResult.Select(item => new
                    {
                        Дата = item["date"]?.ToString()?.Split('T')[0] ?? "",
                        Ученик = item["students"]?["full_name"]?.ToString() ?? "",
                        Предмет = item["subjects"]?["name"]?.ToString() ?? "",
                        Присутствие = (item["present"]?.ToObject<bool>() ?? false) ? "Присутствовал" : "Отсутствовал",
                        Комментарий = item["comment"]?.ToString() ?? ""
                    })
                    .OrderBy(r => r.Дата)
                    .ThenBy(r => r.Ученик)
                    .ToList();

                    ReportGrid.ItemsSource = rows;
                    StatusText.Text = $"Записей по посещаемости: {rows.Count}";
                }
                else if (type == "Успеваемость")
                {
                    int? selectedClassId = (ClassComboBox.SelectedItem as Diplom.Models.Class)?.Id;
                    if (selectedClassId == null || selectedClassId == 0)
                    {
                        MessageBox.Show("Выберите класс для отчёта по успеваемости.",
                            "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // 1. Получаем студентов выбранного класса
                    var studentsResult = await SupabaseClient.ExecuteQuery(
                        "students",
                        $"class_id=eq.{selectedClassId}&select=id,full_name"
                    );

                    if (studentsResult == null || studentsResult.Count == 0)
                    {
                        ReportGrid.ItemsSource = null;
                        StatusText.Text = "В выбранном классе нет студентов";
                        return;
                    }

                    var studentMap = studentsResult
                        .Select(s => new
                        {
                            Id = s["id"]?.ToObject<int>() ?? 0,
                            Name = s["full_name"]?.ToString() ?? ""
                        })
                        .Where(x => x.Id != 0)
                        .ToDictionary(x => x.Id, x => x.Name);

                    if (studentMap.Count == 0)
                    {
                        ReportGrid.ItemsSource = null;
                        StatusText.Text = "Не удалось получить список студентов";
                        return;
                    }

                    string studentFilter = string.Join(",", studentMap.Keys);

                    // 2. Получаем оценки (без join'ов, только id)
                    var gradesResult = await SupabaseClient.ExecuteQuery(
                        "grades",
                        $"date=gte.{from}&date=lte.{to}&student_id=in.({studentFilter})" +
                        "&select=id,student_id,subject_id,teacher_id,grade,date,type,comment" +
                        "&order=date.desc"
                    );

                    System.Diagnostics.Debug.WriteLine($"Grades rows from supabase: {gradesResult?.Count ?? 0}");

                    if (gradesResult == null || gradesResult.Count == 0)
                    {
                        ReportGrid.ItemsSource = null;
                        StatusText.Text = "Оценок за выбранный период нет";
                        return;
                    }

                    // 3. Для простоты покажем только ФИО + оценка, без предметов/учителей
                    var rows = gradesResult.Select(item =>
                    {
                        int studentId = item["student_id"]?.ToObject<int>() ?? 0;
                        string studentName = studentMap.ContainsKey(studentId) ? studentMap[studentId] : $"ID {studentId}";

                        return new
                        {
                            Дата = item["date"]?.ToString()?.Split('T')[0] ?? "",
                            Ученик = studentName,
                            Оценка = item["grade"]?.ToString() ?? "",
                            Тип = item["type"]?.ToString() ?? "",
                            Комментарий = item["comment"]?.ToString() ?? ""
                        };
                    })
                    .OrderBy(r => r.Дата)
                    .ThenBy(r => r.Ученик)
                    .ToList();

                    ReportGrid.ItemsSource = rows;
                    StatusText.Text = $"Оценок в отчёте: {rows.Count}";
                }

                else if (type == "Нагрузка преподавателей")
                {
                    var result = await SupabaseClient.ExecuteQuery(
                        "schedule",
                        $"lesson_date=gte.{from}&lesson_date=lte.{to}&select=teacher_id,teachers(full_name)"
                    );

                    var dict = new Dictionary<string, int>();

                    foreach (var item in result)
                    {
                        string teacher = "Не указан";

                        // безопасно читаем teachers.full_name
                        var teachersToken = item["teachers"];
                        if (teachersToken != null && teachersToken.Type == JTokenType.Object)
                        {
                            var nameToken = teachersToken["full_name"];
                            if (nameToken != null && nameToken.Type != JTokenType.Null)
                                teacher = nameToken.ToString();
                        }

                        if (!dict.ContainsKey(teacher))
                            dict[teacher] = 0;

                        dict[teacher] += 1;
                    }

                    var rows = dict.Select(p => new { Преподаватель = p.Key, Занятий = p.Value })
                                 .OrderByDescending(x => x.Занятий)
                                 .ToList();
                    ReportGrid.ItemsSource = rows;
                    StatusText.Text = $"Преподавателей в отчёте: {rows.Count}";
                }
                else // Общая статистика — покажем список классов
                {
                    var result = await SupabaseClient.ExecuteQuery(
                        "classes",
                        "select=name,students(count)"
                    );

                    var rows = result.Select(item => new
                    {
                        Класс = item["name"]?.ToString() ?? "",
                        Студентов = item["students"]?[0]?["count"]?.ToObject<int?>() ?? 0
                    })
                    .OrderBy(r => r.Класс)
                    .ToList();

                    ReportGrid.ItemsSource = rows;
                    StatusText.Text = $"Классов в отчёте: {rows.Count}";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка формирования отчёта";
                MessageBox.Show($"Ошибка: {ex.Message}\n\nДетали: {ex.InnerException?.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExportToPdf_Click(object sender, RoutedEventArgs e)
        {
            if (ReportGrid.ItemsSource == null)
            {
                MessageBox.Show("Нет данных для экспорта.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = $"Отчет_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                    DefaultExt = ".pdf"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // Используем Dispatcher для обновления UI
                    Dispatcher.Invoke(() => StatusText.Text = "Формирование PDF...");

                    await Task.Run(() => CreateProfessionalPdfReport(saveDialog.FileName));

                    // Используем Dispatcher для обновления UI
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = $"PDF экспортирован: {System.IO.Path.GetFileName(saveDialog.FileName)}";
                    });

                    MessageBox.Show("Отчет успешно экспортирован в PDF", "Экспорт завершен",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    // Открываем файл
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveDialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => StatusText.Text = "Ошибка экспорта");
                MessageBox.Show($"Ошибка при экспорте PDF: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateProfessionalPdfReport(string filePath)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            // Получаем данные в основном потоке перед запуском фоновой задачи
            var reportData = Dispatcher.Invoke(() =>
            {
                string reportType = ((ReportTypeComboBox.SelectedItem as ComboBoxItem)?.Content as string) ?? "Общая статистика";
                string startDate = StartDatePicker.SelectedDate?.ToString("dd.MM.yyyy") ?? "Не указан";
                string endDate = EndDatePicker.SelectedDate?.ToString("dd.MM.yyyy") ?? "Не указан";
                string className = GetSelectedClassName();
                var itemsSource = ReportGrid.ItemsSource;

                return new
                {
                    ReportType = reportType,
                    StartDate = startDate,
                    EndDate = endDate,
                    ClassName = className,
                    ItemsSource = itemsSource
                };
            });

            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                var document = new Document(PageSize.A4.Rotate(), 20, 20, 30, 30);
                var writer = PdfWriter.GetInstance(document, fs);

                document.Open();

                // Шрифты
                BaseFont baseFont = BaseFont.CreateFont("c:/windows/fonts/arial.ttf", BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                var titleFont = new Font(baseFont, 16, Font.BOLD, BaseColor.BLACK);
                var headerFont = new Font(baseFont, 12, Font.BOLD, BaseColor.WHITE);
                var normalFont = new Font(baseFont, 10, Font.NORMAL, BaseColor.BLACK);
                var smallFont = new Font(baseFont, 8, Font.NORMAL, BaseColor.DARK_GRAY);

                // Заголовок отчета
                var title = new Paragraph("ОТЧЕТ ЭЛЕКТРОННОГО ЖУРНАЛА", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 15f
                };
                document.Add(title);

                // Информация об отчете
                string period = $"Период: {reportData.StartDate} - {reportData.EndDate}";
                string generatedTime = $"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}";

                var infoTable = new PdfPTable(2)
                {
                    WidthPercentage = 100,
                    SpacingAfter = 15f
                };
                infoTable.SetWidths(new float[] { 1, 1 });

                infoTable.AddCell(CreateInfoCell($"Тип отчета: {reportData.ReportType}", normalFont));
                infoTable.AddCell(CreateInfoCell(period, normalFont));
                infoTable.AddCell(CreateInfoCell($"Класс: {reportData.ClassName}", normalFont));
                infoTable.AddCell(CreateInfoCell(generatedTime, normalFont));

                document.Add(infoTable);

                // Основная таблица данных
                if (reportData.ItemsSource != null)
                {
                    var dataTable = CreateDataTable(reportData.ItemsSource, headerFont, normalFont);
                    document.Add(dataTable);
                }

                // Подпись
                document.Add(new Paragraph("\n"));
                var signature = new Paragraph("Отчет сформирован автоматически системой электронного журнала", smallFont)
                {
                    Alignment = Element.ALIGN_RIGHT
                };
                document.Add(signature);

                // Номер страницы
                document.Add(new Paragraph($"Страница 1", smallFont)
                {
                    Alignment = Element.ALIGN_CENTER
                });

                document.Close();
            }
        }

        private string GetSelectedClassName()
        {
            var selectedClass = ClassComboBox.SelectedItem as Diplom.Models.Class;
            return selectedClass?.Name ?? "Не выбран";
        }

        private PdfPTable CreateDataTable(object itemsSource, Font headerFont, Font normalFont)
        {
            if (itemsSource == null) return new PdfPTable(1);

            // Определяем колонки на основе типа данных
            var firstItem = (itemsSource as System.Collections.IEnumerable)?.Cast<object>().FirstOrDefault();
            if (firstItem == null) return new PdfPTable(1);

            var properties = firstItem.GetType().GetProperties();
            int columnCount = properties.Length;

            var table = new PdfPTable(columnCount)
            {
                WidthPercentage = 100,
                SpacingBefore = 10f,
                SpacingAfter = 10f
            };

            // Устанавливаем ширины колонок
            float[] widths = new float[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                widths[i] = 1f;
            }
            table.SetWidths(widths);

            // Заголовки таблицы
            foreach (var property in properties)
            {
                table.AddCell(CreateHeaderCell(property.Name, headerFont));
            }

            // Данные таблицы
            int rowCount = 0;
            foreach (var item in (itemsSource as System.Collections.IEnumerable))
            {
                rowCount++;

                // Чередование цветов строк
                BaseColor rowColor = rowCount % 2 == 0 ? new BaseColor(248, 249, 250) : BaseColor.WHITE;

                foreach (var property in properties)
                {
                    var value = property.GetValue(item)?.ToString() ?? "";
                    table.AddCell(CreateDataCell(value, normalFont, rowColor));
                }
            }

            // Статистика внизу таблицы
            if (rowCount > 0)
            {
                var statsCell = new PdfPCell(new Phrase($"Всего записей: {rowCount}", normalFont))
                {
                    Colspan = columnCount,
                    BackgroundColor = new BaseColor(240, 240, 240),
                    BorderWidth = 0,
                    Padding = 8,
                    HorizontalAlignment = Element.ALIGN_RIGHT
                };
                table.AddCell(statsCell);
            }

            return table;
        }

        private PdfPCell CreateInfoCell(string text, Font font)
        {
            return new PdfPCell(new Phrase(text, font))
            {
                Padding = 5,
                BorderWidth = 0,
                BackgroundColor = BaseColor.WHITE
            };
        }

        private PdfPCell CreateHeaderCell(string text, Font font)
        {
            return new PdfPCell(new Phrase(text, font))
            {
                Padding = 8,
                BorderWidth = 0.5f,
                BorderColor = new BaseColor(200, 200, 200),
                BackgroundColor = new BaseColor(82, 113, 255),
                HorizontalAlignment = Element.ALIGN_CENTER
            };
        }

        private PdfPCell CreateDataCell(string text, Font font, BaseColor backgroundColor)
        {
            return new PdfPCell(new Phrase(text, font))
            {
                Padding = 6,
                BorderWidth = 0.5f,
                BorderColor = new BaseColor(200, 200, 200),
                BackgroundColor = backgroundColor,
                HorizontalAlignment = Element.ALIGN_LEFT
            };
        }


        private void UpdateClassFilterVisibility()
        {
            string type = ((ReportTypeComboBox.SelectedItem as ComboBoxItem)?.Content as string) ?? "";
            // Класс нужен только для посещаемости и успеваемости
            if (type == "Посещаемость" || type == "Успеваемость")
                ClassFilterPanel.Visibility = Visibility.Visible;
            else
                ClassFilterPanel.Visibility = Visibility.Collapsed;
        }

        private void ReportTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateClassFilterVisibility();
        }
    }
}