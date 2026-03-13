using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Postgrest.Attributes;
using Postgrest.Models;
using Supabase;


namespace Diplom.Models
{
    [Table("students")]
    public class Student : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        public string FullName { get; set; }
        public DateTime? BirthDate { get; set; }
        public int? ClassId { get; set; }
        public string Contact { get; set; }

        public string ClassName { get; set; }
    }

    [Table("parents")]
    public class Parent : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
    }

    [Table("student_parent")]
    public class StudentParent : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int ParentId { get; set; }
        public string Relation { get; set; }
    }

    [Table("teachers")]
    public class Teacher : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        public string FullName { get; set; }
        public int? SubjectId { get; set; }
        public string Email { get; set; }

        public string SubjectName { get; set; }
    }

    [Table("classes")]
    public class Class : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        [Column("name")]
        public string Name { get; set; }
        public int? TeacherId { get; set; }

        public int StudentsCount { get; set; }
        public string TeacherName { get; set; }
    }

    [Table("subjects")]
    public class Subject : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        public string Name { get; set; }

        public int TeachersCount { get; set; }
    }

    [Table("grades")]
    public class Grade : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int SubjectId { get; set; }
        public int TeacherId { get; set; }
        public string GradeValue { get; set; }
        public DateTime Date { get; set; }
        public string Type { get; set; }
        public string Comment { get; set; }


    }

    [Table("attendance")]
    public class Attendance : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int SubjectId { get; set; }
        public DateTime Date { get; set; }
        public bool Present { get; set; }
        public string Comment { get; set; }
    }

    [Table("homework")]
    public class Homework : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        public int SubjectId { get; set; }
        public int ClassId { get; set; }
        public string Task { get; set; }
        public DateTime Deadline { get; set; }
        public DateTime PublishDate { get; set; }
        public string FileLink { get; set; }
        public string Comment { get; set; }
    }

    [Table("homework_status")]
    public class HomeworkStatus : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        public int HomeworkId { get; set; }
        public int StudentId { get; set; }
        public string Status { get; set; }
        public DateTime SubmitDate { get; set; }
        public string Comment { get; set; }
    }

    [Table("schedule")]
    public class Schedule : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        public int? ClassId { get; set; }
        public int? SubjectId { get; set; }
        public DateTime LessonDate { get; set; }
        public int LessonNumber { get; set; }
        public int? TeacherId { get; set; }
        public string Topic { get; set; }


        public string SubjectName { get; set; }

        public string TeacherName { get; set; }
    }

    [Table("portfolio")]
    public class Portfolio : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string AchievementType { get; set; }
        public string Description { get; set; }
        public string FileLink { get; set; }
        public DateTime Date { get; set; }
    }

    [Table("users")]
    public class User : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("login")]
        public string Login { get; set; }

        [Column("password_hash")]
        public string PasswordHash { get; set; }

        [Column("role")]
        public string Role { get; set; }
    }

    [Table("notifications")]
    public class Notification : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        public int ReceiverId { get; set; }
        public string ReceiverType { get; set; }
        public string Message { get; set; }
        public DateTime SendDate { get; set; }
    }
}
