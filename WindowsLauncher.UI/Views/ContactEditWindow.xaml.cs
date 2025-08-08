using System.Windows;
using WindowsLauncher.UI.ViewModels;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Interaction logic for ContactEditWindow.xaml
    /// </summary>
    public partial class ContactEditWindow : Window
    {
        public ContactEditWindow()
        {
            InitializeComponent();
        }
        
        public ContactEditWindow(ContactEditViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}