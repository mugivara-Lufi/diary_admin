using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using Diplom.Models;

namespace Diplom.Services
{
    public class ScheduleReportService
    {
        static ScheduleReportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public static byte[] GenerateMonthlyScheduleReportBeautiful(
            DateTime month,
            Dictionary<Class, List<Schedule>> schedulesByClass)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    // Заголовок
                    page.Header()
                        .ShowOnce()
                        .Column(col =>
                        {
                            col.Item().PaddingBottom(10)
                                .Text($"РАСПИСАНИЕ ЗАНЯТИЙ")
                                .FontSize(22)
                                .Bold()
                                .AlignCenter()
                                .FontColor(Colors.Blue.Darken2);

                            col.Item().PaddingBottom(5)
                                .Text($"{month:MMMM yyyy}".ToUpper())
                                .FontSize(16)
                                .Bold()
                                .AlignCenter()
                                .FontColor(Colors.Grey.Darken3);

                            col.Item().PaddingBottom(20)
                                .Text($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}")
                                .FontSize(9)
                                .AlignCenter()
                                .FontColor(Colors.Grey.Medium);

                            col.Item().PaddingBottom(10)
                                .LineHorizontal(1)
                                .LineColor(Colors.Blue.Lighten1);
                        });

                    // Содержание
                    page.Content()
                        .PaddingVertical(10)
                        .Column(col =>
                        {
                            foreach (var classSchedule in schedulesByClass)
                            {
                                var classItem = classSchedule.Key;
                                var schedules = classSchedule.Value;

                                if (schedules.Count == 0)
                                    continue;

                                // Заголовок класса
                                col.Item().PaddingBottom(8).PaddingTop(10)
                                    .Text($"ГРУППА: {classItem.Name}")
                                    .FontSize(14)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken1)
                                    .Underline();

                                // ИСПРАВЛЕНИЕ ЗДЕСЬ: Группировка сразу по дате понедельника (началу недели)
                                var weeks = schedules
                                    .GroupBy(s => GetStartOfWeek(s.LessonDate))
                                    .OrderBy(g => g.Key);

                                foreach (var week in weeks)
                                {
                                    // Ключ группировки (g.Key) теперь и есть дата понедельника для этой группы
                                    var weekStart = week.Key;
                                    var weekEnd = weekStart.AddDays(6); // Воскресенье этой же недели

                                    col.Item().PaddingTop(10).PaddingBottom(8)
                                        .Text($"НЕДЕЛЯ: {weekStart:dd.MM} - {weekEnd:dd.MM}")
                                        .FontSize(11)
                                        .SemiBold()
                                        .FontColor(Colors.Grey.Darken2);

                                    // Группировка по дням недели внутри текущей недели
                                    var days = week.GroupBy(s => s.LessonDate.Date)
                                        .OrderBy(g => g.Key);

                                    foreach (var day in days)
                                    {
                                        var dayOfWeek = GetRussianDayName(day.Key.DayOfWeek);
                                        var dayDate = day.Key.ToString("dd.MM.yyyy");

                                        // Заголовок дня
                                        col.Item().PaddingTop(5).PaddingBottom(5)
                                            .Row(row =>
                                            {
                                                row.RelativeItem().Text($"{dayOfWeek}, {dayDate}")
                                                    .FontSize(11)
                                                    .Bold();
                                            });

                                        // Таблица для дня
                                        col.Item().Table(table =>
                                        {
                                            table.ColumnsDefinition(columns =>
                                            {
                                                columns.RelativeColumn(0.8f);  // №
                                                columns.RelativeColumn(1.2f);  // Время
                                                columns.RelativeColumn(2.5f);  // Предмет
                                                columns.RelativeColumn(2.5f);  // Преподаватель
                                                columns.RelativeColumn(3f);    // Тема
                                            });

                                            int rowNum = 1;
                                            var orderedLessons = day.OrderBy(s => s.LessonNumber);

                                            foreach (var lesson in orderedLessons)
                                            {
                                                var lessonTimes = GetLessonTimes(lesson.LessonNumber);

                                                // Стили для ячеек
                                                var cellStyle = (rowNum % 2 == 0) ? Colors.Grey.Lighten4 : Colors.White;

                                                table.Cell().Background(cellStyle).Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                                    .Text(rowNum.ToString()).FontSize(9);

                                                table.Cell().Background(cellStyle).Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                                    .Text(lessonTimes).FontSize(9).SemiBold();

                                                table.Cell().Background(cellStyle).Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                                    .Text(lesson.SubjectName ?? "—").FontSize(10).Bold();

                                                table.Cell().Background(cellStyle).Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                                    .Text(lesson.TeacherName ?? "—").FontSize(9);

                                                table.Cell().Background(cellStyle).Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                                    .Text(lesson.Topic ?? "—").FontSize(9).FontColor(Colors.Grey.Darken3);

                                                rowNum++;
                                            }
                                        });
                                    }
                                }

                                // Разделитель между группами
                                col.Item().PaddingTop(15).PageBreak();
                            }
                        });

                    // Нижний колонтитул
                    page.Footer()
                        .AlignCenter()
                        .Column(col =>
                        {
                            col.Item().PaddingTop(5)
                                .LineHorizontal(0.5f)
                                .LineColor(Colors.Grey.Lighten2);

                            col.Item().PaddingTop(5)
                                .Text(x =>
                                {
                                    x.Span("Страница ");
                                    x.CurrentPageNumber();
                                    x.Span(" из ");
                                    x.TotalPages();
                                });
                        });
                });
            });

            return document.GeneratePdf();
        }

        // Этот метод находим начало недели (Понедельник) - он написан верно
        private static DateTime GetStartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
        }

        private static string GetLessonTimes(int lessonNumber)
        {
            var times = new Dictionary<int, string>
            {
                { 1, "8:00-9:30" },
                { 2, "9:40-11:10" },
                { 3, "11:20-12:50" },
                { 4, "13:45-15:15" },
                { 5, "15:25-16:55" },
                { 6, "17:05-18:35" },
                { 7, "18:45-20:15" }
            };

            return times.ContainsKey(lessonNumber) ? times[lessonNumber] : $"{lessonNumber} урок";
        }

        private static string GetRussianDayName(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => "Понедельник",
                DayOfWeek.Tuesday => "Вторник",
                DayOfWeek.Wednesday => "Среда",
                DayOfWeek.Thursday => "Четверг",
                DayOfWeek.Friday => "Пятница",
                DayOfWeek.Saturday => "Суббота",
                DayOfWeek.Sunday => "Воскресенье",
                _ => ""
            };
        }
    }
}