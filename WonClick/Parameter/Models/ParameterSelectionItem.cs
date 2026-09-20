using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WonClick.Parameter.Models
{
    public class ParameterSelectionItem :
        INotifyPropertyChanged
    {
        private bool _isSelected;


        // =========================================================
        // PARAMETER INFO
        // =========================================================

        public string Name { get; set; } =
            string.Empty;


        public string GroupName { get; set; } =
            string.Empty;


        public string RawGroupTypeId { get; set; } =
            string.Empty;


        public string DataTypeName { get; set; } =
            string.Empty;


        public string UiGroup { get; set; } =
            "Other";


        // =========================================================
        // SCOPE
        // =========================================================

        public bool IsTypeParameter { get; set; }


        public string ScopeName =>
            IsTypeParameter
                ? "Type"
                : "Instance";


        // =========================================================
        // IDENTIFIER
        // =========================================================

        public string Identifier { get; set; } =
            string.Empty;


        public string SharedParameterGuid { get; set; } =
            string.Empty;


        public long ParameterDefinitionId { get; set; }


        // =========================================================
        // SELECTION
        // =========================================================

        public bool IsSelected
        {
            get =>
                _isSelected;

            set
            {
                if (_isSelected == value)
                {
                    return;
                }


                _isSelected =
                    value;


                OnPropertyChanged();
            }
        }


        // =========================================================
        // PROPERTY CHANGED
        // =========================================================

        public event PropertyChangedEventHandler?
            PropertyChanged;


        protected virtual void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(
                    propertyName));
        }
    }
}