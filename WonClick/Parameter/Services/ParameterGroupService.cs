using Autodesk.Revit.DB;
using System;
using WonClick.Parameter.Models;

namespace WonClick.Parameter.Services
{
    public static class ParameterGroupService
    {
        public static string GetUiGroup(
            ParameterSelectionItem item)
        {
            string rawGroupTypeId =
                item.RawGroupTypeId
                ?? string.Empty;


            // =====================================================
            // TEXT
            //
            // Revit의 실제 Text Parameter Group만 포함합니다.
            // Identity Data는 Text로 분류하지 않습니다.
            // =====================================================

            if (IsSameGroup(
                rawGroupTypeId,
                GroupTypeId.Text))
            {
                return "Text";
            }


            // =====================================================
            // SET
            //
            // Revit 2025:
            // GroupTypeId.CouplerArray
            // 표시명은 "Set"
            // =====================================================

            if (IsSameGroup(
                rawGroupTypeId,
                GroupTypeId.CouplerArray))
            {
                return "Set";
            }


            // =====================================================
            // CONSTRUCTION
            // =====================================================

            if (IsSameGroup(
                rawGroupTypeId,
                GroupTypeId.Construction))
            {
                return "Construction";
            }


            // =====================================================
            // DIMENSIONS
            //
            // Revit의 Geometry Parameter Group은
            // Properties UI에서 Dimensions에 해당합니다.
            //
            // Constraints는 별도 Group이므로 포함하지 않습니다.
            // =====================================================

            if (IsSameGroup(
                rawGroupTypeId,
                GroupTypeId.Geometry))
            {
                return "Dimensions";
            }


            // =====================================================
            // OTHER
            // =====================================================

            return "Other";
        }


        private static bool IsSameGroup(
            string rawGroupTypeId,
            ForgeTypeId targetGroupTypeId)
        {
            if (string.IsNullOrWhiteSpace(
                rawGroupTypeId))
            {
                return false;
            }


            if (targetGroupTypeId == null)
            {
                return false;
            }


            string targetTypeId =
                targetGroupTypeId.TypeId
                ?? string.Empty;


            if (string.IsNullOrWhiteSpace(
                targetTypeId))
            {
                return false;
            }


            return string.Equals(
                rawGroupTypeId,
                targetTypeId,
                StringComparison.Ordinal);
        }
    }
}