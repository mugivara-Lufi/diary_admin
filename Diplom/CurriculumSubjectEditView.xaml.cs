using Diplom.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Diplom
{
    public partial class CurriculumSubjectEditView : UserControl
    {
        private int _curriculumId;
        private int _subjectId;
        private CurriculumSubjectViewModel _subject;
        public event Action SubjectSaved;

        public CurriculumSubjectEditView(int curriculumId, CurriculumSubjectViewModel subject = null)
        {
            InitializeComponent();
            _curriculumId = curriculumId;
            _subject = subject;

            LoadSubjects();

            if (subject != null)
            {
                _subjectId = subject.Id;
                SubjectComboBox.SelectedValue = subject.SubjectId;
                SemesterComboBox.SelectedIndex = subject.Semester == 1 ? 0 : 1;
                HoursPerWeekBox.Text = subject.HoursPerWeek.ToString();
                TotalHoursBox.Text = subject.TotalHours.ToString();
                AttestationComboBox.SelectedIndex = subject.AttestationType == "exam" ? 1 : 0;
            }
        }

        private async void LoadSubjects()
        {
            var result = await SupabaseClient.GetAllSubjects();
            SubjectComboBox.ItemsSource = result.Select(s => new { Id = s["id"].Value<int>(), Name = s["name"].ToString() }).ToList();

            if (SubjectComboBox.Items.Count > 0 && _subject == null)
                SubjectComboBox.SelectedIndex = 0;
        }

        private bool ValidateForm()
        {
            if (SubjectComboBox.SelectedValue == null)
            {
                MessageBox.Show("Выберите дисциплину");
                return false;
            }

            if (!int.TryParse(HoursPerWeekBox.Text, out int hours) || hours <= 0)
            {
                MessageBox.Show("Введите корректное количество часов в неделю");
                return false;
            }

            if (!int.TryParse(TotalHoursBox.Text, out int total) || total <= 0)
            {
                MessageBox.Show("Введите корректное количество часов за семестр");
                return false;
            }

            return true;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                int subjectId = (int)SubjectComboBox.SelectedValue;
                int semester = SemesterComboBox.SelectedIndex + 1;
                int hoursPerWeek = int.Parse(HoursPerWeekBox.Text);
                int totalHours = int.Parse(TotalHoursBox.Text);
                string attestation = ((ComboBoxItem)AttestationComboBox.SelectedItem).Tag.ToString();

                if (_subject == null)
                {
                    await SupabaseClient.AddCurriculumSubject(_curriculumId, subjectId, semester, hoursPerWeek, totalHours, attestation);
                }
                else
                {
                    // Обновление
                    var data = new
                    {
                        subject_id = subjectId,
                        semester = semester,
                        hours_per_week = hoursPerWeek,
                        total_hours = totalHours,
                        attestation_type = attestation
                    };
                    await SupabaseClient.Update("curriculum_subjects", $"id=eq.{_subjectId}", data);
                }

                SubjectSaved?.Invoke();

                var window = Window.GetWindow(this);
                window?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            window?.Close();
        }


    }
}