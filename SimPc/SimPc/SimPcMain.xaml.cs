using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Text.Json;
using System.IO;

namespace SimPc
{
    
    public partial class SimPcMain : Window
    {
       

        private List<Product> allProducts;
        private ObservableCollection<Product> filteredProducts;
        private ObservableCollection<BuildItem> buildItems;
        private HashSet<int> selectedManufacturers;
        private string currentCategory = "Все компоненты";



        public SimPcMain()
        {

            InitializeComponent();
            InitializeData();
            InitializeManufacturersFilter();
            SetupEventHandlers();
            UpdateProductsDisplay();
        }

        private void InitializeData()
        {
           
            allProducts = GenerateTestData();
            filteredProducts = new ObservableCollection<Product>();
            buildItems = new ObservableCollection<BuildItem>();
            selectedManufacturers = new HashSet<int>();

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
                    Margin = new Thickness(0, 3,0,0),
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

        private void ApplyFilters()
        {
            var query = allProducts.AsQueryable();

            if (currentCategory != "Все компоненты")
            {
                query = query.Where(p => p.Category == currentCategory);
            }

            decimal maxPrice = (decimal)PriceSlider.Value;
            query = query.Where(p => p.Price <= maxPrice);

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
                var dialog = new Microsoft.Win32.OpenFileDialog
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
            }
        }

        private List<Product> GenerateTestData()
        {
            var products = new List<Product>();
            int id = 1;
            var random = new Random();

            var processors = new[]
            {
                new { Name = "Intel Core i9-13900K", Price = 65000, Man = "Intel", Desc = "24 ядра, до 5.8 ГГц", Icon = "⚡" },
                new { Name = "Intel Core i7-13700K", Price = 45000, Man = "Intel", Desc = "16 ядер, до 5.4 ГГц", Icon = "⚡" },
                new { Name = "Intel Core i5-13600K", Price = 32000, Man = "Intel", Desc = "14 ядер, до 5.1 ГГц", Icon = "⚡" },
                new { Name = "AMD Ryzen 9 7950X", Price = 60000, Man = "AMD", Desc = "16 ядер, до 5.7 ГГц", Icon = "⚡" },
                new { Name = "AMD Ryzen 7 7800X3D", Price = 48000, Man = "AMD", Desc = "8 ядер, 3D V-Cache", Icon = "⚡" },
                new { Name = "AMD Ryzen 5 7600X", Price = 25000, Man = "AMD", Desc = "6 ядер, до 5.3 ГГц", Icon = "⚡" }
            };

            foreach (var p in processors)
            {
                products.Add(new Product
                {
                    Id = id++,
                    Name = p.Name,
                    Price = p.Price,
                    Manufacturer = p.Man,
                    Category = "Процессоры",
                    Description = p.Desc,
                    Icon = p.Icon
                });
            }

            var gpus = new[]
            {
                new { Name = "NVIDIA RTX 4090", Price = 180000, Man = "NVIDIA", Desc = "24GB GDDR6X", Icon = "🎮" },
                new { Name = "NVIDIA RTX 4080", Price = 130000, Man = "NVIDIA", Desc = "16GB GDDR6X", Icon = "🎮" },
                new { Name = "NVIDIA RTX 4070 Ti", Price = 85000, Man = "NVIDIA", Desc = "12GB GDDR6X", Icon = "🎮" },
                new { Name = "AMD RX 7900 XTX", Price = 120000, Man = "AMD", Desc = "24GB GDDR6", Icon = "🎮" },
                new { Name = "AMD RX 7900 XT", Price = 95000, Man = "AMD", Desc = "20GB GDDR6", Icon = "🎮" },
                new { Name = "AMD RX 7800 XT", Price = 65000, Man = "AMD", Desc = "16GB GDDR6", Icon = "🎮" }
            };

            foreach (var g in gpus)
            {
                products.Add(new Product
                {
                    Id = id++,
                    Name = g.Name,
                    Price = g.Price,
                    Manufacturer = g.Man,
                    Category = "Видеокарты",
                    Description = g.Desc,
                    Icon = g.Icon
                });
            }

            var mobos = new[]
            {
                new { Name = "ASUS ROG Maximus Z790", Price = 55000, Man = "ASUS", Desc = "Intel Z790, DDR5", Icon = "🔌" },
                new { Name = "MSI MPG B650 Carbon", Price = 32000, Man = "MSI", Desc = "AMD B650, DDR5", Icon = "🔌" },
                new { Name = "Gigabyte Z790 AORUS", Price = 38000, Man = "Gigabyte", Desc = "Intel Z790, DDR5", Icon = "🔌" },
                new { Name = "ASRock B650E Taichi", Price = 45000, Man = "ASRock", Desc = "AMD B650E, DDR5", Icon = "🔌" }
            };

            foreach (var m in mobos)
            {
                products.Add(new Product
                {
                    Id = id++,
                    Name = m.Name,
                    Price = m.Price,
                    Manufacturer = m.Man,
                    Category = "Материнские платы",
                    Description = m.Desc,
                    Icon = m.Icon
                });
            }

            var rams = new[]
            {
                new { Name = "Corsair Vengeance 32GB", Price = 15000, Man = "Corsair", Desc = "DDR5-6000MHz", Icon = "💾" },
                new { Name = "Kingston Fury 32GB", Price = 14000, Man = "Kingston", Desc = "DDR5-5600MHz", Icon = "💾" },
                new { Name = "G.Skill Trident 32GB", Price = 16000, Man = "G.Skill", Desc = "DDR5-6400MHz", Icon = "💾" },
                new { Name = "Crucial 16GB", Price = 7000, Man = "Crucial", Desc = "DDR4-3200MHz", Icon = "💾" }
            };

            foreach (var r in rams)
            {
                products.Add(new Product
                {
                    Id = id++,
                    Name = r.Name,
                    Price = r.Price,
                    Manufacturer = r.Man,
                    Category = "Оперативная память",
                    Description = r.Desc,
                    Icon = r.Icon
                });
            }
            var storages = new[]
            {
                new { Name = "Samsung 980 Pro 1TB", Price = 12000, Man = "Samsung", Desc = "NVMe M.2, 7000MB/s", Icon = "💿" },
                new { Name = "WD Black SN850 1TB", Price = 11000, Man = "WD", Desc = "NVMe M.2, 7000MB/s", Icon = "💿" },
                new { Name = "Kingston KC3000 1TB", Price = 10000, Man = "Kingston", Desc = "NVMe M.2, 7000MB/s", Icon = "💿" },
                new { Name = "Crucial P5 Plus 1TB", Price = 9500, Man = "Crucial", Desc = "NVMe M.2, 6600MB/s", Icon = "💿" }
            };

            foreach (var s in storages)
            {
                products.Add(new Product
                {
                    Id = id++,
                    Name = s.Name,
                    Price = s.Price,
                    Manufacturer = s.Man,
                    Category = "Накопители",
                    Description = s.Desc,
                    Icon = s.Icon
                });
            }

            var psus = new[]
            {
                new { Name = "Corsair RM1000x", Price = 22000, Man = "Corsair", Desc = "1000W, 80+ Gold", Icon = "⚡" },
                new { Name = "Seasonic Focus 850W", Price = 18000, Man = "Seasonic", Desc = "850W, 80+ Gold", Icon = "⚡" },
                new { Name = "be quiet! 1200W", Price = 28000, Man = "be quiet!", Desc = "1200W, 80+ Platinum", Icon = "⚡" }
            };

            foreach (var p in psus)
            {
                products.Add(new Product
                {
                    Id = id++,
                    Name = p.Name,
                    Price = p.Price,
                    Manufacturer = p.Man,
                    Category = "Блоки питания",
                    Description = p.Desc,
                    Icon = p.Icon
                });
            }

            var cases = new[]
            {
                new { Name = "Fractal Design Meshify 2", Price = 15000, Man = "Fractal", Desc = "Mid-Tower, Tempered Glass", Icon = "📦" },
                new { Name = "Lian Li O11 Dynamic", Price = 18000, Man = "Lian Li", Desc = "Mid-Tower, Dual Chamber", Icon = "📦" },
                new { Name = "NZXT H7 Flow", Price = 13000, Man = "NZXT", Desc = "Mid-Tower, High Airflow", Icon = "📦" }
            };

            foreach (var c in cases)
            {
                products.Add(new Product
                {
                    Id = id++,
                    Name = c.Name,
                    Price = c.Price,
                    Manufacturer = c.Man,
                    Category = "Корпуса",
                    Description = c.Desc,
                    Icon = c.Icon
                });
            }

            return products;
        }
    }
    public class Product : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Manufacturer { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }

        private bool isInBuild;
        public bool IsInBuild
        {
            get => isInBuild;
            set
            {
                isInBuild = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class BuildItem
    {
        public Product Product { get; set; }
        public string Category { get; set; }
        public string CategoryIcon
        {
            get
            {
                switch (Category)
                {
                    case "Процессоры": return "⚡";
                    case "Видеокарты": return "🎮";
                    case "Материнские платы": return "🔌";
                    case "Оперативная память": return "💾";
                    case "Накопители": return "💿";
                    case "Блоки питания": return "⚡";
                    case "Корпуса": return "📦";
                    default: return "🖥️";
                }
            }
        }
    }

    public class BuildSaveData
    {
        public List<int> Items { get; set; }
        public DateTime SaveDate { get; set; }
    }

    public class InBuildToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? "✓ В сборке" : "+ Добавить";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InBuildToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x5C)) : new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}