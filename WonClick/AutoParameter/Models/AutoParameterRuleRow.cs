namespace WonClick.AutoParameter.Models
{
    public class AutoParameterRuleRow
    {
        // =========================================================
        // 해당 Rule 사용 여부
        // =========================================================

        public bool Enabled
        {
            get;
            set;
        } = true;


        // =========================================================
        // 선택된 Instance Parameter
        // =========================================================

        public InstanceParameterItem? SelectedParameter
        {
            get;
            set;
        }


        // =========================================================
        // 자동 입력 Value
        // =========================================================

        public string Value
        {
            get;
            set;
        } = string.Empty;
    }
}