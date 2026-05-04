using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Diplom
{
    public partial class CurriculumEditView : UserControl
    {
        private int _planId;
        public event Action PlanSaved;
        public event Action GoBack;

        public CurriculumEditView(int planId = 0, string name = "", int classId = 0, string year = "")
        {
            InitializeComponent();
            _planId = planId;

            LoadClasses();

            if (planId > 0)
            {
                NameBox.Text = name;
                ClassComboBox.SelectedValue = classId;
                if (!string.IsNullOrEmpty(year))
                    YearComboBox.Text = year;
            }
        }

        private async void LoadClasses()
        {
            try
            {
                var result = await SupabaseClient.ExecuteQuery("classes", "select=id,name&order=name");
                var classes = result.Select(c => new { Id = c["id"].Value<int>(), Name = c["name"].ToString() }).ToList();
                ClassComboBox.ItemsSource = classes;

                if (classes.Any() && _planId == 0)
                    ClassComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки групп: {ex.Message}");
            }
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("Введите название плана", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameBox.Focus();
                return false;
            }

            if (ClassComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите группу", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                ClassComboBox.Focus();
                return false;
            }

            return true;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                int classId = (int)ClassComboBox.SelectedValue;
                string year = (YearComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                bool isCurrent = IsCurrentCheck.IsChecked ?? false;

                if (_planId == 0)
                {
                    await SupabaseClient.AddCurriculum(NameBox.Text.Trim(), classId, year, isCurrent);
                    MessageBox.Show("Учебный план успешно создан!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    await SupabaseClient.UpdateCurriculum(_planId, NameBox.Text.Trim(), isCurrent);
                    MessageBox.Show("Учебный план успешно обновлен!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                PlanSaved?.Invoke();
                GoBack?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            GoBack?.Invoke();
        }
    }
}