using Diplom.Models;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Diplom
{
    public partial class HomeworkEditView : UserControl
    {
        private Class _class;
        private Subject _subject;
        private HomeworkItem _existingHomework;

        // События для навигации
        public event Action GoBack;
        public event Action<bool> SaveCompleted;

        public HomeworkEditView(Class selectedClass, Subject selectedSubject,
            HomeworkItem homework = null)
        {
            InitializeComponent();

            _class = selectedClass;
            _subject = selectedSubject;
            _existingHomework = homework;

            ClassNameText.Text = _class.Name;
            SubjectNameText.Text = _subject.Name;

            if (homework != null)
            {
                TitleText.Text = "✏️ Редактирование задания";
                SubtitleText.Text = "Измените данные домашнего задания";
                TaskTextBox.Text = homework.Task;
                DeadlinePicker.SelectedDate = homework.Deadline;
                FileLinkTextBox.Text = homework.FileLink ?? "";
                CommentTextBox.Text = homework.Comment ?? "";
            }
            else
            {
                TitleText.Text = "➕ Новое задание";
                SubtitleText.Text = "Заполните информацию о задании";
                DeadlinePicker.SelectedDate = DateTime.Now.AddDays(7);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TaskTextBox.Text))
            {
                MessageBox.Show("Введите текст задания", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TaskTextBox.Focus();
                return;
            }

            if (DeadlinePicker.SelectedDate == null)
            {
                MessageBox.Show("Выберите срок сдачи", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                DeadlinePicker.Focus();
                return;
            }

            try
            {
                var task = TaskTextBox.Text.Trim();
                var deadline = DeadlinePicker.SelectedDate.Value;
                var fileLink = FileLinkTextBox.Text.Trim();
                var comment = CommentTextBox.Text.Trim();

                if (_existingHomework != null)
                {
                    var updateData = new
                    {
                        task = task,
                        deadline = deadline.ToString("yyyy-MM-dd"),
                        file_link = string.IsNullOrEmpty(fileLink) ? null : fileLink,
                        comment = string.IsNullOrEmpty(comment) ? null : comment
                    };
                    await SupabaseClient.Update("homework",
                        $"id=eq.{_existingHomework.Id}", updateData);
                }
                else
                {
                    await SupabaseClient.AddHomework(
                        _subject.Id,
                        _class.Id,
                        task,
                        deadline.ToString("yyyy-MM-dd"),
                        string.IsNullOrEmpty(fileLink) ? null : fileLink,
                        string.IsNullOrEmpty(comment) ? null : comment);
                }

                SaveCompleted?.Invoke(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SaveCompleted?.Invoke(false);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            GoBack?.Invoke();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            GoBack?.Invoke();
        }
    }
}