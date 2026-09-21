using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using System;
using System.Collections.Generic;
using System.Globalization;

using RevitParameter = Autodesk.Revit.DB.Parameter;

namespace WonClick.TypeNameParameter.Services
{
    /// <summary>
    /// Pipe / Duct Insulation의 SK Parameter를 관리한다.
    ///
    /// 규칙:
    ///
    /// 1. SK_ITEM
    ///    Insulation 자신의 Type Name [0]
    ///
    /// 2. SK_MATE
    ///    Insulation 자신의 Type Name [1]
    ///
    /// 3. SK_SIZE
    ///    Insulation 자신의 Thickness(mm)
    ///    예: 40.0 -> 40T
    ///        19.0 -> 19T
    ///
    /// 4. 나머지 SK_* Parameter
    ///    Host Pipe / Duct / Fitting / Accessory의 값을 상속
    ///
    ///    예:
    ///    SK_UTIL
    ///    SK_FL
    ///    SK_ZONE
    ///    SK_AREA
    ///    ...
    ///
    /// SK_ITEM / SK_MATE / SK_SIZE는 Host에서 복사하지 않는다.
    /// </summary>
    public static class InsulationParameterService
    {
        private const string ItemParameterName =
            "SK_ITEM";


        private const string MateParameterName =
            "SK_MATE";


        private const string SizeParameterName =
            "SK_SIZE";


        private const string SKPrefix =
            "SK_";


        // =========================================================
        // Supported Insulation
        // =========================================================

        public static bool IsSupportedInsulation(
            Element element)
        {
            return
                element is PipeInsulation
                ||
                element is DuctInsulation;
        }


        // =========================================================
        // Supported Host
        // =========================================================

        public static bool IsSupportedHost(
            Element element)
        {
            if (element == null)
            {
                return false;
            }


            Category? category =
                element.Category;


            if (category == null)
            {
                return false;
            }


            long categoryId =
                category.Id.Value;


            return
                categoryId ==
                (long)BuiltInCategory.OST_PipeCurves

                ||

                categoryId ==
                (long)BuiltInCategory.OST_FlexPipeCurves

                ||

                categoryId ==
                (long)BuiltInCategory.OST_PipeFitting

                ||

                categoryId ==
                (long)BuiltInCategory.OST_PipeAccessory

                ||

                categoryId ==
                (long)BuiltInCategory.OST_DuctCurves

                ||

                categoryId ==
                (long)BuiltInCategory.OST_FlexDuctCurves

                ||

                categoryId ==
                (long)BuiltInCategory.OST_DuctFitting

                ||

                categoryId ==
                (long)BuiltInCategory.OST_DuctAccessory;
        }


        // =========================================================
        // Single Insulation Sync
        // =========================================================

        public static bool SyncInsulation(
            Document doc,
            Element insulationElement)
        {
            if (doc == null
                ||
                insulationElement == null)
            {
                return false;
            }


            if (!IsSupportedInsulation(
                    insulationElement))
            {
                return false;
            }


            bool changed =
                false;


            try
            {
                // =================================================
                // 1. Insulation 자신의 Type Name
                // =================================================

                string typeName =
                    GetElementTypeName(
                        doc,
                        insulationElement);


                ParseTypeName(
                    typeName,
                    out string item,
                    out string mate);


                if (!string.IsNullOrWhiteSpace(
                        item))
                {
                    RevitParameter? itemParameter =
                        FindWritableStringParameter(
                            insulationElement,
                            ItemParameterName);


                    if (TrySetString(
                            itemParameter,
                            item,
                            allowEmpty: false))
                    {
                        changed =
                            true;
                    }
                }


                if (!string.IsNullOrWhiteSpace(
                        mate))
                {
                    RevitParameter? mateParameter =
                        FindWritableStringParameter(
                            insulationElement,
                            MateParameterName);


                    if (TrySetString(
                            mateParameter,
                            mate,
                            allowEmpty: false))
                    {
                        changed =
                            true;
                    }
                }


                // =================================================
                // 2. Insulation 자신의 Thickness -> SK_SIZE
                // =================================================

                string sizeValue =
                    GetInsulationThicknessText(
                        insulationElement);


                if (!string.IsNullOrWhiteSpace(
                        sizeValue))
                {
                    RevitParameter? sizeParameter =
                        FindWritableStringParameter(
                            insulationElement,
                            SizeParameterName);


                    if (TrySetString(
                            sizeParameter,
                            sizeValue,
                            allowEmpty: false))
                    {
                        changed =
                            true;
                    }
                }


                // =================================================
                // 3. Host의 나머지 SK_* 상속
                // =================================================

                Element? host =
                    GetHostElement(
                        doc,
                        insulationElement);


                if (host != null)
                {
                    if (CopyHostSKParameters(
                            host,
                            insulationElement))
                    {
                        changed =
                            true;
                    }
                }
            }
            catch
            {
                return changed;
            }


            return changed;
        }


        // =========================================================
        // Host -> 연결 Insulation 전체 Sync
        // =========================================================

        public static int SyncHostInsulations(
            Document doc,
            Element host)
        {
            if (doc == null
                ||
                host == null)
            {
                return 0;
            }


            if (!IsSupportedHost(
                    host))
            {
                return 0;
            }


            ICollection<ElementId> insulationIds;


            try
            {
                insulationIds =
                    InsulationLiningBase
                        .GetInsulationIds(
                            doc,
                            host.Id);
            }
            catch
            {
                return 0;
            }


            int changedCount =
                0;


            foreach (ElementId insulationId
                     in insulationIds)
            {
                Element? insulation;


                try
                {
                    insulation =
                        doc.GetElement(
                            insulationId);
                }
                catch
                {
                    continue;
                }


                if (insulation == null)
                {
                    continue;
                }


                if (SyncInsulation(
                        doc,
                        insulation))
                {
                    changedCount++;
                }
            }


            return changedCount;
        }


        // =========================================================
        // Entire Project
        // =========================================================

        public static int SyncDocument(
            Document doc)
        {
            if (doc == null)
            {
                return 0;
            }


            ElementMulticategoryFilter filter =
                CreateInsulationCategoryFilter();


            FilteredElementCollector collector =
                new FilteredElementCollector(
                    doc)
                    .WhereElementIsNotElementType()
                    .WherePasses(
                        filter);


            int changedCount =
                0;


            foreach (Element element
                     in collector)
            {
                if (SyncInsulation(
                        doc,
                        element))
                {
                    changedCount++;
                }
            }


            return changedCount;
        }


        // =========================================================
        // Current View
        // =========================================================

        public static int SyncView(
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
                return 0;
            }


            ElementMulticategoryFilter filter =
                CreateInsulationCategoryFilter();


            FilteredElementCollector collector =
                new FilteredElementCollector(
                    doc,
                    viewId)
                    .WhereElementIsNotElementType()
                    .WherePasses(
                        filter);


            int changedCount =
                0;


            foreach (Element element
                     in collector)
            {
                if (SyncInsulation(
                        doc,
                        element))
                {
                    changedCount++;
                }
            }


            return changedCount;
        }


        // =========================================================
        // Selected Elements
        //
        // Insulation 선택:
        // -> 해당 Insulation Sync
        //
        // Host 선택:
        // -> 연결 Insulation Sync
        // =========================================================

        public static int SyncElements(
            Document doc,
            IEnumerable<ElementId> elementIds)
        {
            if (doc == null
                ||
                elementIds == null)
            {
                return 0;
            }


            int changedCount =
                0;


            HashSet<long> processedInsulationIds =
                new HashSet<long>();


            foreach (ElementId elementId
                     in elementIds)
            {
                if (elementId == null
                    ||
                    elementId ==
                    ElementId.InvalidElementId)
                {
                    continue;
                }


                Element? element;


                try
                {
                    element =
                        doc.GetElement(
                            elementId);
                }
                catch
                {
                    continue;
                }


                if (element == null)
                {
                    continue;
                }


                if (IsSupportedInsulation(
                        element))
                {
                    if (!processedInsulationIds.Add(
                            element.Id.Value))
                    {
                        continue;
                    }


                    if (SyncInsulation(
                            doc,
                            element))
                    {
                        changedCount++;
                    }


                    continue;
                }


                if (!IsSupportedHost(
                        element))
                {
                    continue;
                }


                ICollection<ElementId> insulationIds;


                try
                {
                    insulationIds =
                        InsulationLiningBase
                            .GetInsulationIds(
                                doc,
                                element.Id);
                }
                catch
                {
                    continue;
                }


                foreach (ElementId insulationId
                         in insulationIds)
                {
                    if (!processedInsulationIds.Add(
                            insulationId.Value))
                    {
                        continue;
                    }


                    Element? insulation =
                        doc.GetElement(
                            insulationId);


                    if (insulation == null)
                    {
                        continue;
                    }


                    if (SyncInsulation(
                            doc,
                            insulation))
                    {
                        changedCount++;
                    }
                }
            }


            return changedCount;
        }


        // =========================================================
        // Host 가져오기
        // =========================================================

        private static Element? GetHostElement(
            Document doc,
            Element insulationElement)
        {
            if (insulationElement
                is not InsulationLiningBase insulation)
            {
                return null;
            }


            ElementId hostId =
                insulation.HostElementId;


            if (hostId == null
                ||
                hostId ==
                ElementId.InvalidElementId)
            {
                return null;
            }


            Element? host =
                doc.GetElement(
                    hostId);


            if (host == null)
            {
                return null;
            }


            return
                IsSupportedHost(
                    host)
                    ?
                    host
                    :
                    null;
        }


        // =========================================================
        // Host의 SK_* 상속
        //
        // 제외:
        // SK_ITEM
        // SK_MATE
        // SK_SIZE
        // =========================================================

        private static bool CopyHostSKParameters(
            Element host,
            Element insulation)
        {
            bool changed =
                false;


            foreach (RevitParameter sourceParameter
                     in host.Parameters)
            {
                if (sourceParameter == null)
                {
                    continue;
                }


                Definition? definition =
                    sourceParameter.Definition;


                if (definition == null)
                {
                    continue;
                }


                string parameterName =
                    definition.Name
                    ??
                    string.Empty;


                if (!parameterName.StartsWith(
                        SKPrefix,
                        StringComparison.Ordinal))
                {
                    continue;
                }


                if (IsCoreSKParameter(
                        parameterName))
                {
                    continue;
                }


                RevitParameter? targetParameter =
                    FindMatchingTargetParameter(
                        insulation,
                        sourceParameter,
                        parameterName);


                if (targetParameter == null)
                {
                    continue;
                }


                if (targetParameter.IsReadOnly
                    ||
                    targetParameter.StorageType !=
                    StorageType.String)
                {
                    continue;
                }


                string sourceValue =
                    ReadParameterAsText(
                        sourceParameter);


                // Host가 비어 있으면 Insulation도 비운다.
                if (TrySetString(
                        targetParameter,
                        sourceValue,
                        allowEmpty: true))
                {
                    changed =
                        true;
                }
            }


            return changed;
        }


        // =========================================================
        // Matching Target Parameter
        // =========================================================

        private static RevitParameter?
            FindMatchingTargetParameter(
                Element targetElement,
                RevitParameter sourceParameter,
                string parameterName)
        {
            // =====================================================
            // 1. Shared Parameter GUID
            // =====================================================

            if (sourceParameter.IsShared)
            {
                try
                {
                    RevitParameter? byGuid =
                        targetElement.get_Parameter(
                            sourceParameter.GUID);


                    if (byGuid != null)
                    {
                        return byGuid;
                    }
                }
                catch
                {
                }
            }


            // =====================================================
            // 2. 동일 ParameterId
            // =====================================================

            foreach (RevitParameter targetParameter
                     in targetElement.Parameters)
            {
                if (targetParameter == null)
                {
                    continue;
                }


                try
                {
                    if (targetParameter.Id.Value ==
                        sourceParameter.Id.Value)
                    {
                        return targetParameter;
                    }
                }
                catch
                {
                }
            }


            // =====================================================
            // 3. Name Fallback
            // =====================================================

            IList<RevitParameter> sameNameParameters;


            try
            {
                sameNameParameters =
                    targetElement.GetParameters(
                        parameterName);
            }
            catch
            {
                return null;
            }


            RevitParameter? matched =
                null;


            int matchCount =
                0;


            foreach (RevitParameter parameter
                     in sameNameParameters)
            {
                if (parameter == null)
                {
                    continue;
                }


                matched =
                    parameter;


                matchCount++;
            }


            return
                matchCount == 1
                    ?
                    matched
                    :
                    null;
        }


        // =========================================================
        // Insulation Type Name
        // =========================================================

        private static string GetElementTypeName(
            Document doc,
            Element element)
        {
            try
            {
                ElementId typeId =
                    element.GetTypeId();


                if (typeId == null
                    ||
                    typeId ==
                    ElementId.InvalidElementId)
                {
                    return string.Empty;
                }


                Element? typeElement =
                    doc.GetElement(
                        typeId);


                return
                    typeElement?.Name
                    ??
                    string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }


        // =========================================================
        // Type Name Parser
        //
        // Pipe Insulation, Foam Rubber, NA
        //
        // [0] -> Pipe Insulation
        // [1] -> Foam Rubber
        // =========================================================

        private static void ParseTypeName(
            string typeName,
            out string item,
            out string mate)
        {
            item =
                string.Empty;


            mate =
                string.Empty;


            if (string.IsNullOrWhiteSpace(
                    typeName))
            {
                return;
            }


            if (typeName.IndexOf(
                    ',') < 0)
            {
                return;
            }


            string[] parts =
                typeName.Split(
                    ',',
                    StringSplitOptions.None);


            if (parts.Length >= 1)
            {
                item =
                    parts[0].Trim();
            }


            if (parts.Length >= 2)
            {
                mate =
                    parts[1].Trim();
            }
        }


        // =========================================================
        // Thickness -> 40T / 19T
        // =========================================================

        private static string GetInsulationThicknessText(
            Element insulationElement)
        {
            // =====================================================
            // 1순위:
            // Revit 2025 InsulationLiningBase.Thickness
            //
            // PipeInsulation / DuctInsulation 모두
            // InsulationLiningBase를 상속하므로 공통 처리.
            //
            // 기존 BuiltInParameter 직접 읽기에서
            // 40 mm가 0T로 들어가는 환경이 확인되어
            // 실제 Insulation 객체의 Thickness Property를
            // Source of Truth로 사용한다.
            // =====================================================

            try
            {
                if (insulationElement
                    is InsulationLiningBase insulation)
                {
                    double internalThickness =
                        insulation.Thickness;


                    if (internalThickness > 1e-9)
                    {
                        return
                            ConvertThicknessToText(
                                internalThickness);
                    }
                }
            }
            catch
            {
                // Fallback으로 계속
            }


            // =====================================================
            // 2순위 Fallback:
            // Pipe / Duct BuiltInParameter
            // =====================================================

            RevitParameter? thicknessParameter =
                null;


            try
            {
                if (insulationElement
                    is PipeInsulation)
                {
                    thicknessParameter =
                        insulationElement.get_Parameter(
                            BuiltInParameter
                                .PIPE_INSULATION_THICKNESS);
                }
                else if (insulationElement
                    is DuctInsulation)
                {
                    thicknessParameter =
                        insulationElement.get_Parameter(
                            BuiltInParameter
                                .DUCT_INSULATION_THICKNESS);
                }
            }
            catch
            {
                thicknessParameter =
                    null;
            }


            if (thicknessParameter != null
                &&
                thicknessParameter.StorageType ==
                StorageType.Double)
            {
                try
                {
                    double internalThickness =
                        thicknessParameter.AsDouble();


                    if (internalThickness > 1e-9)
                    {
                        return
                            ConvertThicknessToText(
                                internalThickness);
                    }
                }
                catch
                {
                }
            }


            // =====================================================
            // 3순위 Fallback:
            // Properties > Mechanical > Insulation Thickness
            // 이름으로 직접 검색
            // =====================================================

            try
            {
                IList<RevitParameter> parameters =
                    insulationElement.GetParameters(
                        "Insulation Thickness");


                foreach (RevitParameter parameter
                         in parameters)
                {
                    if (parameter == null
                        ||
                        parameter.StorageType !=
                        StorageType.Double)
                    {
                        continue;
                    }


                    double internalThickness =
                        parameter.AsDouble();


                    if (internalThickness <= 1e-9)
                    {
                        continue;
                    }


                    return
                        ConvertThicknessToText(
                            internalThickness);
                }
            }
            catch
            {
            }


            return string.Empty;
        }


        // =========================================================
        // Revit Internal Length -> mm -> 정수 + T
        //
        // 40.0 mm -> 40T
        // 19.0 mm -> 19T
        // =========================================================

        private static string ConvertThicknessToText(
            double internalThickness)
        {
            try
            {
                double millimeters =
                    UnitUtils.ConvertFromInternalUnits(
                        internalThickness,
                        UnitTypeId.Millimeters);


                double rounded =
                    Math.Round(
                        millimeters,
                        0,
                        MidpointRounding.AwayFromZero);


                return
                    rounded.ToString(
                        "0",
                        CultureInfo.InvariantCulture)
                    +
                    "T";
            }
            catch
            {
                return string.Empty;
            }
        }


        // =========================================================
        // Parameter Helpers
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
                if (parameter == null)
                {
                    continue;
                }


                if (parameter.IsReadOnly)
                {
                    continue;
                }


                if (parameter.StorageType !=
                    StorageType.String)
                {
                    continue;
                }


                return parameter;
            }


            return null;
        }


        private static string ReadParameterAsText(
            RevitParameter parameter)
        {
            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.String:

                        return
                            parameter.AsString()
                            ??
                            string.Empty;


                    case StorageType.Integer:

                        return
                            parameter.AsInteger()
                                .ToString(
                                    CultureInfo.InvariantCulture);


                    case StorageType.Double:

                        return
                            parameter.AsValueString()
                            ??
                            string.Empty;


                    default:

                        return string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }


        private static bool TrySetString(
            RevitParameter? parameter,
            string value,
            bool allowEmpty)
        {
            if (parameter == null)
            {
                return false;
            }


            if (parameter.IsReadOnly
                ||
                parameter.StorageType !=
                StorageType.String)
            {
                return false;
            }


            value ??=
                string.Empty;


            if (!allowEmpty
                &&
                string.IsNullOrWhiteSpace(
                    value))
            {
                return false;
            }


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


        private static bool IsCoreSKParameter(
            string parameterName)
        {
            return
                string.Equals(
                    parameterName,
                    ItemParameterName,
                    StringComparison.Ordinal)

                ||

                string.Equals(
                    parameterName,
                    MateParameterName,
                    StringComparison.Ordinal)

                ||

                string.Equals(
                    parameterName,
                    SizeParameterName,
                    StringComparison.Ordinal);
        }


        private static ElementMulticategoryFilter
            CreateInsulationCategoryFilter()
        {
            List<ElementId> categoryIds =
                new List<ElementId>
                {
                    new ElementId(
                        BuiltInCategory.OST_PipeInsulations),

                    new ElementId(
                        BuiltInCategory.OST_DuctInsulations)
                };


            return
                new ElementMulticategoryFilter(
                    categoryIds);
        }
    }
}
