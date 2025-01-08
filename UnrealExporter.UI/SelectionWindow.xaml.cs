using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace UnrealExporter.UI
{
    /// <summary>
    /// Interaction logic for SelectionWindow.xaml
    /// </summary>
    public partial class SelectionWindow : Window
    {
        public string? SelectedProject { get; set; }

        public SelectionWindow()
        {
            InitializeComponent();
        }

        public SelectionWindow(string[] selection)
        {
            InitializeComponent();

            lstSelection.ItemsSource = selection;
        }

        private void lstSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedProject = lstSelection.SelectedItem as string;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
