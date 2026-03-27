using Diplom.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Diplom
{
    public partial class StudentEditView : UserControl
    {
        public Student Student { get; set; }
        public ObservableCollection<Class> Classes { get; set; }
        public ObservableCollection<Parent> Parents { get; set; }

        private List<Parent> _removedParents = new List<Parent>();

        public event Action<bool> SaveCompleted;
        public event Action GoBack;

        public StudentEditView(Student student = null)
        {
            InitializeComponent();
            DataContext = this;

            Student = student ?? new Student();
            Classes = new ObservableCollection<Class>();
            Parents = new ObservableCollection<Parent>();

            // Подписываемся на событие загрузки элементов
            ParentsItemsControl.Loaded += ParentsItemsControl_Loaded;

            Loaded += async (s, e) => await InitializeAsync();
        }

        private void ParentsItemsControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateParentVisibility();
        }

        private void UpdateParentVisibility()
        {
            // Обновляем видимость телефона и email для каждого родителя
            if (ParentsItemsControl?.Items != null)
            {
                foreach (var item in ParentsItemsControl.Items)
                {
                    var container = ParentsItemsControl.ItemContainerGenerator.ContainerFromItem(item);
                    if (container != null)
                    {
                        var border = FindVisualChild<Border>(container);
                        if (border != null)
                        {
                            var phoneText = FindVisualChild<TextBlock>(border, "PhoneText");
                            var emailText = FindVisualChild<TextBlock>(border, "EmailText");

                            if (phoneText != null && item is Parent parent)
                            {
                                phoneText.Visibility = string.IsNullOrWhiteSpace(parent.Phone)
                                    ? Visibility.Collapsed : Visibility.Visible;
                            }

                            if (emailText != null && item is Parent parent2)
                            {
                                emailText.Visibility = string.IsNullOrWhiteSpace(parent2.Email)
                                    ? Visibility.Collapsed : Visibility.Visible;
                            }
                        }
                    }
                }
            }
        }

        // Вспомогательный метод для поиска дочерних элементов
        private T FindVisualChild<T>(DependencyObject parent, string name = null) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && (string.IsNullOrEmpty(name) || typedChild.Name == name))
                    return typedChild;

                var result = FindVisualChild<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            try
            {
                await LoadClassesAsync();
                await LoadParentsAsync();
                UpdateTitle();
                UpdateEmptyParentsMessage();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadClassesAsync()
        {
            try
            {
                var result = await SupabaseClient.ExecuteQuery("classes", "select=*&order=name");
                Classes.Clear();

                Classes.Add(new Class { Id = 0, Name = "Не назначен" });

                foreach (var item in result)
                {
                    var classItem = new Class
                    {
                        Id = item["id"].Value<int>(),
                        Name = item["name"]?.ToString()
                    };
                    Classes.Add(classItem);
                }

                ClassComboBox.ItemsSource = Classes;

                if (Student.ClassId.HasValue && Student.ClassId.Value > 0)
                {
                    ClassComboBox.SelectedValue = Student.ClassId.Value;
                }
                else
                {
                    ClassComboBox.SelectedValue = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки классов: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadParentsAsync()
        {
            if (Student.Id == 0) return;

            try
            {
                var result = await SupabaseClient.GetStudentParents(Student.Id);
                Parents.Clear();

                foreach (var item in result)
                {
                    var parentData = item["parents"];
                    var parent = new Parent
                    {
                        Id = parentData["id"].Value<int>(),
                        FullName = parentData["full_name"]?.ToString(),
                        Phone = parentData["phone"]?.ToString(),
                        Email = parentData["email"]?.ToString()
                    };
                    Parents.Add(parent);
                }

                ParentsItemsControl.ItemsSource = Parents;
                UpdateEmptyParentsMessage();
                UpdateParentVisibility();
            }
            catch (Exception ex)
            {
                // Если нет родителей - просто игнорируем ошибку
            }
        }

        private void UpdateEmptyParentsMessage()
        {
            if (EmptyParentsText != null)
            {
                EmptyParentsText.Visibility = Parents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateTitle()
        {
            TitleText.Text = Student.Id > 0 ? "Редактирование ученика" : "Добавление ученика";
            SaveButton.Content = Student.Id > 0 ? "Обновить" : "Создать";
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(Student.FullName))
            {
                MessageBox.Show("Введите ФИО ученика", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                FullNameTextBox.Focus();
                return false;
            }

            if (Student.FullName.Length < 2)
            {
                MessageBox.Show("ФИО должно содержать не менее 2 символов", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                FullNameTextBox.Focus();
                return false;
            }

            return true;
        }

        // ============ МЕТОДЫ ДЛЯ РАБОТЫ С РОДИТЕЛЯМИ ============

        private async void AddParentButton_Click(object sender, RoutedEventArgs e)
        {
            var parentEditDialog = new ParentEditView();
            var dialogWindow = new Window
            {
                Title = "Создание родителя",
                Content = parentEditDialog,
                Width = 550,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            parentEditDialog.ParentSaved += async (newParent) =>
            {
                // Добавляем родителя в список
                Parents.Add(newParent);
                ParentsItemsControl.ItemsSource = null;
                ParentsItemsControl.ItemsSource = Parents;
                UpdateEmptyParentsMessage();
                UpdateParentVisibility();

                // Если студент уже существует, сразу сохраняем связь
                if (Student.Id > 0)
                {
                    try
                    {
                        await SupabaseClient.AddStudentParent(Student.Id, newParent.Id);
                        MessageBox.Show("Родитель добавлен и привязан к ученику", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка привязки родителя: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                dialogWindow.Close();
            };

            parentEditDialog.Canceled += () => dialogWindow.Close();
            dialogWindow.ShowDialog();
        }

        private async void SelectExistingParent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Загрузка и фильтрация данных (LINQ делает код чище)
                var allParents = await SupabaseClient.GetAllParents();
                var existingParents = allParents
                    .Select(item => new Parent
                    {
                        Id = item["id"].Value<int>(),
                        FullName = item["full_name"]?.ToString() ?? "Без имени",
                        Phone = item["phone"]?.ToString(),
                        Email = item["email"]?.ToString()
                    })
                    .Where(p => !Parents.Any(existing => existing.Id == p.Id))
                    .ToList();

                if (!existingParents.Any())
                {
                    MessageBox.Show("Нет доступных родителей для добавления.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 2. Создание окна с улучшенным дизайном
                var selectDialog = new Window
                {
                    Title = "Выбор родителя",
                    Width = 450,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F7FA")), // Светло-серый фон
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false
                };

                var mainPanel = new Grid { Margin = new Thickness(20) };
                mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Заголовок
                mainPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Список
                mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Кнопки

                // Заголовок
                var titleText = new TextBlock
                {
                    Text = "Выберите родителя",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D3748")),
                    Margin = new Thickness(0, 0, 0, 20)
                };
                Grid.SetRow(titleText, 0);
                mainPanel.Children.Add(titleText);

                // 3. Настройка ListBox с шаблоном карточек
                var listBox = new ListBox
                {
                    ItemsSource = existingParents,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(0, 0, 0, 20),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                };

                // Создаем DataTemplate для элементов списка (Карточки)
                var cardTemplate = new DataTemplate(typeof(Parent));
                var borderFactory = new FrameworkElementFactory(typeof(Border));
                borderFactory.SetValue(Border.BackgroundProperty, Brushes.White);
                borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
                borderFactory.SetValue(Border.PaddingProperty, new Thickness(15));
                borderFactory.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 10));
                borderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")));
                borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));

                var stackFactory = new FrameworkElementFactory(typeof(StackPanel));

                // Имя
                var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
                nameFactory.SetBinding(TextBlock.TextProperty, new Binding("FullName"));
                nameFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
                nameFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
                nameFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D3748")));

                // Телефон
                var phoneFactory = new FrameworkElementFactory(typeof(TextBlock));
                phoneFactory.SetBinding(TextBlock.TextProperty, new Binding("Phone"));
                phoneFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
                phoneFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#718096")));
                phoneFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 4, 0, 0));

                stackFactory.AppendChild(nameFactory);
                stackFactory.AppendChild(phoneFactory);
                borderFactory.AppendChild(stackFactory);
                cardTemplate.VisualTree = borderFactory;
                listBox.ItemTemplate = cardTemplate;

                Grid.SetRow(listBox, 1);
                mainPanel.Children.Add(listBox);

                // 4. Панель кнопок
                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

                var cancelButton = new Button
                {
                    Content = "Отмена",
                    Width = 100,
                    Height = 35,
                    Margin = new Thickness(0, 0, 10, 0),
                    Style = (Style)FindResource("SecondaryButton") // Используем ваш стиль
                };
                cancelButton.Click += (s, a) => selectDialog.Close();

                var selectButton = new Button
                {
                    Content = "Выбрать",
                    Width = 100,
                    Height = 35,
                    Style = (Style)FindResource("PrimaryButton") // Используем ваш стиль
                };

                selectButton.Click += async (s, args) =>
                {
                    if (listBox.SelectedItem is Parent selected)
                    {
                        Parents.Add(selected);
                        // Обновляем UI родительского окна
                        ParentsItemsControl.ItemsSource = null;
                        ParentsItemsControl.ItemsSource = Parents;
                        UpdateEmptyParentsMessage();
                        UpdateParentVisibility();

                        if (Student.Id > 0)
                        {
                            try { await SupabaseClient.AddStudentParent(Student.Id, selected.Id); }
                            catch (Exception ex) { MessageBox.Show(ex.Message); }
                        }
                        selectDialog.Close();
                    }
                };

                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(selectButton);
                Grid.SetRow(buttonPanel, 2);
                mainPanel.Children.Add(buttonPanel);

                selectDialog.Content = mainPanel;
                selectDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditParent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Parent parent)
            {
                var parentEditDialog = new ParentEditView(parent);
                var dialogWindow = new Window
                {
                    Title = "Редактирование родителя",
                    Content = parentEditDialog,
                    Width = 550,
                    Height = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false
                };

                parentEditDialog.ParentSaved += async (updatedParent) =>
                {
                    // Обновляем данные родителя в списке
                    var index = Parents.IndexOf(parent);
                    if (index >= 0)
                    {
                        Parents[index] = updatedParent;
                        ParentsItemsControl.ItemsSource = null;
                        ParentsItemsControl.ItemsSource = Parents;
                        UpdateParentVisibility();
                    }

                    MessageBox.Show("Данные родителя обновлены", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    dialogWindow.Close();
                };

                parentEditDialog.Canceled += () => dialogWindow.Close();
                dialogWindow.ShowDialog();
            }
        }

        private async void RemoveParent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Parent parent)
            {
                var result = MessageBox.Show($"Удалить родителя {parent.FullName} из списка?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Если студент уже существует, добавляем в список на удаление
                    if (Student.Id > 0)
                    {
                        _removedParents.Add(parent);
                    }

                    Parents.Remove(parent);
                    ParentsItemsControl.ItemsSource = null;
                    ParentsItemsControl.ItemsSource = Parents;
                    UpdateEmptyParentsMessage();

                    MessageBox.Show("Родитель удален из списка", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // ============ СОХРАНЕНИЕ ============

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                SaveButton.IsEnabled = false;
                SaveButton.Content = "Сохранение...";

                if (Student.Id > 0)
                {
                    // Обновление существующего ученика
                    await SupabaseClient.UpdateStudent(
                        Student.Id,
                        Student.FullName,
                        Student.BirthDate?.ToString("yyyy-MM-dd"),
                        Student.ClassId,
                        Student.Contact
                    );
                }
                else
                {
                    // Добавление нового ученика с созданием пользователя
                    var (studentResult, userResult) = await SupabaseClient.AddStudentWithUser(
                        Student.FullName,
                        Student.BirthDate?.ToString("yyyy-MM-dd"),
                        Student.ClassId,
                        Student.Contact
                    );

                    if (studentResult != null && studentResult.Count > 0)
                    {
                        Student.Id = studentResult[0]["id"].Value<int>();

                        string login = userResult[0]["login"].Value<string>();
                        MessageBox.Show(
                            $"Ученик успешно добавлен!\n\n" +
                            $"Данные для входа:\n" +
                            $"Логин: {login}\n" +
                            $"Пароль: password123\n\n" +
                            $"Сообщите эти данные ученику для входа в систему.",
                            "Успех",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }

                // Сохраняем связи с родителями
                if (Student.Id > 0)
                {
                    // Добавляем новых родителей
                    foreach (var parent in Parents)
                    {
                        // Проверяем, есть ли уже связь
                        var existingRelations = await SupabaseClient.GetStudentParents(Student.Id);
                        bool exists = false;

                        foreach (var rel in existingRelations)
                        {
                            var parentData = rel["parents"];
                            if (parentData["id"].Value<int>() == parent.Id)
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            await SupabaseClient.AddStudentParent(Student.Id, parent.Id);
                        }
                    }

                    // Удаляем удаленных родителей
                    foreach (var removedParent in _removedParents)
                    {
                        await SupabaseClient.DeleteStudentParent(Student.Id, removedParent.Id);
                    }
                    _removedParents.Clear();
                }

                SaveCompleted?.Invoke(true);

                if (Student.Id == 0)
                {
                    GoBack?.Invoke();
                }
                else
                {
                    MessageBox.Show("Ученик успешно обновлен", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SaveCompleted?.Invoke(false);
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveButton.Content = Student.Id > 0 ? "Обновить" : "Создать";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            GoBack?.Invoke();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            GoBack?.Invoke();
        }
    }
}