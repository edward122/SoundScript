using System.Windows;
using SoundScript.ViewModels;

namespace SoundScript.Views
{
    public partial class HistoryWindow : Window
    {
        public HistoryWindow()
        {
            InitializeComponent();
            DataContext = new HistoryViewModel();
        }
    }
} 