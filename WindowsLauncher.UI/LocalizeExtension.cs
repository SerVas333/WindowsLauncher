using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using WindowsLauncher.UI.Properties.Resources;

namespace WindowsLauncher.UI
{
    [MarkupExtensionReturnType(typeof(BindingExpression))]
    public class LocalizeExtension : MarkupExtension
    {
        public string Key { get; set; }

        public LocalizeExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding("Value")
            {
                Source = new LocalizedString(Key)
            };

            return binding.ProvideValue(serviceProvider);
        }
    }

    public class LocalizedString : INotifyPropertyChanged
    {
        private string _key;

        public string Key
        {
            get => _key;
            set
            {
                _key = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        public string Value
        {
            get
            {
                var value = Resources.ResourceManager.GetString(Key, Resources.Culture);
                return value ?? Key;  // 🔄 Упрощенная версия без args
            }
        }

        public LocalizedString(string key)
        {
            _key = key;
            LocalizationManager.LanguageChanged += (s, e) => OnPropertyChanged(nameof(Value));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}