using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using WonClick.TypeNameParameter.Models;

using RevitParameter = Autodesk.Revit.DB.Parameter;

namespace WonClick.TypeNameParameter.Services
{
    /// <summary>
    /// 현재 3D View + Section Box 범위에
    /// 고정 SK_* 값을 일괄 입력한다.
    ///
    /// 예:
    /// SK_FL = 03F
    /// SK_UTIL = CHW
    /// SK_ZONE = A
    ///
    /// SK_ITEM / SK_MATE / SK_SIZE는
    /// 자동 계산 Parameter이므로 View Fill 대상에서 제외한다.
    ///
    /// Visibility Gate:
    /// 1. View 기반 Collector
    /// 2. Hide in View 제외
    /// 3. Category Visibility OFF 제외
    /// 4. Visibility OFF Parameter Filter 제외
    /// 5. Visibility OFF Selection Filter 제외
    ///
    /// Insulation은 직접 Fill하지 않고
    /// Host 값 적용 후 InsulationParameterService를 통해 상속한다.
    /// </summary>
    public static class SKViewFillService
    {
        private const string ItemParameterName =
            "SK_ITEM";


        private const string MateParameterName =
            "SK_MATE";


        private const string SizeParameterName =
            "SK_SIZE";


        // =========================================================
        // 현재 View에서 사용할 수 있는 SK_* 목록
        // =========================================================

        public static IReadOnlyList<string>
            GetAvailableParameterNames(
                Document doc,
                ElementId viewId)
        {
            if (doc == null
                ||
                viewId == null
                ||
                viewId ==
                ElementId.InvalidElementId)
            {
                return
                    Array.Empty<string>();
            }


            View? view =
                doc.GetElement(
                    viewId)
                as View;


            if (view == null)
            {
                return
                    Array.Empty<string>();
            }


            ViewVisibilityContext visibilityContext =
                BuildVisibilityContext(
                    doc,
                    view);


            HashSet<string> names =
                new HashSet<string>(
                    StringComparer.Ordinal);


            FilteredElementCollector collector =
                new FilteredElementCollector(
                    doc,
                    viewId)
                    .WhereElementIsNotElementType();


            foreach (Element element
                     in collector)
            {
                if (!IsEligibleViewFillElement(
                        element))
                {
                    continue;
                }


                if (!IsVisibleForViewFill(
                        doc,
                        view,
                        element,
                        visibilityContext))
                {
                    continue;
                }


                foreach (RevitParameter parameter
                         in element.Parameters)
                {
                    if (parameter == null
                        ||
                        parameter.IsReadOnly
                        ||
                        parameter.StorageType !=
                        StorageType.String)
                    {
                        continue;
                    }


                    string name =
                        parameter.Definition?.Name
                        ??
                        string.Empty;


                    if (!IsFillParameterName(
                            name))
                    {
                        continue;
                    }


                    names.Add(
                        name);
                }
            }


            List<string> result =
                names.ToList();


            result.Sort(
                StringComparer
                    .CurrentCultureIgnoreCase);


            return result;
        }


        // =========================================================
        // Apply
        // =========================================================

        public static SKViewFillResult Apply(
            Document doc,
            ElementId viewId,
            IEnumerable<SKViewFillRow> rows)
        {
            SKViewFillResult result =
                new SKViewFillResult();


            if (doc == null
                ||
                viewId == null
                ||
                viewId ==
                ElementId.InvalidElementId
                ||
                rows == null)
            {
                return result;
            }


            View? view =
                doc.GetElement(
                    viewId)
                as View;


            if (view == null)
            {
                return result;
            }


            ViewVisibilityContext visibilityContext =
                BuildVisibilityContext(
                    doc,
                    view);


            Dictionary<string, string> values =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);


            foreach (SKViewFillRow row
                     in rows)
            {
                if (row == null)
                {
                    continue;
                }


                string name =
                    row.ParameterName?.Trim()
                    ??
                    string.Empty;


                if (!IsFillParameterName(
                        name))
                {
                    continue;
                }


                values[name] =
                    row.Value
                    ??
                    string.Empty;
            }


            if (values.Count == 0)
            {
                return result;
            }


            FilteredElementCollector collector =
                new FilteredElementCollector(
                    doc,
                    viewId)
                    .WhereElementIsNotElementType();


            foreach (Element element
                     in collector)
            {
                if (!IsEligibleViewFillElement(
                        element))
                {
                    continue;
                }


                if (!IsVisibleForViewFill(
                        doc,
                        view,
                        element,
                        visibilityContext))
                {
                    result.HiddenElementsSkipped++;

                    continue;
                }


                result.ScannedElements++;


                bool elementChanged =
                    false;


                foreach (
                    KeyValuePair<string, string> pair
                    in values)
                {
                    RevitParameter? parameter =
                        FindWritableStringParameter(
                            element,
                            pair.Key);


                    if (parameter == null)
                    {
                        continue;
                    }


                    result.TargetParameters++;


                    if (TrySetString(
                            parameter,
                            pair.Value))
                    {
                        elementChanged =
                            true;


                        result.UpdatedParameters++;
                    }
                }


                if (elementChanged)
                {
                    result.UpdatedElements++;
                }


                // =================================================
                // Host가 실제 View에서 보이는 경우에만
                // 연결 Insulation도 상속 갱신한다.
                // =================================================

                if (InsulationParameterService
                    .IsSupportedHost(
                        element))
                {
                    result.UpdatedInsulations +=
                        InsulationParameterService
                            .SyncHostInsulations(
                                doc,
                                element);
                }
            }


            return result;
        }


        // =========================================================
        // View Filter 정보 Cache
        // =========================================================

        private static ViewVisibilityContext
            BuildVisibilityContext(
                Document doc,
                View view)
        {
            ViewVisibilityContext context =
                new ViewVisibilityContext();


            ICollection<ElementId> filterIds;


            try
            {
                filterIds =
                    view.GetFilters();
            }
            catch
            {
                return context;
            }


            foreach (ElementId filterId
                     in filterIds)
            {
                // Filter 자체가 Disabled이면 영향 없음
                try
                {
                    if (!view.GetIsFilterEnabled(
                            filterId))
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }


                // Visibility가 ON이면 숨김 Filter가 아님
                try
                {
                    if (view.GetFilterVisibility(
                            filterId))
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }


                Element? filterElement =
                    doc.GetElement(
                        filterId);


                // =================================================
                // Parameter Filter
                // =================================================

                if (filterElement
                    is ParameterFilterElement parameterFilter)
                {
                    HiddenParameterFilterInfo info =
                        new HiddenParameterFilterInfo();


                    try
                    {
                        foreach (ElementId categoryId
                                 in parameterFilter.GetCategories())
                        {
                            info.CategoryIds.Add(
                                categoryId.Value);
                        }
                    }
                    catch
                    {
                    }


                    try
                    {
                        info.ElementFilter =
                            parameterFilter
                                .GetElementFilter();
                    }
                    catch
                    {
                        info.ElementFilter =
                            null;
                    }


                    context.HiddenParameterFilters.Add(
                        info);
                }

                // =================================================
                // Selection Filter
                // =================================================

                else if (filterElement
                    is SelectionFilterElement selectionFilter)
                {
                    try
                    {
                        foreach (ElementId elementId
                                 in selectionFilter.GetElementIds())
                        {
                            context.HiddenSelectionElementIds.Add(
                                elementId.Value);
                        }
                    }
                    catch
                    {
                    }
                }
            }


            return context;
        }


        // =========================================================
        // 실제 View Fill Visibility 판정
        // =========================================================

        private static bool IsVisibleForViewFill(
            Document doc,
            View view,
            Element element,
            ViewVisibilityContext context)
        {
            if (element == null)
            {
                return false;
            }


            // =====================================================
            // 1. Hide in View > Elements
            // =====================================================

            try
            {
                if (element.IsHidden(
                        view))
                {
                    return false;
                }
            }
            catch
            {
            }


            Category? category =
                element.Category;


            if (category == null)
            {
                return false;
            }


            // =====================================================
            // 2. Visibility / Graphics Category OFF
            // =====================================================

            try
            {
                if (view.GetCategoryHidden(
                        category.Id))
                {
                    return false;
                }
            }
            catch
            {
            }


            // =====================================================
            // 3. Selection Filter Visibility OFF
            // =====================================================

            if (context
                .HiddenSelectionElementIds
                .Contains(
                    element.Id.Value))
            {
                return false;
            }


            // =====================================================
            // 4. Parameter Filter Visibility OFF
            //
            // Category가 Filter 대상이고
            // Rule까지 통과하면 실제 View에서 Hidden이므로 제외.
            // =====================================================

            long categoryId =
                category.Id.Value;


            foreach (HiddenParameterFilterInfo info
                     in context.HiddenParameterFilters)
            {
                if (info.CategoryIds.Count > 0
                    &&
                    !info.CategoryIds.Contains(
                        categoryId))
                {
                    continue;
                }


                // Rule이 없는 Category-only Filter
                if (info.ElementFilter == null)
                {
                    return false;
                }


                try
                {
                    if (info.ElementFilter.PassesFilter(
                            doc,
                            element.Id))
                    {
                        return false;
                    }
                }
                catch
                {
                    // 판정할 수 없는 Filter는
                    // View Collector 결과를 유지한다.
                }
            }


            return true;
        }


        // =========================================================
        // Fill 대상
        // =========================================================

        private static bool IsEligibleViewFillElement(
            Element element)
        {
            if (element == null
                ||
                element is ElementType)
            {
                return false;
            }


            // Insulation 자체에는 직접 Fill하지 않는다.
            if (InsulationParameterService
                .IsSupportedInsulation(
                    element))
            {
                return false;
            }


            if (element.ViewSpecific)
            {
                return false;
            }


            if (element is DatumPlane
                ||
                element is View
                ||
                element is Sketch
                ||
                element is CurveElement
                ||
                element is RevitLinkInstance
                ||
                element is ImportInstance)
            {
                return false;
            }


            Category? category =
                element.Category;


            if (category == null
                ||
                category.CategoryType !=
                CategoryType.Model)
            {
                return false;
            }


            if (IsExcludedSystemCategory(
                    category.Id))
            {
                return false;
            }


            return true;
        }


        // =========================================================
        // Parameter Name
        // =========================================================

        private static bool IsFillParameterName(
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(
                    parameterName))
            {
                return false;
            }


            if (!parameterName.StartsWith(
                    "SK_",
                    StringComparison.Ordinal))
            {
                return false;
            }


            return
                !string.Equals(
                    parameterName,
                    ItemParameterName,
                    StringComparison.Ordinal)

                &&

                !string.Equals(
                    parameterName,
                    MateParameterName,
                    StringComparison.Ordinal)

                &&

                !string.Equals(
                    parameterName,
                    SizeParameterName,
                    StringComparison.Ordinal);
        }


        // =========================================================
        // Parameter
        // =========================================================

        private static RevitParameter?
            FindWritableStringParameter(
                Element element,
                string parameterName)
        {
            IList<RevitParameter> parameters;


            try
            {
                parameters =
                    element.GetParameters(
                        parameterName);
            }
            catch
            {
                return null;
            }


            foreach (RevitParameter parameter
                     in parameters)
            {
                if (parameter == null
                    ||
                    parameter.IsReadOnly
                    ||
                    parameter.StorageType !=
                    StorageType.String)
                {
                    continue;
                }


                return parameter;
            }


            return null;
        }


        private static bool TrySetString(
            RevitParameter parameter,
            string value)
        {
            value ??=
                string.Empty;


            try
            {
                string currentValue =
                    parameter.AsString()
                    ??
                    string.Empty;


                if (string.Equals(
                        currentValue,
                        value,
                        StringComparison.Ordinal))
                {
                    return false;
                }


                return
                    parameter.Set(
                        value);
            }
            catch
            {
                return false;
            }
        }


        // =========================================================
        // System / Helper
        // =========================================================

        private static bool IsExcludedSystemCategory(
            ElementId categoryId)
        {
            if (categoryId == null
                ||
                categoryId ==
                ElementId.InvalidElementId)
            {
                return true;
            }


            long id =
                categoryId.Value;


            return
                id ==
                (long)BuiltInCategory.OST_Levels

                ||

                id ==
                (long)BuiltInCategory.OST_Grids

                ||

                id ==
                (long)BuiltInCategory.OST_SectionBox

                ||

                id ==
                (long)BuiltInCategory.OST_VolumeOfInterest

                ||

                id ==
                (long)BuiltInCategory.OST_Views

                ||

                id ==
                (long)BuiltInCategory.OST_Viewers

                ||

                id ==
                (long)BuiltInCategory.OST_Sections

                ||

                id ==
                (long)BuiltInCategory.OST_ReferencePoints

                ||

                id ==
                (long)BuiltInCategory.OST_Lines;
        }


        // =========================================================
        // Visibility Cache
        // =========================================================

        private sealed class ViewVisibilityContext
        {
            public List<HiddenParameterFilterInfo>
                HiddenParameterFilters
            {
                get;
            } =
                new List<HiddenParameterFilterInfo>();


            public HashSet<long>
                HiddenSelectionElementIds
            {
                get;
            } =
                new HashSet<long>();
        }


        private sealed class HiddenParameterFilterInfo
        {
            public HashSet<long> CategoryIds
            {
                get;
            } =
                new HashSet<long>();


            public ElementFilter? ElementFilter
            {
                get;
                set;
            }
        }
    }


    // =============================================================
    // Result
    // =============================================================

    public sealed class SKViewFillResult
    {
        public int ScannedElements
        {
            get;
            set;
        }


        public int HiddenElementsSkipped
        {
            get;
            set;
        }


        public int TargetParameters
        {
            get;
            set;
        }


        public int UpdatedElements
        {
            get;
            set;
        }


        public int UpdatedParameters
        {
            get;
            set;
        }


        public int UpdatedInsulations
        {
            get;
            set;
        }
    }
}
