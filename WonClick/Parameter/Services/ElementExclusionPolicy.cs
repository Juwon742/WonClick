using Autodesk.Revit.DB;
using System;

namespace WonClick.Parameter.Services
{
    public static class ElementExclusionPolicy
    {
        public static bool ShouldExclude(
            Element? element)
        {
            // =====================================================
            // NULL
            // =====================================================

            if (element == null)
            {
                return true;
            }


            // =====================================================
            // ELEMENT TYPE
            // =====================================================

            if (element is ElementType)
            {
                return true;
            }


            // =====================================================
            // VIEW SPECIFIC
            // =====================================================

            if (element.ViewSpecific)
            {
                return true;
            }


            // =====================================================
            // CATEGORY
            // =====================================================

            Category? category =
                element.Category;


            if (category == null)
            {
                return true;
            }


            if (category.CategoryType
                != CategoryType.Model)
            {
                return true;
            }


            // =====================================================
            // REVIT LINK
            //
            // 현재 WonClick Parameter는 Host Model만 대상으로 합니다.
            // =====================================================

            if (element is RevitLinkInstance)
            {
                return true;
            }


            // =====================================================
            // CAMERA
            // =====================================================

            if (category.Id.Value
                == (long)BuiltInCategory.OST_Cameras)
            {
                return true;
            }


            // =====================================================
            // CENTER LINE
            //
            // 대소문자를 구분하지 않습니다.
            //
            // 예:
            // Center Line
            // CENTER LINE
            // center line
            // Center line
            //
            // 모두 제외합니다.
            // =====================================================

            if (IsCenterLine(
                element,
                category))
            {
                return true;
            }


            return false;
        }


        // =========================================================
        // CENTER LINE CHECK
        // =========================================================

        private static bool IsCenterLine(
            Element element,
            Category category)
        {
            // -----------------------------------------------------
            // Element Name
            // -----------------------------------------------------

            string elementName =
                element.Name
                    ?.Trim()
                ?? string.Empty;


            if (string.Equals(
                elementName,
                "Center Line",
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }


            // -----------------------------------------------------
            // Category Name
            //
            // 객체 자체의 Name 대신 Category가 Center Line으로
            // 정의된 경우도 같이 제외합니다.
            // -----------------------------------------------------

            string categoryName =
                category.Name
                    ?.Trim()
                ?? string.Empty;


            if (string.Equals(
                categoryName,
                "Center Line",
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }


            return false;
        }
    }
}