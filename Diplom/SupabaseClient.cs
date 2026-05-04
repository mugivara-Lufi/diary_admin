using Diplom.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Postgrest.Attributes;
using Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Diplom
{
    public class SupabaseClient
    {
        #region Конфигурация и статический конструктор

        private static readonly string SupabaseUrl = "https://bphgjttgaugascgyimej.supabase.co";
        private static readonly string SupabaseKey = "sb_publishable_67LrH1tn6KzqlCNtHHzNFQ_7SaXqARG";
        private static readonly HttpClient client = new HttpClient();

        static SupabaseClient()
        {
            client.DefaultRequestHeaders.Add("apikey", SupabaseKey);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseKey}");
            client.DefaultRequestHeaders.Add("Prefer", "return=representation");
        }

        #endregion

        #region Базовые CRUD операции

        /// <summary>
        /// GET - Получение данных из таблицы
        /// </summary>
        public static async Task<JArray> ExecuteQuery(string table, string query = "")
        {
            try
            {
                string url = $"{SupabaseUrl}/rest/v1/{table}";
                if (!string.IsNullOrEmpty(query))
                    url += "?" + query;

                using (var cts = new System.Threading.CancellationTokenSource())
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(15));
                    var response = await client.GetAsync(url, cts.Token);
                    var content = await response.Content.ReadAsStringAsync();

                    if (string.IsNullOrWhiteSpace(content))
                        return new JArray();

                    if (!response.IsSuccessStatusCode)
                        throw new Exception($"Статус: {response.StatusCode}, Ответ: {content}");

                    return JArray.Parse(content);
                }
            }
            catch (TaskCanceledException)
            {
                return new JArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Supabase ExecuteQuery error: {ex}");
                return new JArray();
            }
        }

        /// <summary>
        /// GET - Получение одной записи
        /// </summary>
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

        /// <summary>
        /// POST - Добавление данных
        /// </summary>
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

        /// <summary>
        /// PATCH - Обновление данных
        /// </summary>
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

        /// <summary>
        /// DELETE - Удаление данных
        /// </summary>
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

        /// <summary>
        /// Подсчет записей
        /// </summary>
        public static async Task<int> Count(string table, string filter = "")
        {
            try
            {
                string url = $"{SupabaseUrl}/rest/v1/{table}?select=count";
                if (!string.IsNullOrEmpty(filter))
                    url += "&" + filter;

                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return 0;

                var result = JArray.Parse(content);
                return result.Count > 0 ? (int)result[0]["count"] : 0;
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region Аутентификация и управление пользователями

        /// <summary>
        /// Авторизация пользователя
        /// </summary>
        public static async Task<JObject> Login(string login, string password)
        {
            try
            {
                var users = await ExecuteQuery("users", $"login=eq.{login}&select=*");

                if (users.Count == 0)
                    return null;

                var user = users[0] as JObject;
                string passwordHash = user["password_hash"]?.ToString();

                if (string.IsNullOrEmpty(passwordHash))
                    return null;

                if (password == passwordHash)
                    return user;

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка авторизации: {ex.Message}");
            }
        }

        /// <summary>
        /// Создание пользователя
        /// </summary>
        public static async Task<JArray> CreateUser(string login, string password, string role)
        {
            var data = new
            {
                login = login,
                password_hash = password,
                role = role
            };
            return await Insert("users", data);
        }

        /// <summary>
        /// Получение пользователя по логину
        /// </summary>
        public static async Task<JObject> GetUserByLogin(string login)
        {
            var result = await ExecuteQuery("users", $"login=eq.{login}");
            return result.Count > 0 ? result[0] as JObject : null;
        }

        /// <summary>
        /// Получение пользователя по ID
        /// </summary>
        public static async Task<JObject> GetUserById(int userId)
        {
            return await GetSingle("users", $"id=eq.{userId}");
        }

        /// <summary>
        /// Проверка существования логина
        /// </summary>
        public static async Task<bool> IsLoginExists(string login)
        {
            var result = await ExecuteQuery("users", $"login=eq.{login}");
            return result.Count > 0;
        }

        /// <summary>
        /// Сброс пароля пользователя
        /// </summary>
        public static async Task<JArray> ResetUserPassword(int userId, string newPassword)
        {
            var data = new { password_hash = newPassword };
            return await Update("users", $"id=eq.{userId}", data);
        }

        /// <summary>
        /// Активация/деактивация пользователя
        /// </summary>
        public static async Task<JArray> SetUserActive(int userId, bool isActive)
        {
            var data = new { is_active = isActive };
            return await Update("users", $"id=eq.{userId}", data);
        }

        /// <summary>
        /// Генерация логина на основе ФИО
        /// </summary>
        public static string GenerateLogin(string fullName)
        {
            string login = fullName
                .ToLower()
                .Replace(" ", ".")
                .Replace("ё", "e")
                .Replace("й", "i")
                .Replace("ц", "ts")
                .Replace("у", "u")
                .Replace("к", "k")
                .Replace("е", "e")
                .Replace("н", "n")
                .Replace("г", "g")
                .Replace("ш", "sh")
                .Replace("щ", "sch")
                .Replace("з", "z")
                .Replace("х", "h")
                .Replace("ъ", "")
                .Replace("ф", "f")
                .Replace("ы", "y")
                .Replace("в", "v")
                .Replace("а", "a")
                .Replace("п", "p")
                .Replace("р", "r")
                .Replace("о", "o")
                .Replace("л", "l")
                .Replace("д", "d")
                .Replace("ж", "zh")
                .Replace("э", "e")
                .Replace("я", "ya")
                .Replace("ч", "ch")
                .Replace("с", "s")
                .Replace("м", "m")
                .Replace("и", "i")
                .Replace("т", "t")
                .Replace("ь", "")
                .Replace("б", "b")
                .Replace("ю", "yu");

            login = System.Text.RegularExpressions.Regex.Replace(login, @"[^a-z0-9.]", "");
            return login;
        }

        #endregion

        #region Сервис аутентификации

        public static class AuthService
        {
            public static JObject CurrentUser { get; set; }
            public static Teacher CurrentTeacher { get; set; }

            public static async Task<JObject> LoginAsync(string login, string password)
            {
                return await SupabaseClient.Login(login, password);
            }

            public static void Logout()
            {
                CurrentUser = null;
                CurrentTeacher = null;
            }

            public static bool IsAdmin()
            {
                return CurrentUser?["role"]?.ToString() == "admin";
            }

            public static bool IsTeacher()
            {
                return CurrentUser?["role"]?.ToString() == "teacher";
            }
        }

        #endregion

        #region Работа с учениками (Students)

        /// <summary>
        /// Получить всех учеников с классами
        /// </summary>
        public static async Task<JArray> GetStudentsWithClasses()
        {
            return await ExecuteQuery("students",
                "select=id,full_name,birth_date,class_id,contact,classes(name)&order=full_name");
        }

        /// <summary>
        /// Получить ученика с данными пользователя
        /// </summary>
        public static async Task<JObject> GetStudentWithUser(int studentId)
        {
            return await GetSingle("students", $"id=eq.{studentId}&select=*,users(*)");
        }

        /// <summary>
        /// Добавить ученика (без создания пользователя)
        /// </summary>
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

        /// <summary>
        /// Добавить ученика с созданием пользователя
        /// </summary>
        public static async Task<(JArray student, JArray user)> AddStudentWithUser(
            string fullName,
            string birthDate,
            int? classId = null,
            string contact = null,
            string password = null)
        {
            string login = GenerateLogin(fullName);
            int counter = 1;
            string originalLogin = login;

            while (await IsLoginExists(login))
            {
                login = $"{originalLogin}{counter}";
                counter++;
            }

            string userPassword = password ?? "password123";
            var userResult = await CreateUser(login, userPassword, "student");

            if (userResult == null || userResult.Count == 0)
                throw new Exception("Не удалось создать пользователя");

            int userId = userResult[0]["id"].Value<int>();

            var studentData = new
            {
                full_name = fullName,
                birth_date = birthDate,
                class_id = classId,
                contact = contact,
                user_id = userId
            };

            var studentResult = await Insert("students", studentData);
            return (studentResult, userResult);
        }

        /// <summary>
        /// Обновить ученика
        /// </summary>
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

        /// <summary>
        /// Удалить ученика
        /// </summary>
        public static async Task<bool> DeleteStudent(int id)
        {
            return await Delete("students", $"id=eq.{id}");
        }

        #endregion

        #region Работа с родителями (Parents)

        /// <summary>
        /// Получить всех родителей
        /// </summary>
        public static async Task<JArray> GetAllParents()
        {
            return await ExecuteQuery("parents", "select=*&order=full_name");
        }

        /// <summary>
        /// Получить родителя с данными пользователя
        /// </summary>
        public static async Task<JObject> GetParentWithUser(int parentId)
        {
            return await GetSingle("parents", $"id=eq.{parentId}&select=*,users(*)");
        }

        /// <summary>
        /// Добавить родителя (без создания пользователя)
        /// </summary>
        public static async Task<JArray> AddParent(string fullName, string phone = null, string email = null)
        {
            var data = new
            {
                full_name = fullName,
                phone = phone,
                email = email
            };
            return await Insert("parents", data);
        }

        /// <summary>
        /// Добавить родителя с созданием пользователя
        /// </summary>
        public static async Task<(JArray parent, JArray user)> AddParentWithUser(
            string fullName,
            string phone = null,
            string email = null,
            string password = null)
        {
            string login;
            if (!string.IsNullOrEmpty(email))
                login = email.Split('@')[0];
            else if (!string.IsNullOrEmpty(phone))
                login = phone.Replace("+", "").Replace("-", "").Replace(" ", "");
            else
                login = GenerateLogin(fullName);

            int counter = 1;
            string originalLogin = login;

            while (await IsLoginExists(login))
            {
                login = $"{originalLogin}{counter}";
                counter++;
            }

            string userPassword = password ?? "password123";
            var userResult = await CreateUser(login, userPassword, "parent");

            if (userResult == null || userResult.Count == 0)
                throw new Exception("Не удалось создать пользователя");

            int userId = userResult[0]["id"].Value<int>();

            var parentData = new
            {
                full_name = fullName,
                phone = phone,
                email = email,
                user_id = userId
            };

            var parentResult = await Insert("parents", parentData);
            return (parentResult, userResult);
        }

        /// <summary>
        /// Обновить родителя
        /// </summary>
        public static async Task<JArray> UpdateParent(int id, string fullName, string phone = null, string email = null)
        {
            var data = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(fullName)) data["full_name"] = fullName;
            if (!string.IsNullOrEmpty(phone)) data["phone"] = phone;
            if (!string.IsNullOrEmpty(email)) data["email"] = email;

            return await Update("parents", $"id=eq.{id}", data);
        }

        /// <summary>
        /// Удалить родителя
        /// </summary>
        public static async Task<bool> DeleteParent(int id)
        {
            return await Delete("parents", $"id=eq.{id}");
        }

        /// <summary>
        /// Добавить или обновить родителя (upsert)
        /// </summary>
        public static async Task<JArray> UpsertParent(Parent parent)
        {
            if (parent.Id == 0)
                return await AddParent(parent.FullName, parent.Phone, parent.Email);
            else
                return await UpdateParent(parent.Id, parent.FullName, parent.Phone, parent.Email);
        }

        #endregion

        #region Связи ученик-родитель (Student-Parent)

        /// <summary>
        /// Получить родителей ученика
        /// </summary>
        public static async Task<JArray> GetStudentParents(int studentId)
        {
            return await ExecuteQuery("student_parent",
                $"student_id=eq.{studentId}&select=*,parents(*)");
        }

        /// <summary>
        /// Получить учеников родителя
        /// </summary>
        public static async Task<JArray> GetParentStudents(int parentId)
        {
            return await ExecuteQuery("student_parent",
                $"parent_id=eq.{parentId}&select=*,students(*)");
        }

        /// <summary>
        /// Добавить связь ученика с родителем
        /// </summary>
        public static async Task<JArray> AddStudentParent(int studentId, int parentId, string relation = "parent")
        {
            var data = new
            {
                student_id = studentId,
                parent_id = parentId,
                relation = relation
            };
            return await Insert("student_parent", data);
        }

        /// <summary>
        /// Удалить связь ученика с родителем
        /// </summary>
        public static async Task<bool> DeleteStudentParent(int studentId, int parentId)
        {
            return await Delete("student_parent", $"student_id=eq.{studentId}&parent_id=eq.{parentId}");
        }

        #endregion

        #region Работа с учителями (Teachers)

        /// <summary>
        /// Получить всех учителей с предметами
        /// </summary>
        public static async Task<JArray> GetTeachersWithSubjects()
        {
            return await ExecuteQuery("teachers", "select=*,subjects(name)");
        }

        /// <summary>
        /// Получить учителя с данными пользователя
        /// </summary>
        public static async Task<JObject> GetTeacherWithUser(int teacherId)
        {
            return await GetSingle("teachers", $"id=eq.{teacherId}&select=*,users(*)");
        }

        /// <summary>
        /// Добавить учителя (без создания пользователя)
        /// </summary>
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

        /// <summary>
        /// Добавить учителя с созданием пользователя
        /// </summary>
        public static async Task<(JArray teacher, JArray user)> AddTeacherWithUser(
            string fullName,
            int? subjectId = null,
            string email = null,
            string password = null)
        {
            string login;
            if (!string.IsNullOrEmpty(email))
                login = email.Split('@')[0];
            else
                login = GenerateLogin(fullName);

            int counter = 1;
            string originalLogin = login;

            while (await IsLoginExists(login))
            {
                login = $"{originalLogin}{counter}";
                counter++;
            }

            string userPassword = password ?? "password123";
            var userResult = await CreateUser(login, userPassword, "teacher");

            if (userResult == null || userResult.Count == 0)
                throw new Exception("Не удалось создать пользователя");

            int userId = userResult[0]["id"].Value<int>();

            var teacherData = new
            {
                full_name = fullName,
                subject_id = subjectId,
                email = email,
                user_id = userId
            };

            var teacherResult = await Insert("teachers", teacherData);
            return (teacherResult, userResult);
        }

        /// <summary>
        /// Обновить учителя
        /// </summary>
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

        /// <summary>
        /// Удалить учителя
        /// </summary>
        public static async Task<bool> DeleteTeacher(int id)
        {
            return await Delete("teachers", $"id=eq.{id}");
        }

        #endregion

        #region Работа с классами (Classes)

        /// <summary>
        /// Получить все классы с учителями
        /// </summary>
        public static async Task<JArray> GetClassesWithTeachers()
        {
            return await ExecuteQuery("classes", "select=*,teachers(full_name)");
        }

        /// <summary>
        /// Добавить класс
        /// </summary>
        public static async Task<JArray> AddClass(string name, int? teacherId = null)
        {
            var data = new
            {
                name = name,
                teacher_id = teacherId
            };
            return await Insert("classes", data);
        }

        /// <summary>
        /// Обновить класс
        /// </summary>
        public static async Task<JArray> UpdateClass(int id, string name, int? teacherId)
        {
            var data = new
            {
                name = name,
                teacher_id = teacherId
            };
            return await Update("classes", $"id=eq.{id}", data);
        }

        /// <summary>
        /// Удалить класс
        /// </summary>
        public static async Task<bool> DeleteClass(int id)
        {
            return await Delete("classes", $"id=eq.{id}");
        }

        #endregion

        #region Работа с предметами (Subjects)

        /// <summary>
        /// Получить все предметы
        /// </summary>
        public static async Task<JArray> GetAllSubjects()
        {
            return await ExecuteQuery("subjects", "select=*&order=name");
        }

        /// <summary>
        /// Добавить предмет
        /// </summary>
        public static async Task<JArray> AddSubject(string name)
        {
            var data = new { name = name };
            return await Insert("subjects", data);
        }

        /// <summary>
        /// Обновить предмет
        /// </summary>
        public static async Task<JArray> UpdateSubject(int id, string name)
        {
            var data = new { name = name };
            return await Update("subjects", $"id=eq.{id}", data);
        }

        /// <summary>
        /// Удалить предмет
        /// </summary>
        public static async Task<bool> DeleteSubject(int id)
        {
            return await Delete("subjects", $"id=eq.{id}");
        }

        #endregion

        #region Работа с оценками (Grades)

        /// <summary>
        /// Получить оценки ученика
        /// </summary>
        public static async Task<JArray> GetStudentGrades(int studentId)
        {
            return await ExecuteQuery("grades",
                $"student_id=eq.{studentId}&select=*,subjects(name),teachers(full_name)&order=date.desc");
        }

        /// <summary>
        /// Добавить оценку
        /// </summary>
        public static async Task<JArray> AddGrade(int studentId, int subjectId, int teacherId,
            string grade, string type, string comment, DateTime date)
        {
            var data = new
            {
                student_id = studentId,
                subject_id = subjectId,
                teacher_id = teacherId,
                grade = grade,
                date = date.ToString("yyyy-MM-dd"),
                type = type,
                comment = comment
            };
            return await Insert("grades", data);
        }

        #endregion

        #region Работа с расписанием (Schedule)

        /// <summary>
        /// Получить расписание класса
        /// </summary>
        public static async Task<JArray> GetClassSchedule(int classId, string date)
        {
            return await ExecuteQuery("schedule",
                $"class_id=eq.{classId}&lesson_date=eq.{date}&select=*,subjects(name),teachers(full_name)&order=lesson_number");
        }

        /// <summary>
        /// Добавить урок в расписание
        /// </summary>
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

        /// <summary>
        /// Обновить урок в расписании
        /// </summary>
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

        #endregion

        #region Работа с домашними заданиями (Homework)

        /// <summary>
        /// Получить домашние задания класса
        /// </summary>
        public static async Task<JArray> GetClassHomework(int classId)
        {
            return await ExecuteQuery("homework",
                $"class_id=eq.{classId}&select=*,subjects(name)&order=deadline.asc");
        }

        /// <summary>
        /// Добавить домашнее задание
        /// </summary>
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

        #endregion

        #region Работа с посещаемостью (Attendance)

        /// <summary>
        /// Получить посещаемость ученика
        /// </summary>
        public static async Task<JArray> GetStudentAttendance(int studentId, string dateFrom, string dateTo)
        {
            return await ExecuteQuery("attendance",
                $"student_id=eq.{studentId}&date=gte.{dateFrom}&date=lte.{dateTo}&select=*,subjects(name)&order=date.desc");
        }

        /// <summary>
        /// Отметить посещаемость
        /// </summary>
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

        #endregion

        #region Работа с уведомлениями (Notifications)

        /// <summary>
        /// Получить уведомления пользователя
        /// </summary>
        public static async Task<JArray> GetUserNotifications(int userId, string userType)
        {
            return await ExecuteQuery("notifications",
                $"receiver_id=eq.{userId}&receiver_type=eq.{userType}&order=send_date.desc&limit=50");
        }

        /// <summary>
        /// Отправить уведомление
        /// </summary>
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

        #endregion

        #region Работа с портфолио (Portfolio)

        /// <summary>
        /// Получить портфолио ученика
        /// </summary>
        public static async Task<JArray> GetStudentPortfolio(int studentId)
        {
            return await ExecuteQuery("portfolio",
                $"student_id=eq.{studentId}&order=date.desc");
        }

        #endregion

        #region Статистика и аналитика

        /// <summary>
        /// Подсчет учителей по предметам
        /// </summary>
        public static async Task<Dictionary<int, int>> GetTeachersCountBySubjectAlternative()
        {
            var countsDict = new Dictionary<int, int>();

            try
            {
                var subjects = await ExecuteQuery("subjects", "select=id");

                foreach (var subjectObj in subjects)
                {
                    int subjectId = subjectObj["id"].Value<int>();
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
                Debug.WriteLine($"Ошибка подсчета учителей: {ex.Message}");
            }

            return countsDict;
        }

        /// <summary>
        /// Подсчет студентов по классам
        /// </summary>
        public static async Task<Dictionary<int, int>> GetStudentsCountByClassAlternative()
        {
            var countsDict = new Dictionary<int, int>();

            try
            {
                var classes = await ExecuteQuery("classes", "select=id");

                foreach (var classObj in classes)
                {
                    int classId = classObj["id"].Value<int>();
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
                Debug.WriteLine($"Ошибка подсчета студентов: {ex.Message}");
            }

            return countsDict;
        }

        #endregion


        public static async Task<bool> SendLoginCredentials(string contact, string login, string password, string fullName, string role)
        {
            var notificationService = new Services.NotificationService();
            return await notificationService.SendLoginCredentials(contact, login, password, fullName, role);
        }

        public static bool IsValidEmail(string email)
        {
            return Services.NotificationService.IsValidEmail(email);
        }

        public static bool IsValidPhone(string phone)
        {
            return Services.NotificationService.IsValidPhone(phone);
        }




        #region Учебный план

        /// <summary>
        /// Получить предметы из учебного плана для группы на семестр
        /// </summary>
        public static async Task<JArray> GetSubjectsByCurriculum(int classId, int semester)
        {
            // Сначала получаем текущий учебный план для класса
            var curricula = await ExecuteQuery("curricula",
                $"class_id=eq.{classId}&is_current=eq.true&select=id");

            if (curricula == null || curricula.Count == 0)
                return new JArray();

            int curriculumId = curricula[0]["id"].Value<int>();

            return await ExecuteQuery("curriculum_subjects",
                $"curriculum_id=eq.{curriculumId}&semester=eq.{semester}&select=*,subjects(*)");
        }

        /// <summary>
        /// Получить всех преподавателей, закрепленных за предметами учебного плана группы
        /// </summary>
        public static async Task<JArray> GetTeachersByCurriculum(int classId, int semester)
        {
            // Получаем текущий учебный план
            var curricula = await ExecuteQuery("curricula",
                $"class_id=eq.{classId}&is_current=eq.true&select=id");

            if (curricula == null || curricula.Count == 0)
                return new JArray();

            int curriculumId = curricula[0]["id"].Value<int>();

            return await ExecuteQuery("subject_teachers",
                $"curriculum_subject_id=in.(SELECT id FROM curriculum_subjects WHERE curriculum_id=eq.{curriculumId} AND semester=eq.{semester})&select=*,teachers(*)");
        }
        /// <summary>
        /// Получить текущий семестр группы
        /// </summary>
        public static async Task<int> GetCurrentSemester(int classId)
        {
            // Если поле semester отсутствует в classes, можно вычислять по учебному году
            // или всегда возвращать 1-2 семестр в зависимости от текущего месяца
            int month = DateTime.Now.Month;
            return month >= 9 || month <= 1 ? 1 : 2; // 1 семестр: сентябрь-январь, 2 семестр: февраль-июнь
        }

        /// <summary>
        /// Получить учебный план группы с предметами на семестр
        /// </summary>
        public static async Task<JArray> GetCurriculumWithSubjects(int classId, int semester)
        {
            var curricula = await ExecuteQuery("curricula",
                $"class_id=eq.{classId}&is_current=eq.true&select=id");

            if (curricula == null || curricula.Count == 0)
                return new JArray();

            int curriculumId = curricula[0]["id"].Value<int>();

            return await ExecuteQuery("curriculum_subjects",
                $"curriculum_id=eq.{curriculumId}&semester=eq.{semester}&select=*,subjects(*),subject_teachers(*,teachers(*))");
        }

        #endregion

        #region Учебный план

        /// <summary>
        /// Получить учебный план группы
        /// </summary>
        public static async Task<JArray> GetCurriculumByClass(int classId)
        {
            return await ExecuteQuery("curricula",
                $"class_id=eq.{classId}&is_current=eq.true&select=*,curriculum_subjects(*,subjects(*),subject_teachers(*,teachers(*)))");
        }

        /// <summary>
        /// Получить список предметов для группы по семестру
        /// </summary>
        public static async Task<List<int>> GetSubjectIdsByClass(int classId, int semester)
        {
            var curriculum = await GetCurriculumByClass(classId);
            if (curriculum == null || curriculum.Count == 0) return new List<int>();

            var curriculumId = curriculum[0]["id"].Value<int>();

            var subjects = await ExecuteQuery("curriculum_subjects",
                $"curriculum_id=eq.{curriculumId}&semester=eq.{semester}&select=subject_id");

            var subjectIds = new List<int>();
            foreach (var item in subjects)
            {
                subjectIds.Add(item["subject_id"].Value<int>());
            }
            return subjectIds;
        }

        /// <summary>
        /// Добавить учебный план для группы
        /// </summary>
        public static async Task<JArray> AddCurriculum(string name, int classId, string academicYear, bool isCurrent = false)
        {
            var data = new
            {
                name = name,
                class_id = classId,
                academic_year = academicYear,
                is_current = isCurrent
            };
            return await Insert("curricula", data);
        }

        /// <summary>
        /// Обновить учебный план
        /// </summary>
        public static async Task<JArray> UpdateCurriculum(int id, string name, bool isCurrent)
        {
            var data = new { name = name, is_current = isCurrent };
            return await Update("curricula", $"id=eq.{id}", data);
        }

        /// <summary>
        /// Удалить учебный план
        /// </summary>
        public static async Task<bool> DeleteCurriculum(int id)
        {
            return await Delete("curricula", $"id=eq.{id}");
        }

        /// <summary>
        /// Получить все учебные планы с деталями
        /// </summary>
        public static async Task<JArray> GetCurriculaWithDetails()
        {
            return await ExecuteQuery("curricula",
                "select=*,classes(name),curriculum_subjects(id)");
        }

        /// <summary>
        /// Получить учебный план по ID с деталями
        /// </summary>
        public static async Task<JObject> GetCurriculumById(int id)
        {
            return await GetSingle("curricula",
                $"id=eq.{id}&select=*,classes(name),curriculum_subjects(*,subjects(*),subject_teachers(*,teachers(*)))");
        }

        /// <summary>
        /// Добавить предмет в учебный план
        /// </summary>
        public static async Task<JArray> AddCurriculumSubject(int curriculumId, int subjectId, int semester,
            int hoursPerWeek = 2, int totalHours = 68, string attestationType = "credit")
        {
            var data = new
            {
                curriculum_id = curriculumId,
                subject_id = subjectId,
                semester = semester,
                hours_per_week = hoursPerWeek,
                total_hours = totalHours,
                attestation_type = attestationType
            };
            return await Insert("curriculum_subjects", data);
        }

        /// <summary>
        /// Удалить предмет из учебного плана
        /// </summary>
        public static async Task<bool> DeleteCurriculumSubject(int id)
        {
            return await Delete("curriculum_subjects", $"id=eq.{id}");
        }

        /// <summary>
        /// Назначить преподавателя на дисциплину
        /// </summary>
        public static async Task<JArray> AssignTeacherToSubject(int curriculumSubjectId, int teacherId, bool isMain = true)
        {
            var data = new
            {
                curriculum_subject_id = curriculumSubjectId,
                teacher_id = teacherId,
                is_main = isMain
            };
            return await Insert("subject_teachers", data);
        }

        /// <summary>
        /// Получить преподавателей для дисциплины
        /// </summary>
        public static async Task<JArray> GetTeachersForCurriculumSubject(int curriculumSubjectId)
        {
            return await ExecuteQuery("subject_teachers",
                $"curriculum_subject_id=eq.{curriculumSubjectId}&select=*,teachers(*)");
        }

        /// <summary>
        /// Удалить преподавателя с дисциплины
        /// </summary>
        public static async Task<bool> RemoveTeacherFromSubject(int subjectTeacherId)
        {
            return await Delete("subject_teachers", $"id=eq.{subjectTeacherId}");
        }

        #endregion
    }
}