namespace WonClick.Parameter.Models
{
    public class ImportApplyResult
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
        // ELEMENT DISPLAY INFO
        // =========================================================

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
        // RESULT
        //
        // Applied
        // Skipped
        // Conflict
        // Failed
        // Read Only
        // =========================================================

        public string Status { get; set; } =
            string.Empty;


        public string Message { get; set; } =
            string.Empty;


        // =========================================================
        // RESULT HELPERS
        // =========================================================

        public bool IsApplied =>
            Status == "Applied";


        public bool IsSkipped =>
            Status == "Skipped";


        public bool IsConflict =>
            Status == "Conflict";


        public bool IsFailed =>
            Status == "Failed";


        public bool IsReadOnly =>
            Status == "Read Only";
    }
}