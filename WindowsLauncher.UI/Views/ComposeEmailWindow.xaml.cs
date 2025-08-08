using System.Windows;
using WindowsLauncher.UI.ViewModels;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Interaction logic for ComposeEmailWindow.xaml
    /// </summary>
    public partial class ComposeEmailWindow : Window
    {
        public ComposeEmailWindow()
        {
            InitializeComponent();
        }
        
        public ComposeEmailWindow(ComposeEmailViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}