using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Diplom
{
    public class SupabaseClient
    {
        // ⚠️ ЗАМЕНИТЕ НА ВАШИ ДАННЫЕ ИЗ SUPABASE
        private static readonly string SupabaseUrl = "https://bphgjttgaugascgyimej.supabase.co";
        private static readonly string SupabaseKey = "sb_publishable_67LrH1tn6KzqlCNtHHzNFQ_7SaXqARG";

        private static readonly HttpClient client = new HttpClient();

        static SupabaseClient()
        {
            client.DefaultRequestHeaders.Add("apikey", SupabaseKey);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseKey}");
            client.DefaultRequestHeaders.Add("Prefer", "return=representation");
        }


        #region мусор
        // ============ GET - Получение данных ============
        public static async Task<JArray> ExecuteQuery(string table, string query = "")
        {
            try
            {
                string url = $"{SupabaseUrl}/rest/v1/{table}";
                if (!string.IsNullOrEmpty(query))
                    url += "?" + query;

                // Таймаут на каждый запрос
                using (var cts = new System.Threading.CancellationTokenSource())
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(15)); // под себя

                    var response = await client.GetAsync(url, cts.Token);
                    var content = await response.Content.ReadAsStringAsync();

                    // Если Supabase вернул 200, но тело пустое — считаем это допустимым (нет данных)
                    if (string.IsNullOrWhiteSpace(content))
                        return new JArray();

                    if (!response.IsSuccessStatusCode)
                        throw new Exception($"Статус: {response.StatusCode}, Ответ: {content}");

                    return JArray.Parse(content);
                }
            }
            catch (TaskCanceledException)
            {
                // Таймаут. Не валим приложение, а возвращаем пустой массив.
                return new JArray();
            }
            catch (Exception ex)
            {
                // Здесь лучше логировать в файл / консоль, а не кидать дальше.
                // Если хочешь всё же увидеть ошибку в UI — верни пустой массив и покажи диалог выше по стеку.
                System.Diagnostics.Debug.WriteLine($"Supabase ExecuteQuery error: {ex}");
                return new JArray();
            }
        }


        // ============ GET - Получение одной записи ============
        public static async Task<JObject> GetSingle(string table, string query)
        {
            try
            {
                var result = await ExecuteQuery(table, query);
                return result.Count > 0 ? result[0] as JObject : null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка GetSingle: {ex.Message}");
            }
        }

        // ============ POST - Добавление данных ============
        public static async Task<JArray> Insert(string table, object data)
        {
            try
            {
                string url = $"{SupabaseUrl}/rest/v1/{table}";
                string jsonData = JsonConvert.SerializeObject(data);
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Ошибка вставки: {response.StatusCode}, {responseContent}");
                }

                return JArray.Parse(responseContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка Insert: {ex.Message}");
            }
        }

        // ============ PATCH - Обновление данных ============
        public static async Task<JArray> Update(string table, string query, object data)
        {
            try
            {
                string url = $"{SupabaseUrl}/rest/v1/{table}?{query}";
                string jsonData = JsonConvert.SerializeObject(data);
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                var response = await client.PatchAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Ошибка обновления: {response.StatusCode}, {responseContent}");
                }

                return JArray.Parse(responseContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка Update: {ex.Message}");
            }
        }

        // ============ DELETE - Удаление данных ============
        public static async Task<bool> Delete(string table, string query)
        {
            try
            {
                string url = $"{SupabaseUrl}/rest/v1/{table}?{query}";

                var response = await client.DeleteAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Ошибка удаления: {response.StatusCode}, {responseContent}");
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка Delete: {ex.Message}");
            }
        }

        // ============ ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ============

        // Подсчет записей
        public static async Task<int> Count(string table, string filter = "")
        {
            try
            {
                string url = $"{SupabaseUrl}/rest/v1/{table}?select=count";
                if (!string.IsNullOrEmpty(filter))
                {
                    url += "&" + filter;
                }

                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return 0;
                }

                var result = JArray.Parse(content);
                return result.Count > 0 ? (int)result[0]["count"] : 0;
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        // ============ АВТОРИЗАЦИЯ ============
        public static async Task<JObject> Login(string login, string password)
        {
            try
            {
                // Получаем пользователя по логину
                var users = await ExecuteQuery("users", $"login=eq.{login}&select=*");

                if (users.Count == 0)
                {
                    return null;
                }

                var user = users[0] as JObject;
                string passwordHash = user["password_hash"]?.ToString();

                // Проверяем что хеш пароля существует
                if (string.IsNullOrEmpty(passwordHash))
                {
                    return null;
                }

                // Простое сравнение пароля (без хеширования)
                if (password == passwordHash)
                {
                    return user;
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка авторизации: {ex.Message}");
            }
        }
        public static class AuthService
        {
            public static JObject CurrentUser { get; set; }

            public static async Task<JObject> LoginAsync(string login, string password)
            {
                return await SupabaseClient.Login(login, password);
            }

            public static void Logout()
            {
                CurrentUser = null;
            }

            public static bool IsAdmin()
            {
                return CurrentUser?["role"]?.ToString() == "admin";
            }
        }

        // ============ СПЕЦИФИЧНЫЕ МЕТОДЫ ДЛЯ ШКОЛЫ ============

        // Получить учеников с классами
        public static async Task<JArray> GetStudentsWithClasses()
        {
            // Правильный запрос с JOIN через внешние ключи
            return await ExecuteQuery("students",
                "select=id,full_name,birth_date,class_id,contact,classes(name)&order=full_name");
        }

        // Получить учителей с предметами
        public static async Task<JArray> GetTeachersWithSubjects()
        {
            return await ExecuteQuery("teachers", "select=*,subjects(name)");
        }

        // Получить оценки ученика
        public static async Task<JArray> GetStudentGrades(int studentId)
        {
            return await ExecuteQuery("grades",
                $"student_id=eq.{studentId}&select=*,subjects(name),teachers(full_name)&order=date.desc");
        }

        // Получить расписание класса
        public static async Task<JArray> GetClassSchedule(int classId, string date)
        {
            return await ExecuteQuery("schedule",
                $"class_id=eq.{classId}&lesson_date=eq.{date}&select=*,subjects(name),teachers(full_name)&order=lesson_number");
        }

        // Получить домашние задания класса
        public static async Task<JArray> GetClassHomework(int classId)
        {
            return await ExecuteQuery("homework",
                $"class_id=eq.{classId}&select=*,subjects(name)&order=deadline.asc");
        }

        // Получить уведомления пользователя
        public static async Task<JArray> GetUserNotifications(int userId, string userType)
        {
            return await ExecuteQuery("notifications",
                $"receiver_id=eq.{userId}&receiver_type=eq.{userType}&order=send_date.desc&limit=50");
        }

        // Получить родителей ученика
        public static async Task<JArray> GetStudentParents(int studentId)
        {
            return await ExecuteQuery("student_parent",
                $"student_id=eq.{studentId}&select=*,parents(*)");
        }

        // Получить посещаемость ученика
        public static async Task<JArray> GetStudentAttendance(int studentId, string dateFrom, string dateTo)
        {
            return await ExecuteQuery("attendance",
                $"student_id=eq.{studentId}&date=gte.{dateFrom}&date=lte.{dateTo}&select=*,subjects(name)&order=date.desc");
        }

        // Получить портфолио ученика
        public static async Task<JArray> GetStudentPortfolio(int studentId)
        {
            return await ExecuteQuery("portfolio",
                $"student_id=eq.{studentId}&order=date.desc");
        }

        // Добавить оценку
        public static async Task<JArray> AddGrade(int studentId, int subjectId, int teacherId,
            string grade, string type, string comment = null)
        {
            var data = new
            {
                student_id = studentId,
                subject_id = subjectId,
                teacher_id = teacherId,
                grade = grade,
                date = DateTime.Now.ToString("yyyy-MM-dd"),
                type = type,
                comment = comment
            };

            return await Insert("grades", data);
        }

        // Добавить домашнее задание
        public static async Task<JArray> AddHomework(int subjectId, int classId, string task,
            string deadline, string fileLink = null, string comment = null)
        {
            var data = new
            {
                subject_id = subjectId,
                class_id = classId,
                task = task,
                deadline = deadline,
                publish_date = DateTime.Now.ToString("yyyy-MM-dd"),
                file_link = fileLink,
                comment = comment
            };

            return await Insert("homework", data);
        }

        // Отметить посещаемость
        public static async Task<JArray> MarkAttendance(int studentId, int subjectId,
            bool present, string comment = null)
        {
            var data = new
            {
                student_id = studentId,
                subject_id = subjectId,
                date = DateTime.Now.ToString("yyyy-MM-dd"),
                present = present,
                comment = comment
            };

            return await Insert("attendance", data);
        }

        // Отправить уведомление
        public static async Task<JArray> SendNotification(int receiverId, string receiverType, string message)
        {
            var data = new
            {
                receiver_id = receiverId,
                receiver_type = receiverType,
                message = message,
                send_date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            return await Insert("notifications", data);
        }

        // Добавить ученика
        public static async Task<JArray> AddStudent(string fullName, string birthDate,
            int? classId = null, string contact = null)
        {
            var data = new
            {
                full_name = fullName,
                birth_date = birthDate,
                class_id = classId,
                contact = contact
            };

            return await Insert("students", data);
        }

        // Добавить учителя
        public static async Task<JArray> AddTeacher(string fullName, int? subjectId = null, string email = null)
        {
            var data = new
            {
                full_name = fullName,
                subject_id = subjectId,
                email = email
            };

            return await Insert("teachers", data);
        }

        // Обновить ученика
        public static async Task<JArray> UpdateStudent(int id, string fullName = null,
            string birthDate = null, int? classId = null, string contact = null)
        {
            var data = new Dictionary<string, object>();

            if (fullName != null) data["full_name"] = fullName;
            if (birthDate != null) data["birth_date"] = birthDate;
            if (classId != null) data["class_id"] = classId;
            if (contact != null) data["contact"] = contact;

            return await Update("students", $"id=eq.{id}", data);
        }
        public static async Task<JArray> UpdateTeacher(int id, string fullName, int? subjectId, string email)
        {
            var data = new
            {
                full_name = fullName,
                subject_id = subjectId,
                email = email
            };
            return await Update("teachers", $"id=eq.{id}", data);
        }

        // Удалить ученика
        public static async Task<bool> DeleteStudent(int id)
        {
            return await Delete("students", $"id=eq.{id}");
        }

        // Удалить учителя
        public static async Task<bool> DeleteTeacher(int id)
        {
            return await Delete("teachers", $"id=eq.{id}");
        }
        // Получить классы с учителями
        public static async Task<JArray> GetClassesWithTeachers()
        {
            return await ExecuteQuery("classes", "select=*,teachers(full_name)");
        }

        // Удалить класс
        public static async Task<bool> DeleteClass(int id)
        {
            return await Delete("classes", $"id=eq.{id}");
        }

        // Добавить класс
        public static async Task<JArray> AddClass(string name, int? teacherId = null)
        {
            var data = new
            {
                name = name,
                teacher_id = teacherId
            };
            return await Insert("classes", data);
        }

        // Обновить класс
        public static async Task<JArray> UpdateClass(int id, string name, int? teacherId)
        {
            var data = new
            {
                name = name,
                teacher_id = teacherId
            };
            return await Update("classes", $"id=eq.{id}", data);
        }

        // Получить все предметы
        public static async Task<JArray> GetAllSubjects()
        {
            return await ExecuteQuery("subjects", "select=*&order=name");
        }

        // Удалить предмет
        public static async Task<bool> DeleteSubject(int id)
        {
            return await Delete("subjects", $"id=eq.{id}");
        }

        // Добавить предмет
        public static async Task<JArray> AddSubject(string name)
        {
            var data = new
            {
                name = name
            };
            return await Insert("subjects", data);
        }

        // Обновить предмет
        public static async Task<JArray> UpdateSubject(int id, string name)
        {
            var data = new
            {
                name = name
            };
            return await Update("subjects", $"id=eq.{id}", data);
        }

        // Добавить урок в расписание
        public static async Task<JArray> AddSchedule(int classId, int subjectId, int teacherId,
            DateTime lessonDate, int lessonNumber, string topic = null)
        {
            var data = new
            {
                class_id = classId,
                subject_id = subjectId,
                teacher_id = teacherId,
                lesson_date = lessonDate.ToString("yyyy-MM-dd"),
                lesson_number = lessonNumber,
                topic = topic
            };
            return await Insert("schedule", data);
        }
        // подсчет учителей
        public static async Task<Dictionary<int, int>> GetTeachersCountBySubjectAlternative()
        {
            var countsDict = new Dictionary<int, int>();

            try
            {
                // Получаем все предметы
                var subjects = await ExecuteQuery("subjects", "select=id");

                foreach (var subjectObj in subjects)
                {
                    int subjectId = subjectObj["id"].Value<int>();

                    // Подсчитываем учителей для каждого предмета
                    string countUrl = $"{SupabaseUrl}/rest/v1/teachers?subject_id=eq.{subjectId}&select=count";
                    var response = await client.GetAsync(countUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var countArray = JArray.Parse(content);

                        int count = countArray.Count > 0 ? countArray[0]["count"].Value<int>() : 0;
                        countsDict[subjectId] = count;
                    }
                    else
                    {
                        countsDict[subjectId] = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                // В случае ошибки возвращаем пустые счетчики
                Console.WriteLine($"Ошибка подсчета учителей: {ex.Message}");
            }

            return countsDict;
        }

        // подсчет студентов
        public static async Task<Dictionary<int, int>> GetStudentsCountByClassAlternative()
        {
            var countsDict = new Dictionary<int, int>();

            try
            {
                // Получаем все классы
                var classes = await ExecuteQuery("classes", "select=id");

                foreach (var classObj in classes)
                {
                    int classId = classObj["id"].Value<int>();

                    // Подсчитываем студентов для каждого класса
                    string countUrl = $"{SupabaseUrl}/rest/v1/students?class_id=eq.{classId}&select=count";
                    var response = await client.GetAsync(countUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var countArray = JArray.Parse(content);

                        int count = countArray.Count > 0 ? countArray[0]["count"].Value<int>() : 0;
                        countsDict[classId] = count;
                    }
                    else
                    {
                        countsDict[classId] = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                // В случае ошибки возвращаем пустые счетчики
                Console.WriteLine($"Ошибка подсчета студентов: {ex.Message}");
            }

            return countsDict;
        }


        // Обновить урок в расписании
        public static async Task<JArray> UpdateSchedule(int id, int subjectId, int teacherId,
            DateTime lessonDate, int lessonNumber, string topic = null)
        {
            var data = new
            {
                subject_id = subjectId,
                teacher_id = teacherId,
                lesson_date = lessonDate.ToString("yyyy-MM-dd"),
                lesson_number = lessonNumber,
                topic = topic
            };
            return await Update("schedule", $"id=eq.{id}", data);
        }






    }
}
