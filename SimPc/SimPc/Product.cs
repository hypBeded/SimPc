using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SimPc
{
    public class Product : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Manufacturer { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string Socket { get; set; }
        public string RamType { get; set; }
        public int RamFrequency { get; set; }
        public int PowerConsumption { get; set; }
        public string FormFactor { get; set; }
        public int Capacity { get; set; }

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

   
}

