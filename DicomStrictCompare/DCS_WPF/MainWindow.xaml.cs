using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.IO;
using System.Windows.Markup;

namespace DCS_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Private variables

        string _sourceLabelString;
        string _targetLabelString;
        string _sourceDirectoryPathString;
        string _targetDirectoryPathString;
        string _saveLabelString;
        string _saveDirectoryPathString;
        int _progressValue;
        #endregion

        #region Properties
        /// <summary>
        /// Human readable description of the source or reference dataset 
        /// To be used in both the application and the produced graphic outputs
        /// </summary>
        public string SourceLabelString
        {
            get { return _sourceLabelString; }
            set
            {
                _sourceLabelString = value;
                OnPropertyChanged(nameof(SourceLabelString));
            }
        }

        /// <summary>
        /// Human readable description of the target or test dataset 
        /// To be used in both the application and the produced graphic outputs
        /// </summary>
        public string TargetLabelString
        {
            get { return _targetLabelString; }
            set
            {

                _targetLabelString = value ?? _targetLabelString;
                OnPropertyChanged(nameof(TargetLabelString));
            }
        }

        public string SaveLabelString
        {
            get { return _saveLabelString; }
            set
            {
                _saveLabelString = value;
                OnPropertyChanged(nameof(SaveLabelString));
            }
        }

        // weird way to make sure it's never null 
        public string SourceDirectoryPathString
        {
            get => _sourceDirectoryPathString;
            set
            {
                if (value != null)
                    _sourceDirectoryPathString = value;
                OnPropertyChanged(nameof(SourceDirectoryPathString));

            }
        }
        public string TargetDirectoryPathString
        {
            get => _targetDirectoryPathString;
            set
            {
                if (value != null)
                    _targetDirectoryPathString = value;
                OnPropertyChanged(nameof(TargetDirectoryPathString));

            }
        }
        public string SaveDirectoryPathString
        {
            get => _saveDirectoryPathString;
            set
            {
                if (value != null)
                    _saveDirectoryPathString = value;
                OnPropertyChanged(nameof(SaveDirectoryPathString));

            }
        }

        public int SourceDoseFileCount { get; private set; } = -1;

        public int TargetDoseFileCount { get; private set; } = -1;

        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                if (value < 0)
                    _progressValue = 0;
                if (value > 100)
                    _progressValue = 100;
                else
                    _progressValue = value;
            }
        }


        #endregion

        public MainWindow()
        {

            InitializeComponent();
            _sourceLabelString = Properties.Resources.DefaultSourceLabel;
            _targetLabelString = Properties.Resources.DefaultTargetLabel;
            _saveLabelString = Properties.Resources.DefaultSaveLabel;
            _sourceDirectoryPathString = Properties.Resources.DefaultSourceDirectory;
            _targetDirectoryPathString = Properties.Resources.DefaultTargetDirectory;
            _saveDirectoryPathString= Properties.Resources.DefaultSaveDirectory;
            
            MainGrid.DataContext = this; // This is what binds this window's properties to the xaml view!
        }

        #region field binding instructions

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        #endregion


        #region Buttons
        private void SourceDirectoryPath_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.ShowNewFolderButton = true;
            dialog.InitialDirectory = System.IO.Path.GetFullPath(Directory.GetParent(path: SourceDirectoryPathString).FullName.ToString());
            DialogResult dialogResult = dialog.ShowDialog();
            if (dialogResult == System.Windows.Forms.DialogResult.OK)
            {
                SourceDirectoryPathLabel.Content = dialog.SelectedPath;
                SourceDirectoryPathString = System.IO.Path.GetFullPath(dialog.SelectedPath);

            }

            OnPropertyChanged(nameof(TargetDirectoryPathString));
        }

        private void TargetDirectoryPath_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.ShowNewFolderButton = true;
            dialog.InitialDirectory = System.IO.Path.GetFullPath(Directory.GetParent(path: TargetDirectoryPathString).FullName.ToString());
            DialogResult dialogResult = dialog.ShowDialog();
            if (dialogResult == System.Windows.Forms.DialogResult.OK)
            {
                TargetDirectoryPathLabel.Content = dialog.SelectedPath;
                TargetDirectoryPathString = System.IO.Path.GetFullPath(dialog.SelectedPath);

            }

            OnPropertyChanged(nameof(TargetDirectoryPathString));
        }

        #endregion

        private void addDTASet_Click(object sender, RoutedEventArgs e)
        {
            Views.AddDta addDta = new Views.AddDta();
            addDta.Show();
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
