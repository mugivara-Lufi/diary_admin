using Diplom.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Diplom
{
    public partial class CurriculumView : UserControl
    {
        private ObservableCollection<CurriculumViewModel> _curricula;

        public CurriculumView()
        {
            InitializeComponent();
            _curricula = new ObservableCollection<CurriculumViewModel>();
            CurriculumGrid.ItemsSource = _curricula;

            Loaded += async (s, e) => await LoadCurriculaAsync();
        }

        private async System.Threading.Tasks.Task LoadCurriculaAsync()
        {
            try
            {
                var result = await SupabaseClient.GetCurriculaWithDetails();
                _curricula.Clear();

                foreach (var item in result)
                {
                    _curricula.Add(new CurriculumViewModel
                    {
                        Id = item["id"].ToObject<int>(),
                        Name = item["name"].ToString(),
                        ClassId = item["class_id"].ToObject<int>(),
                        ClassName = item["classes"]?["name"]?.ToString(),
                        AcademicYear = item["academic_year"]?.ToString(),
                        IsCurrent = item["is_current"].ToObject<bool>(),
                        SubjectsCount = item["curriculum_subjects"]?.Count() ?? 0
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
            }
        }

        private ContentControl GetParentContentControl(DependencyObject child)
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is ContentControl contentControl)
                    return contentControl;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private void CreatePlanButton_Click(object sender, RoutedEventArgs e)
        {
            var parentContentControl = GetParentContentControl(this);
            if (parentContentControl == null) return;

            var currentView = parentContentControl.Content;

            var editView = new CurriculumEditView();
            editView.PlanSaved += async () =>
            {
                parentContentControl.Content = currentView;
                await LoadCurriculaAsync();
            };
            editView.GoBack += () => parentContentControl.Content = currentView;

            parentContentControl.Content = editView;
        }

        private void EditPlanButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurriculumGrid.SelectedItem is not CurriculumViewModel plan) return;

            var parentContentControl = GetParentContentControl(this);
            if (parentContentControl == null) return;

            var currentView = parentContentControl.Content;

            var editView = new CurriculumEditView(plan.Id, plan.Name, plan.ClassId, plan.AcademicYear);
            editView.PlanSaved += async () =>
            {
                parentContentControl.Content = currentView;
                await LoadCurriculaAsync();
            };
            editView.GoBack += () => parentContentControl.Content = currentView;

            parentContentControl.Content = editView;
        }

        private void EditSubjects_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not CurriculumViewModel plan) return;

            var parentContentControl = GetParentContentControl(this);
            if (parentContentControl == null) return;

            var currentView = parentContentControl.Content;

            var subjectsView = new CurriculumSubjectsView(plan.Id, plan.Name);
            subjectsView.GoBack += () => parentContentControl.Content = currentView;

            parentContentControl.Content = subjectsView;
        }

        private async void DeletePlanButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurriculumGrid.SelectedItem is not CurriculumViewModel plan) return;

            var result = MessageBox.Show($"Удалить план '{plan.Name}'?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await SupabaseClient.DeleteCurriculum(plan.Id);
                await LoadCurriculaAsync();
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadCurriculaAsync();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text?.Trim().ToLower() ?? "";

            if (string.IsNullOrEmpty(searchText))
            {
                CurriculumGrid.ItemsSource = _curricula;
            }
            else
            {
                var filtered = _curricula.Where(c =>
                    c.Name.ToLower().Contains(searchText) ||
                    (c.ClassName?.ToLower().Contains(searchText) ?? false)).ToList();
                CurriculumGrid.ItemsSource = filtered;
            }
        }

        private void CurriculumGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Можно добавить логику при выборе, если нужно
        }
    }

    public class CurriculumViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int ClassId { get; set; }
        public string ClassName { get; set; }
        public string AcademicYear { get; set; }
        public bool IsCurrent { get; set; }
        public int SubjectsCount { get; set; }
    }

    public class BoolToYesNoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? "Да" : "Нет";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value.ToString() == "Да";
        }
    }
}