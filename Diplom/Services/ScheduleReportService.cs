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

        public static byte[] GenerateMonthlyScheduleReport(
            DateTime month,
            Dictionary<Class, List<Schedule>> schedulesByClass,
            Dictionary<int, string> subjectsDict,
            Dictionary<int, string> teachersDict)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    // Заголовок
                    page.Header()
                        .ShowOnce()
                        .Column(col =>
                        {
                            col.Item().Text($"Расписание занятий за {month:MMMM yyyy}")
                                .FontSize(18)
                                .Bold()
                                .AlignCenter();

                            col.Item().Text($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}")
                                .FontSize(10)
                                .AlignCenter()
                                .FontColor(Colors.Grey.Darken1);
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

                                // Заголовок класса
                                col.Item().PaddingBottom(5).Text($"Группа: {classItem.Name}")
                                    .FontSize(14)
                                    .Bold()
                                    .Underline();

                                // Группировка по неделям
                                var weeks = schedules.GroupBy(s => GetWeekNumber(s.LessonDate))
                                    .OrderBy(g => g.Key);

                                foreach (var week in weeks)
                                {
                                    var weekStart = GetStartOfWeek(week.First().LessonDate);
                                    var weekEnd = weekStart.AddDays(6);

                                    col.Item().PaddingTop(10).PaddingBottom(5).Text($"Неделя: {weekStart:dd.MM} - {weekEnd:dd.MM}")
                                        .FontSize(12)
                                        .SemiBold();

                                    // Создаем таблицу для недели
                                    col.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(0.5f); // №
                                            columns.RelativeColumn(1.5f); // День
                                            columns.RelativeColumn(1f);   // Время
                                            columns.RelativeColumn(2f);   // Предмет
                                            columns.RelativeColumn(2f);   // Учитель
                                            columns.RelativeColumn(2f);   // Тема
                                        });

                                        // Заголовки таблицы
                                        table.Header(header =>
                                        {
                                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("№").Bold();
                                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("День").Bold();
                                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Время").Bold();
                                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Дисциплина").Bold();
                                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Преподаватель").Bold();
                                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Тема").Bold();
                                        });

                                        // Данные
                                        int rowNum = 1;
                                        var orderedSchedules = week.OrderBy(s => s.LessonDate).ThenBy(s => s.LessonNumber);

                                        foreach (var schedule in orderedSchedules)
                                        {
                                            var lessonTimes = GetLessonTimes(schedule.LessonNumber);
                                            var dayName = GetRussianDayName(schedule.LessonDate.DayOfWeek);
                                            var dateStr = $"{dayName}\n{schedule.LessonDate:dd.MM}";

                                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                                .Text(rowNum.ToString());
                                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                                .Text(dateStr);
                                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                                .Text(lessonTimes);
                                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                                .Text(schedule.SubjectName ?? "—");
                                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                                .Text(schedule.TeacherName ?? "—");
                                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                                .Text(schedule.Topic ?? "—");

                                            rowNum++;
                                        }
                                    });
                                }

                                // Разделитель между классами
                                col.Item().PaddingTop(20).PageBreak();
                            }
                        });

                    // Нумерация страниц
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Страница ");
                            x.CurrentPageNumber();
                        });
                });
            });

            return document.GeneratePdf();
        }

        private static int GetWeekNumber(DateTime date)
        {
            var startOfYear = new DateTime(date.Year, 1, 1);
            int days = (date - startOfYear).Days;
            return (days / 7) + 1;
        }

        private static DateTime GetStartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
        }

        private static string GetLessonTimes(int lessonNumber)
        {
            var times = new Dictionary<int, string>
            {
                { 1, "8:00-8:45" },
                { 2, "8:55-9:40" },
                { 3, "9:50-10:35" },
                { 4, "10:45-11:30" },
                { 5, "11:40-12:25" },
                { 6, "12:35-13:20" },
                { 7, "13:30-14:15" },
                { 8, "14:25-15:10" }
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