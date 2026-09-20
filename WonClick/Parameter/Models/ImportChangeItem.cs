namespace WonClick.Parameter.Models
{
    public class ImportChangeItem
    {
        // =========================================================
        // EXCEL POSITION
        // =========================================================

        public int ExcelRow { get; set; }


        public int ExcelColumn { get; set; }


        // =========================================================
        // ELEMENT
        // =========================================================

        public long ElementIdValue { get; set; }


        public string ElementUniqueId { get; set; } =
            string.Empty;


        public long TypeIdValue { get; set; }


        // =========================================================
        // RVT / ELEMENT DISPLAY INFO
        // =========================================================

        public string RvtFileName { get; set; } =
            string.Empty;


        public string CategoryName { get; set; } =
            string.Empty;


        public string FamilyName { get; set; } =
            string.Empty;


        public string TypeName { get; set; } =
            string.Empty;


        // =========================================================
        // PARAMETER
        // =========================================================

        public string ParameterName { get; set; } =
            string.Empty;


        public string UiGroup { get; set; } =
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
        // PARAMETER IDENTIFIER
        // =========================================================

        public string Identifier { get; set; } =
            string.Empty;


        public string SharedParameterGuid { get; set; } =
            string.Empty;


        public long ParameterDefinitionId { get; set; }


        // =========================================================
        // VALUE
        // =========================================================

        public string RevitValue { get; set; } =
            string.Empty;


        public string ExcelValue { get; set; } =
            string.Empty;


        // =========================================================
        // WRITABLE
        // =========================================================

        public bool IsReadOnly { get; set; }


        // =========================================================
        // STATUS
        //
        // Analyze 단계 기본값은 Change입니다.
        // Apply 단계에서 Applied / Skipped / Conflict /
        // Failed / Read Only 등의 상태가 별도로 생성됩니다.
        // =========================================================

        public string Status { get; set; } =
            "Change";


        public string Message { get; set; } =
            string.Empty;
    }
}