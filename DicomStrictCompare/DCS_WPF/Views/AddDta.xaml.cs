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

namespace DCS_WPF.Views
{
    /// <summary>
    /// Interaction logic for AddDta.xaml
    /// </summary>
    public partial class AddDta : Window
    {
        public string? Tolerance;
        public string? DTA;
        public Models.DtaSet.DTAUnit? DTAUnit;
        public string? Threshhold;
        public string? Trim;
        public bool Global = false;
        public bool Gamma = false;
        public bool Override = false;


        public AddDta()
        {
            InitializeComponent();
            
        }

        public void AddDta_Closing(object sender, ConsoleCancelEventArgs e)
        {


        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            
            if (DTA != null && Threshhold != null && Trim != null && Tolerance != null && DTAUnit != null)
            {
                //TODO: Write save code here
            }
            Close();
        }
    }
}
