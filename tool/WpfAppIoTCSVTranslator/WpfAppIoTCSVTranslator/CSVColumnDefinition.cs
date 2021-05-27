using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace WpfAppIoTCSVTranslator
{
    public class CSVColumnDefinition : INotifyPropertyChanged
    {
        private string name;
        private string schema;
        private string displayName;
        private string description;
        private int order;
        private bool isDeviceId;
        private bool isTimestamp;

        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        public string Schema
        {
            get { return schema; }
            set
            {
                schema = value;
                OnPropertyChanged(nameof(Schema));
            }
        }
        public string DisplayName
        {
            get { return displayName; }
            set
            {
                displayName = value;
                OnPropertyChanged(nameof(DisplayName)); 
            }
        }
        public string Description { get { return description; }
            set {
                description = value;
                OnPropertyChanged(nameof(Description));
            }
        }
        public int Order
        {
            get { return order; }
            set
            {
                order = value;
                OnPropertyChanged(nameof(Order));
            }
        }
        public bool IsDeviceId
        {
            get { return isDeviceId; }
            set
            {
                isDeviceId = value;
                OnPropertyChanged(nameof(IsDeviceId));
            }
        }
        public bool IsTimestamp
        {
            get { return isTimestamp; }
            set
            {
                isTimestamp = value;
                OnPropertyChanged(nameof(IsTimestamp));
            }
        }
        public string SchemaName { get; set; }

        public PropertyValueFormatter Formatter { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public interface PropertyValueFormatter
    {
        string Format(string value);
    }
}
