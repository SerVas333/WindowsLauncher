using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WindowsLauncher.Core.Models.Email;
using WindowsLauncher.UI.ViewModels;

namespace WindowsLauncher.UI.Views
{
    /// <summary>
    /// Interaction logic for AddressBookWindow.xaml
    /// </summary>
    public partial class AddressBookWindow : Window
    {
        public AddressBookWindow()
        {
            InitializeComponent();
        }
        
        public AddressBookWindow(AddressBookViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
        
        /// <summary>
        /// Обработка изменения выбора контактов
        /// </summary>
        private void ContactsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is AddressBookViewModel viewModel && sender is ListView listView)
            {
                // Обновляем список выбранных контактов
                var selectedContacts = listView.SelectedItems?.Cast<Contact>().ToList() ?? new List<Contact>();
                viewModel.SelectedContacts = selectedContacts;
                
                // Принудительно обновляем команду
                viewModel.ConfirmSelectionCommand.RaiseCanExecuteChanged();
            }
        }
    }
}