using System.Collections.Generic;

namespace WonClick.AutoParameter.Models
{
    public class AutoParameterPreset
    {
        // =========================================================
        // Preset 파일 버전
        //
        // 향후 형식 변경 시 호환성 확인용
        // =========================================================

        public int Version
        {
            get;
            set;
        } = 1;


        // =========================================================
        // Preset 이름
        // =========================================================

        public string Name
        {
            get;
            set;
        } = "Auto Parameter Preset";


        // =========================================================
        // Rule 목록
        // =========================================================

        public List<AutoParameterPresetRule> Rules
        {
            get;
            set;
        } =
            new List<AutoParameterPresetRule>();
    }


    public class AutoParameterPresetRule
    {
        // =========================================================
        // 사용 여부
        // =========================================================

        public bool Enabled
        {
            get;
            set;
        } = true;


        // =========================================================
        // Parameter 정보
        // =========================================================

        public long ParameterId
        {
            get;
            set;
        }


        public string ParameterName
        {
            get;
            set;
        } =
            string.Empty;


        // Shared Parameter일 경우 GUID
        //
        // JSON 저장을 쉽게 하기 위해 string으로 저장
        public string? SharedParameterGuid
        {
            get;
            set;
        }


        // =========================================================
        // 자동 기입 Value
        // =========================================================

        public string Value
        {
            get;
            set;
        } =
            string.Empty;
    }
}