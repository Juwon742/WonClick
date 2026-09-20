namespace WonClick.Parameter.Models
{
    public class ParameterItem
    {
        // =========================================================
        // ELEMENT
        // =========================================================

        public long ElementIdValue { get; set; }


        public string ElementUniqueId { get; set; } =
            string.Empty;


        public string ElementName { get; set; } =
            string.Empty;


        public string CategoryName { get; set; } =
            string.Empty;


        // =========================================================
        // TYPE
        // =========================================================

        public long TypeIdValue { get; set; }


        public string FamilyName { get; set; } =
            string.Empty;


        public string TypeName { get; set; } =
            string.Empty;


        // =========================================================
        // PARAMETER
        // =========================================================

        public string Name { get; set; } =
            string.Empty;


        public string GroupName { get; set; } =
            string.Empty;


        public string RawGroupTypeId { get; set; } =
            string.Empty;


        public string DataTypeName { get; set; } =
            string.Empty;


        // =========================================================
        // SCOPE
        // =========================================================

        public bool IsTypeParameter { get; set; }


        public string ScopeName =>
            IsTypeParameter
                ? "Type"
                : "Instance";


        // =========================================================
        // WRITABLE
        // =========================================================

        public bool IsReadOnly { get; set; }


        public bool IsWritable =>
            !IsReadOnly;


        public string WritableMark =>
            IsWritable
                ? "O"
                : "X";


        // =========================================================
        // IDENTIFIER
        // =========================================================

        public string Identifier { get; set; } =
            string.Empty;


        public string SharedParameterGuid { get; set; } =
            string.Empty;


        public long ParameterDefinitionId { get; set; }


        // =========================================================
        // VALUE
        // =========================================================

        public string Value { get; set; } =
            string.Empty;
    }
}