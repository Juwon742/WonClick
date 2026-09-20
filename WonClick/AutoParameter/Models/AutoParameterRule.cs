using Autodesk.Revit.DB;
using System;

// WonClick.Parameter 네임스페이스와 이름 충돌 방지
using RevitParameter = Autodesk.Revit.DB.Parameter;

namespace WonClick.AutoParameter.Models
{
    public class AutoParameterRule
    {
        public bool Enabled { get; set; } = true;


        public long ParameterId { get; set; }


        public string ParameterName { get; set; } =
            string.Empty;


        public Guid? SharedParameterGuid { get; set; }


        public string Value { get; set; } =
            string.Empty;


        // =========================================================
        // 선택한 Revit Instance Parameter로부터 Rule 생성
        // =========================================================

        public static AutoParameterRule FromParameter(
            RevitParameter parameter,
            string value)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(
                    nameof(parameter));
            }


            Guid? sharedGuid = null;


            if (parameter.IsShared)
            {
                try
                {
                    sharedGuid =
                        parameter.GUID;
                }
                catch
                {
                    sharedGuid = null;
                }
            }


            return new AutoParameterRule
            {
                Enabled = true,

                ParameterId =
                    parameter.Id.Value,

                ParameterName =
                    parameter.Definition?.Name
                    ?? string.Empty,

                SharedParameterGuid =
                    sharedGuid,

                Value =
                    value ?? string.Empty
            };
        }
    }
}