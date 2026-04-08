using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.Json;
using System.IO;
using Microsoft.Win32;

namespace SimPc
{
    public partial class SimPcMain : Window
    {
        private List<Product> allProducts;
        private ObservableCollection<Product> filteredProducts;
        private ObservableCollection<BuildItem> buildItems;
        private string currentCategory = "Все компоненты";
        private string searchText = "";
        private string currentTheme = "Dark";

        public SimPcMain()
        {
            InitializeComponent();
            InitializeData();
            InitializeManufacturersFilter();
            SetupEventHandlers();
            LoadSettings();
            ApplyTheme(currentTheme);
            UpdateProductsDisplay();
        }

        private void InitializeData()
        {
            allProducts = GenerateTestData();
            filteredProducts = new ObservableCollection<Product>();
            buildItems = new ObservableCollection<BuildItem>();

            BuildItemsControl.ItemsSource = buildItems;
            UpdateTotalPrice();
        }

        private void InitializeManufacturersFilter()
        {
            var manufacturers = allProducts.Select(p => p.Manufacturer).Distinct().ToList();
            foreach (var manufacturer in manufacturers)
            {
                var checkBox = new CheckBox
                {
                    Content = manufacturer,
                    Margin = new Thickness(0, 3, 0, 3),
                    Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                    Tag = manufacturer
                };
                checkBox.Checked += ManufacturerFilter_Changed;
                checkBox.Unchecked += ManufacturerFilter_Changed;
                ManufacturersPanel.Children.Add(checkBox);
            }
        }

        private void SetupEventHandlers()
        {
            CategoryListBox.SelectionChanged += CategoryListBox_SelectionChanged;
            ApplyFiltersButton.Click += ApplyFiltersButton_Click;
            PriceSlider.ValueChanged += (s, e) => ApplyFilters();
        }

        private void ManufacturerFilter_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryListBox.SelectedItem is ListBoxItem selectedItem)
            {
                currentCategory = selectedItem.Content.ToString();
                ApplyFilters();
            }
        }

        private void ApplyFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            searchText = SearchBox.Text?.ToLower() ?? "";
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            IEnumerable<Product> query = allProducts;

            if (currentCategory != "Все компоненты")
            {
                query = query.Where(p => p.Category == currentCategory);
            }

            decimal maxPrice = (decimal)PriceSlider.Value;
            query = query.Where(p => p.Price <= maxPrice);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(p =>
                    p.Name.ToLower().Contains(searchText) ||
                    p.Manufacturer.ToLower().Contains(searchText) ||
                    p.Description.ToLower().Contains(searchText));
            }

            var selectedMans = ManufacturersPanel.Children
                .OfType<CheckBox>()
                .Where(cb => cb.IsChecked == true)
                .Select(cb => cb.Tag.ToString())
                .ToList();

            if (selectedMans.Any())
            {
                query = query.Where(p => selectedMans.Contains(p.Manufacturer));
            }

            filteredProducts.Clear();
            foreach (var product in query.ToList())
            {
                filteredProducts.Add(product);
            }

            ProductsItemsControl.ItemsSource = filteredProducts;
        }

        private void UpdateProductsDisplay()
        {
            ProductsItemsControl.ItemsSource = filteredProducts;
        }

        private void AddToBuildButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var product = button?.Tag as Product;

            if (product != null)
            {
                if (product.IsInBuild)
                {
                    var buildItem = buildItems.FirstOrDefault(bi => bi.Product.Id == product.Id);
                    if (buildItem != null)
                    {
                        buildItems.Remove(buildItem);
                        product.IsInBuild = false;
                    }
                }
                else
                {
                    if (buildItems.Any(bi => bi.Product.Category == product.Category))
                    {
                        MessageBox.Show($"В сборке уже есть компонент категории '{product.Category}'. " +
                                      "В одной сборке не может быть двух компонентов одной категории.",
                                      "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    buildItems.Add(new BuildItem { Product = product, Category = product.Category });
                    product.IsInBuild = true;
                    ShowCompatibilityWarnings();
                }

                UpdateTotalPrice();
                RefreshProductsDisplay();
            }
        }

        private void RemoveFromBuild_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var buildItem = button?.Tag as BuildItem;

            if (buildItem != null)
            {
                buildItem.Product.IsInBuild = false;
                buildItems.Remove(buildItem);
                UpdateTotalPrice();
                RefreshProductsDisplay();
                ShowCompatibilityWarnings();
            }
        }

        private void RefreshProductsDisplay()
        {
            var currentSource = ProductsItemsControl.ItemsSource;
            ProductsItemsControl.ItemsSource = null;
            ProductsItemsControl.ItemsSource = currentSource;
        }

        private void UpdateTotalPrice()
        {
            decimal total = buildItems.Sum(item => item.Product.Price);
            TotalPriceTextBlock.Text = $"{total:N0} ₽";
        }

        private void ShowCompatibilityWarnings()
        {
            var warnings = new List<string>();

            var currentCPU = buildItems.FirstOrDefault(b => b.Category == "Процессоры")?.Product;
            var currentMotherboard = buildItems.FirstOrDefault(b => b.Category == "Материнские платы")?.Product;
            var currentRAM = buildItems.FirstOrDefault(b => b.Category == "Оперативная память")?.Product;
            var currentPSU = buildItems.FirstOrDefault(b => b.Category == "Блоки питания")?.Product;
            var currentGPU = buildItems.FirstOrDefault(b => b.Category == "Видеокарты")?.Product;

            if (currentCPU != null && currentMotherboard != null)
            {
                if (!string.IsNullOrEmpty(currentCPU.Socket) && !string.IsNullOrEmpty(currentMotherboard.Socket))
                {
                    if (currentCPU.Socket != currentMotherboard.Socket)
                    {
                        warnings.Add($"❌ Сокет процессора ({currentCPU.Socket}) не совместим с сокетом материнской платы ({currentMotherboard.Socket})");
                    }
                }
            }

            if (currentMotherboard != null && currentRAM != null)
            {
                if (!string.IsNullOrEmpty(currentMotherboard.RamType) && !string.IsNullOrEmpty(currentRAM.RamType))
                {
                    if (currentMotherboard.RamType != currentRAM.RamType)
                    {
                        warnings.Add($"❌ Тип памяти материнской платы ({currentMotherboard.RamType}) не совместим с типом ОЗУ ({currentRAM.RamType})");
                    }
                }
            }

            int totalPower = (currentCPU?.PowerConsumption ?? 0) + (currentGPU?.PowerConsumption ?? 0) + 50;

            if (currentPSU != null && totalPower > 0)
            {
                if (currentPSU.PowerConsumption < totalPower)
                {
                    warnings.Add($"⚠️ Блок питания ({currentPSU.PowerConsumption}W) может быть недостаточным. Требуется ~{totalPower}W");
                }
            }

            if (buildItems.Any() && currentCPU == null)
            {
                warnings.Add("⚠️ Процессор не выбран!");
            }

            if (buildItems.Any() && currentMotherboard == null)
            {
                warnings.Add("⚠️ Материнская плата не выбрана!");
            }

            string warningsText = string.Join("\n", warnings);

            if (!string.IsNullOrEmpty(warningsText))
            {
                CompatibilityWarningTextBlock.Text = warningsText;
                CompatibilityWarningBorder.Visibility = Visibility.Visible;
            }
            else
            {
                CompatibilityWarningBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyTheme(string theme)
        {
            if (theme == "Light")
            {
                Application.Current.Resources["SidebarBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
                Application.Current.Resources["BuildPanelBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
                Application.Current.Resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
                this.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
            }
            else
            {
                Application.Current.Resources["SidebarBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
                Application.Current.Resources["BuildPanelBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));
                Application.Current.Resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
                this.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            }
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            currentTheme = currentTheme == "Dark" ? "Light" : "Dark";
            ApplyTheme(currentTheme);
            SaveSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new Dictionary<string, object>
                {
                    { "Theme", currentTheme }
                };
                string json = JsonSerializer.Serialize(settings);
                File.WriteAllText("settings.json", json);
            }
            catch { }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists("settings.json"))
                {
                    string json = File.ReadAllText("settings.json");
                    var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (settings != null && settings.ContainsKey("Theme"))
                    {
                        currentTheme = settings["Theme"].ToString();
                    }
                }
            }
            catch { }
        }

        private void SaveBuildButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveData = new BuildSaveData
                {
                    Items = buildItems.Select(bi => bi.Product.Id).ToList(),
                    SaveDate = DateTime.Now
                };

                string json = JsonSerializer.Serialize(saveData, new JsonSerializerOptions { WriteIndented = true });
                string fileName = $"Build_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                File.WriteAllText(fileName, json);

                MessageBox.Show($"Сборка сохранена в файл: {fileName}", "Успешно",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadBuildButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    Title = "Выберите файл со сборкой"
                };

                if (dialog.ShowDialog() == true)
                {
                    string json = File.ReadAllText(dialog.FileName);
                    var saveData = JsonSerializer.Deserialize<BuildSaveData>(json);

                    if (saveData != null)
                    {
                        foreach (var item in buildItems.ToList())
                        {
                            item.Product.IsInBuild = false;
                            buildItems.Remove(item);
                        }

                        foreach (var productId in saveData.Items)
                        {
                            var product = allProducts.FirstOrDefault(p => p.Id == productId);
                            if (product != null && !buildItems.Any(bi => bi.Product.Category == product.Category))
                            {
                                buildItems.Add(new BuildItem { Product = product, Category = product.Category });
                                product.IsInBuild = true;
                            }
                        }

                        UpdateTotalPrice();
                        RefreshProductsDisplay();
                        ShowCompatibilityWarnings();
                        MessageBox.Show("Сборка успешно загружена!", "Успешно",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearBuildButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы уверены, что хотите очистить всю сборку?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (var item in buildItems.ToList())
                {
                    item.Product.IsInBuild = false;
                    buildItems.Remove(item);
                }
                UpdateTotalPrice();
                RefreshProductsDisplay();
                ShowCompatibilityWarnings();
            }
        }

        private List<Product> GenerateTestData()
        {
            var products = new List<Product>();
            int id = 1;

            // Процессоры
            products.Add(new Product { Id = id++, Name = "Intel Core i9-13900K", Price = 65000m, Manufacturer = "Intel", Category = "Процессоры", Description = "24 ядра, до 5.8 ГГц", Icon = "⚡", Socket = "LGA1700", PowerConsumption = 125, RamType = "DDR5" });
            products.Add(new Product { Id = id++, Name = "Intel Core i7-13700K", Price = 45000m, Manufacturer = "Intel", Category = "Процессоры", Description = "16 ядер, до 5.4 ГГц", Icon = "⚡", Socket = "LGA1700", PowerConsumption = 125, RamType = "DDR5" });
            products.Add(new Product { Id = id++, Name = "Intel Core i5-13600K", Price = 32000m, Manufacturer = "Intel", Category = "Процессоры", Description = "14 ядер, до 5.1 ГГц", Icon = "⚡", Socket = "LGA1700", PowerConsumption = 125, RamType = "DDR5" });
            products.Add(new Product { Id = id++, Name = "AMD Ryzen 9 7950X", Price = 60000m, Manufacturer = "AMD", Category = "Процессоры", Description = "16 ядер, до 5.7 ГГц", Icon = "⚡", Socket = "AM5", PowerConsumption = 170, RamType = "DDR5" });
            products.Add(new Product { Id = id++, Name = "AMD Ryzen 7 7800X3D", Price = 48000m, Manufacturer = "AMD", Category = "Процессоры", Description = "8 ядер, 3D V-Cache", Icon = "⚡", Socket = "AM5", PowerConsumption = 120, RamType = "DDR5" });
            products.Add(new Product { Id = id++, Name = "AMD Ryzen 5 7600X", Price = 25000m, Manufacturer = "AMD", Category = "Процессоры", Description = "6 ядер, до 5.3 ГГц", Icon = "⚡", Socket = "AM5", PowerConsumption = 105, RamType = "DDR5" });

            // Видеокарты
            products.Add(new Product { Id = id++, Name = "NVIDIA RTX 4090", Price = 180000m, Manufacturer = "NVIDIA", Category = "Видеокарты", Description = "24GB GDDR6X", Icon = "🎮", PowerConsumption = 450 });
            products.Add(new Product { Id = id++, Name = "NVIDIA RTX 4080", Price = 130000m, Manufacturer = "NVIDIA", Category = "Видеокарты", Description = "16GB GDDR6X", Icon = "🎮", PowerConsumption = 320 });
            products.Add(new Product { Id = id++, Name = "NVIDIA RTX 4070 Ti", Price = 85000m, Manufacturer = "NVIDIA", Category = "Видеокарты", Description = "12GB GDDR6X", Icon = "🎮", PowerConsumption = 285 });
            products.Add(new Product { Id = id++, Name = "AMD RX 7900 XTX", Price = 120000m, Manufacturer = "AMD", Category = "Видеокарты", Description = "24GB GDDR6", Icon = "🎮", PowerConsumption = 355 });
            products.Add(new Product { Id = id++, Name = "AMD RX 7900 XT", Price = 95000m, Manufacturer = "AMD", Category = "Видеокарты", Description = "20GB GDDR6", Icon = "🎮", PowerConsumption = 300 });
            products.Add(new Product { Id = id++, Name = "AMD RX 7800 XT", Price = 65000m, Manufacturer = "AMD", Category = "Видеокарты", Description = "16GB GDDR6", Icon = "🎮", PowerConsumption = 263 });

            // Материнские платы
            products.Add(new Product { Id = id++, Name = "ASUS ROG Maximus Z790", Price = 55000m, Manufacturer = "ASUS", Category = "Материнские платы", Description = "Intel Z790, DDR5", Icon = "🔌", Socket = "LGA1700", RamType = "DDR5" });
            products.Add(new Product { Id = id++, Name = "MSI MPG B650 Carbon", Price = 32000m, Manufacturer = "MSI", Category = "Материнские платы", Description = "AMD B650, DDR5", Icon = "🔌", Socket = "AM5", RamType = "DDR5" });
            products.Add(new Product { Id = id++, Name = "Gigabyte Z790 AORUS", Price = 38000m, Manufacturer = "Gigabyte", Category = "Материнские платы", Description = "Intel Z790, DDR5", Icon = "🔌", Socket = "LGA1700", RamType = "DDR5" });
            products.Add(new Product { Id = id++, Name = "ASRock B650E Taichi", Price = 45000m, Manufacturer = "ASRock", Category = "Материнские платы", Description = "AMD B650E, DDR5", Icon = "🔌", Socket = "AM5", RamType = "DDR5" });

            // Оперативная память
            products.Add(new Product { Id = id++, Name = "Corsair Vengeance 32GB", Price = 15000m, Manufacturer = "Corsair", Category = "Оперативная память", Description = "DDR5-6000MHz", Icon = "💾", RamType = "DDR5", RamFrequency = 6000 });
            products.Add(new Product { Id = id++, Name = "Kingston Fury 32GB", Price = 14000m, Manufacturer = "Kingston", Category = "Оперативная память", Description = "DDR5-5600MHz", Icon = "💾", RamType = "DDR5", RamFrequency = 5600 });
            products.Add(new Product { Id = id++, Name = "G.Skill Trident 32GB", Price = 16000m, Manufacturer = "G.Skill", Category = "Оперативная память", Description = "DDR5-6400MHz", Icon = "💾", RamType = "DDR5", RamFrequency = 6400 });
            products.Add(new Product { Id = id++, Name = "Crucial 16GB", Price = 7000m, Manufacturer = "Crucial", Category = "Оперативная память", Description = "DDR4-3200MHz", Icon = "💾", RamType = "DDR4", RamFrequency = 3200 });

            // Накопители
            products.Add(new Product { Id = id++, Name = "Samsung 980 Pro 1TB", Price = 12000m, Manufacturer = "Samsung", Category = "Накопители", Description = "NVMe M.2, 7000MB/s", Icon = "💿" });
            products.Add(new Product { Id = id++, Name = "WD Black SN850 1TB", Price = 11000m, Manufacturer = "WD", Category = "Накопители", Description = "NVMe M.2, 7000MB/s", Icon = "💿" });
            products.Add(new Product { Id = id++, Name = "Kingston KC3000 1TB", Price = 10000m, Manufacturer = "Kingston", Category = "Накопители", Description = "NVMe M.2, 7000MB/s", Icon = "💿" });
            products.Add(new Product { Id = id++, Name = "Crucial P5 Plus 1TB", Price = 9500m, Manufacturer = "Crucial", Category = "Накопители", Description = "NVMe M.2, 6600MB/s", Icon = "💿" });

            // Блоки питания
            products.Add(new Product { Id = id++, Name = "Corsair RM1000x", Price = 22000m, Manufacturer = "Corsair", Category = "Блоки питания", Description = "1000W, 80+ Gold", Icon = "⚡", PowerConsumption = 1000 });
            products.Add(new Product { Id = id++, Name = "Seasonic Focus 850W", Price = 18000m, Manufacturer = "Seasonic", Category = "Блоки питания", Description = "850W, 80+ Gold", Icon = "⚡", PowerConsumption = 850 });
            products.Add(new Product { Id = id++, Name = "be quiet! 1200W", Price = 28000m, Manufacturer = "be quiet!", Category = "Блоки питания", Description = "1200W, 80+ Platinum", Icon = "⚡", PowerConsumption = 1200 });

            // Корпуса
            products.Add(new Product { Id = id++, Name = "Fractal Design Meshify 2", Price = 15000m, Manufacturer = "Fractal", Category = "Корпуса", Description = "Mid-Tower, Tempered Glass", Icon = "📦" });
            products.Add(new Product { Id = id++, Name = "Lian Li O11 Dynamic", Price = 18000m, Manufacturer = "Lian Li", Category = "Корпуса", Description = "Mid-Tower, Dual Chamber", Icon = "📦" });
            products.Add(new Product { Id = id++, Name = "NZXT H7 Flow", Price = 13000m, Manufacturer = "NZXT", Category = "Корпуса", Description = "Mid-Tower, High Airflow", Icon = "📦" });

            return products;
        }
    }
}