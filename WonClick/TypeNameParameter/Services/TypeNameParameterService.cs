using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using WonClick.TypeNameParameter.Models;

using RevitParameter = Autodesk.Revit.DB.Parameter;

namespace WonClick.TypeNameParameter.Services
{
    public static class TypeNameParameterService
    {
        private const string ItemParameterName =
            "SK_ITEM";


        private const string MateParameterName =
            "SK_MATE";


        private const string SizeParameterName =
            "SK_SIZE";


        private const string SourceSizeParameterName =
            "Size";


        private const string SourceAngleParameterName =
            "Angle";


        // =========================================================
        // Entire Project
        // =========================================================

        public static TypeNameParameterBatchResult
            SyncDocument(
                Document doc)
        {
            if (doc == null)
            {
                throw new ArgumentNullException(
                    nameof(doc));
            }


            Stopwatch stopwatch =
                Stopwatch.StartNew();


            SyncRunContext context =
                CreateRunContext(
                    doc);


            TypeNameParameterBatchResult result =
                CreateBatchResult(
                    context);


            if (context.TargetCategories.Count == 0)
            {
                FinishResult(
                    context,
                    result,
                    stopwatch);

                return result;
            }


            List<ElementId> categoryIds =
                new List<ElementId>();


            foreach (CategoryTargetInfo info
                     in context.TargetCategories.Values)
            {
                categoryIds.Add(
                    info.CategoryId);
            }


            ElementMulticategoryFilter categoryFilter =
                new ElementMulticategoryFilter(
                    categoryIds);


            FilteredElementCollector collector =
                new FilteredElementCollector(
                    doc)
                    .WhereElementIsNotElementType()
                    .WherePasses(
                        categoryFilter);


            foreach (Element element
                     in collector)
            {
                ProcessBatchElement(
                    context,
                    result,
                    element);
            }


            FinishResult(
                context,
                result,
                stopwatch);


            return result;
        }


        // =========================================================
        // Current View
        // =========================================================

        public static TypeNameParameterBatchResult
            SyncView(
                Document doc,
                ElementId viewId)
        {
            if (doc == null)
            {
                throw new ArgumentNullException(
                    nameof(doc));
            }


            if (viewId == null
                ||
                viewId == ElementId.InvalidElementId)
            {
                throw new ArgumentException(
                    "유효하지 않은 View Id입니다.",
                    nameof(viewId));
            }


            Stopwatch stopwatch =
                Stopwatch.StartNew();


            SyncRunContext context =
                CreateRunContext(
                    doc);


            TypeNameParameterBatchResult result =
                CreateBatchResult(
                    context);


            if (context.TargetCategories.Count == 0)
            {
                FinishResult(
                    context,
                    result,
                    stopwatch);

                return result;
            }


            List<ElementId> categoryIds =
                new List<ElementId>();


            foreach (CategoryTargetInfo info
                     in context.TargetCategories.Values)
            {
                categoryIds.Add(
                    info.CategoryId);
            }


            ElementMulticategoryFilter categoryFilter =
                new ElementMulticategoryFilter(
                    categoryIds);


            FilteredElementCollector collector =
                new FilteredElementCollector(
                    doc,
                    viewId)
                    .WhereElementIsNotElementType()
                    .WherePasses(
                        categoryFilter);


            foreach (Element element
                     in collector)
            {
                ProcessBatchElement(
                    context,
                    result,
                    element);
            }


            FinishResult(
                context,
                result,
                stopwatch);


            return result;
        }


        // =========================================================
        // Selected Elements
        // =========================================================

        public static TypeNameParameterBatchResult
            SyncElements(
                Document doc,
                IEnumerable<ElementId> elementIds)
        {
            if (doc == null)
            {
                throw new ArgumentNullException(
                    nameof(doc));
            }


            if (elementIds == null)
            {
                throw new ArgumentNullException(
                    nameof(elementIds));
            }


            Stopwatch stopwatch =
                Stopwatch.StartNew();


            SyncRunContext context =
                CreateRunContext(
                    doc);


            TypeNameParameterBatchResult result =
                CreateBatchResult(
                    context);


            if (context.TargetCategories.Count == 0)
            {
                FinishResult(
                    context,
                    result,
                    stopwatch);

                return result;
            }


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


                Element? element =
                    doc.GetElement(
                        elementId);


                if (element == null)
                {
                    continue;
                }


                ProcessBatchElement(
                    context,
                    result,
                    element);
            }


            FinishResult(
                context,
                result,
                stopwatch);


            return result;
        }


        // =========================================================
        // Single Element
        //
        // 향후 IUpdater용
        // =========================================================

        public static TypeNameParameterElementResult
            SyncElement(
                Document doc,
                Element element)
        {
            TypeNameParameterElementResult result =
                new TypeNameParameterElementResult();


            if (doc == null
                ||
                element == null)
            {
                return result;
            }


            if (!IsEligibleModelElement(
                    element))
            {
                return result;
            }


            try
            {
                // =================================================
                // Naming Convention Gate
                //
                // ","가 없는 Type은
                // SK_ITEM / SK_MATE / SK_SIZE
                // 전부 건드리지 않는다.
                // =================================================

                SyncRunContext context =
                    new SyncRunContext(
                        doc);


                ParsedTypeName parsedTypeName =
                    GetParsedTypeName(
                        context,
                        element);


                if (!parsedTypeName.HasComma)
                {
                    return result;
                }


                RevitParameter? itemParameter =
                    element.LookupParameter(
                        ItemParameterName);


                RevitParameter? mateParameter =
                    element.LookupParameter(
                        MateParameterName);


                RevitParameter? sizeParameter =
                    element.LookupParameter(
                        SizeParameterName);


                CategoryTargetInfo targetInfo =
                    new CategoryTargetInfo(
                        element.Category!.Id)
                    {
                        HasItem =
                            itemParameter != null,

                        HasMate =
                            mateParameter != null,

                        HasSize =
                            sizeParameter != null
                    };


                return
                    SyncElementInternal(
                        context,
                        element,
                        targetInfo,
                        parsedTypeName);
            }
            catch
            {
                result.HadError =
                    true;

                return result;
            }
        }


        // =========================================================
        // Batch Element
        // =========================================================

        private static void ProcessBatchElement(
            SyncRunContext context,
            TypeNameParameterBatchResult batchResult,
            Element element)
        {
            // =====================================================
            // Model Element Gate
            // =====================================================

            if (!IsEligibleModelElement(
                    element))
            {
                return;
            }


            Category? category =
                element.Category;


            if (category == null)
            {
                return;
            }


            long categoryId =
                category.Id.Value;


            // =====================================================
            // SK_* Binding Category인지 확인
            // =====================================================

            if (!context.TargetCategories.TryGetValue(
                    categoryId,
                    out CategoryTargetInfo? targetInfo))
            {
                return;
            }


            batchResult.ScannedElements++;


            // =====================================================
            // 가장 먼저 Type Name Cache 확인
            //
            // 여기서 ","가 없으면 아래의
            //
            // LookupParameter(SK_ITEM)
            // LookupParameter(SK_MATE)
            // LookupParameter(SK_SIZE)
            // Size Search
            // Angle Search
            //
            // 자체를 수행하지 않는다.
            // =====================================================

            ParsedTypeName parsedTypeName =
                GetParsedTypeName(
                    context,
                    element);


            if (!parsedTypeName.HasComma)
            {
                batchResult.SkippedNoComma++;

                return;
            }


            TypeNameParameterElementResult
                elementResult;


            try
            {
                elementResult =
                    SyncElementInternal(
                        context,
                        element,
                        targetInfo,
                        parsedTypeName);
            }
            catch
            {
                batchResult.ErrorElements++;

                return;
            }


            if (elementResult.HasTargetParameter)
            {
                batchResult.TargetElements++;
            }


            if (elementResult.Changed)
            {
                batchResult.UpdatedElements++;
            }


            batchResult.ItemUpdated +=
                elementResult.ItemUpdated;


            batchResult.MateUpdated +=
                elementResult.MateUpdated;


            batchResult.SizeUpdated +=
                elementResult.SizeUpdated;


            if (elementResult.HadError)
            {
                batchResult.ErrorElements++;
            }
        }


        // =========================================================
        // 실제 Sync
        // =========================================================

        private static TypeNameParameterElementResult
            SyncElementInternal(
                SyncRunContext context,
                Element element,
                CategoryTargetInfo targetInfo,
                ParsedTypeName parsedTypeName)
        {
            TypeNameParameterElementResult result =
                new TypeNameParameterElementResult();


            try
            {
                // 안전장치
                if (!parsedTypeName.HasComma)
                {
                    return result;
                }


                RevitParameter? itemParameter =
                    null;


                RevitParameter? mateParameter =
                    null;


                RevitParameter? sizeParameter =
                    null;


                // =================================================
                // Binding된 Parameter만 조회
                // =================================================

                if (targetInfo.HasItem)
                {
                    itemParameter =
                        element.LookupParameter(
                            ItemParameterName);
                }


                if (targetInfo.HasMate)
                {
                    mateParameter =
                        element.LookupParameter(
                            MateParameterName);
                }


                if (targetInfo.HasSize)
                {
                    sizeParameter =
                        element.LookupParameter(
                            SizeParameterName);
                }


                result.HasTargetParameter =
                    itemParameter != null
                    ||
                    mateParameter != null
                    ||
                    sizeParameter != null;


                if (!result.HasTargetParameter)
                {
                    return result;
                }


                // =================================================
                // SK_ITEM
                // =================================================

                if (itemParameter != null
                    &&
                    !string.IsNullOrWhiteSpace(
                        parsedTypeName.Item))
                {
                    string itemValue =
                        BuildItemValue(
                            context,
                            element,
                            parsedTypeName);


                    if (!string.IsNullOrWhiteSpace(
                            itemValue))
                    {
                        if (TrySetTextValue(
                                itemParameter,
                                itemValue))
                        {
                            result.ItemUpdated =
                                1;
                        }
                    }
                }


                // =================================================
                // SK_MATE
                // =================================================

                if (mateParameter != null
                    &&
                    !string.IsNullOrWhiteSpace(
                        parsedTypeName.Mate))
                {
                    if (TrySetTextValue(
                            mateParameter,
                            parsedTypeName.Mate))
                    {
                        result.MateUpdated =
                            1;
                    }
                }


                // =================================================
                // SK_SIZE
                //
                // Dimensions > Size
                // =================================================

                if (sizeParameter != null)
                {
                    RevitParameter? sourceSize =
                        FindDimensionsParameter(
                            context,
                            element,
                            SourceSizeParameterName);


                    if (sourceSize != null)
                    {
                        string sizeValue =
                            ReadParameterDisplayValue(
                                sourceSize);


                        if (!string.IsNullOrWhiteSpace(
                                sizeValue))
                        {
                            if (TrySetTextValue(
                                    sizeParameter,
                                    sizeValue))
                            {
                                result.SizeUpdated =
                                    1;
                            }
                        }
                    }
                }
            }
            catch
            {
                result.HadError =
                    true;
            }


            return result;
        }


        // =========================================================
        // SK_ITEM
        //
        // 일반:
        // Elbow_Long
        //
        // Elbow:
        // 90° Elbow_Long
        // =========================================================

        private static string BuildItemValue(
            SyncRunContext context,
            Element element,
            ParsedTypeName parsedTypeName)
        {
            string item =
                parsedTypeName.Item;


            if (!parsedTypeName.ContainsElbow)
            {
                return item;
            }


            RevitParameter? angleParameter =
                FindDimensionsParameter(
                    context,
                    element,
                    SourceAngleParameterName);


            if (angleParameter == null)
            {
                return item;
            }


            string angleText =
                ReadAngleAsIntegerDegrees(
                    angleParameter);


            if (string.IsNullOrWhiteSpace(
                    angleText))
            {
                return item;
            }


            return
                angleText
                +
                "° "
                +
                item;
        }


        // =========================================================
        // Angle -> 정수 Degree
        // =========================================================

        private static string ReadAngleAsIntegerDegrees(
            RevitParameter parameter)
        {
            if (parameter == null)
            {
                return string.Empty;
            }


            try
            {
                if (parameter.StorageType ==
                    StorageType.Double)
                {
                    double internalValue =
                        parameter.AsDouble();


                    double degreeValue =
                        UnitUtils.ConvertFromInternalUnits(
                            internalValue,
                            UnitTypeId.Degrees);


                    double roundedDegree =
                        Math.Round(
                            degreeValue,
                            0,
                            MidpointRounding.AwayFromZero);


                    return
                        roundedDegree.ToString(
                            "0",
                            CultureInfo.InvariantCulture);
                }


                string value =
                    ReadParameterDisplayValue(
                        parameter);


                if (string.IsNullOrWhiteSpace(
                        value))
                {
                    return string.Empty;
                }


                string normalized =
                    value
                        .Replace(
                            "°",
                            string.Empty)
                        .Trim();


                if (double.TryParse(
                        normalized,
                        NumberStyles.Float,
                        CultureInfo.CurrentCulture,
                        out double currentCultureValue))
                {
                    return
                        Math.Round(
                            currentCultureValue,
                            0,
                            MidpointRounding.AwayFromZero)
                        .ToString(
                            "0",
                            CultureInfo.InvariantCulture);
                }


                if (double.TryParse(
                        normalized,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double invariantValue))
                {
                    return
                        Math.Round(
                            invariantValue,
                            0,
                            MidpointRounding.AwayFromZero)
                        .ToString(
                            "0",
                            CultureInfo.InvariantCulture);
                }
            }
            catch
            {
            }


            return string.Empty;
        }


        // =========================================================
        // Context
        // =========================================================

        private static SyncRunContext CreateRunContext(
            Document doc)
        {
            SyncRunContext context =
                new SyncRunContext(
                    doc);


            context.TargetCategories =
                BuildTargetCategoryMap(
                    doc);


            return context;
        }


        // =========================================================
        // SK_* Instance Binding Category
        // =========================================================

        private static Dictionary<long, CategoryTargetInfo>
            BuildTargetCategoryMap(
                Document doc)
        {
            Dictionary<long, CategoryTargetInfo> result =
                new Dictionary<long, CategoryTargetInfo>();


            BindingMap bindingMap =
                doc.ParameterBindings;


            DefinitionBindingMapIterator iterator =
                bindingMap.ForwardIterator();


            iterator.Reset();


            while (iterator.MoveNext())
            {
                Definition? definition =
                    iterator.Key;


                if (definition == null)
                {
                    continue;
                }


                TargetParameterKind parameterKind =
                    GetTargetParameterKind(
                        definition.Name);


                if (parameterKind ==
                    TargetParameterKind.None)
                {
                    continue;
                }


                // Instance Binding만
                if (iterator.Current
                    is not InstanceBinding
                    instanceBinding)
                {
                    continue;
                }


                CategorySet categories =
                    instanceBinding.Categories;


                foreach (Category category
                         in categories)
                {
                    if (category == null)
                    {
                        continue;
                    }


                    if (category.CategoryType !=
                        CategoryType.Model)
                    {
                        continue;
                    }


                    if (IsExcludedSystemCategory(
                            category.Id))
                    {
                        continue;
                    }


                    long categoryId =
                        category.Id.Value;


                    if (!result.TryGetValue(
                            categoryId,
                            out CategoryTargetInfo? info))
                    {
                        info =
                            new CategoryTargetInfo(
                                category.Id);


                        result.Add(
                            categoryId,
                            info);
                    }


                    switch (parameterKind)
                    {
                        case TargetParameterKind.Item:

                            info.HasItem =
                                true;

                            break;


                        case TargetParameterKind.Mate:

                            info.HasMate =
                                true;

                            break;


                        case TargetParameterKind.Size:

                            info.HasSize =
                                true;

                            break;
                    }
                }
            }


            return result;
        }


        // =========================================================
        // 실제 Model Element Gate
        // =========================================================

        private static bool IsEligibleModelElement(
            Element element)
        {
            if (element == null)
            {
                return false;
            }


            if (element is ElementType)
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
                element is CurveElement)
            {
                return false;
            }


            if (element is RevitLinkInstance
                ||
                element is ImportInstance)
            {
                return false;
            }


            Category? category =
                element.Category;


            if (category == null)
            {
                return false;
            }


            if (category.CategoryType !=
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
        // Helper / System Category
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
        // SK Parameter 종류
        // =========================================================

        private static TargetParameterKind
            GetTargetParameterKind(
                string parameterName)
        {
            if (string.Equals(
                    parameterName,
                    ItemParameterName,
                    StringComparison.Ordinal))
            {
                return
                    TargetParameterKind.Item;
            }


            if (string.Equals(
                    parameterName,
                    MateParameterName,
                    StringComparison.Ordinal))
            {
                return
                    TargetParameterKind.Mate;
            }


            if (string.Equals(
                    parameterName,
                    SizeParameterName,
                    StringComparison.Ordinal))
            {
                return
                    TargetParameterKind.Size;
            }


            return
                TargetParameterKind.None;
        }


        // =========================================================
        // Type Name Cache
        // =========================================================

        private static ParsedTypeName GetParsedTypeName(
            SyncRunContext context,
            Element element)
        {
            ElementId typeId =
                element.GetTypeId();


            if (typeId == null
                ||
                typeId ==
                ElementId.InvalidElementId)
            {
                return
                    ParsedTypeName.Empty;
            }


            long key =
                typeId.Value;


            if (context.TypeNameCache.TryGetValue(
                    key,
                    out ParsedTypeName? cached))
            {
                return cached;
            }


            string typeName =
                string.Empty;


            try
            {
                Element? typeElement =
                    context.Document.GetElement(
                        typeId);


                if (typeElement != null)
                {
                    typeName =
                        typeElement.Name
                        ??
                        string.Empty;
                }
            }
            catch
            {
            }


            ParsedTypeName parsed =
                ParseTypeName(
                    typeName);


            context.TypeNameCache[key] =
                parsed;


            return parsed;
        }


        // =========================================================
        // Type Name Parser
        //
        // ","가 없는 Type은 Empty가 아니라
        // HasComma = false 상태로 Cache해서
        // 같은 Type의 다음 Element도 즉시 Skip한다.
        // =========================================================

        private static ParsedTypeName ParseTypeName(
            string typeName)
        {
            if (string.IsNullOrWhiteSpace(
                    typeName))
            {
                return
                    ParsedTypeName.Empty;
            }


            bool hasComma =
                typeName.IndexOf(
                    ',') >= 0;


            if (!hasComma)
            {
                return
                    new ParsedTypeName(
                        string.Empty,
                        string.Empty,
                        false,
                        false);
            }


            string[] parts =
                typeName.Split(
                    ',',
                    StringSplitOptions.None);


            for (int i = 0;
                 i < parts.Length;
                 i++)
            {
                parts[i] =
                    parts[i].Trim();
            }


            string item =
                parts.Length >= 1
                    ?
                    parts[0]
                    :
                    string.Empty;


            string mate =
                parts.Length >= 2
                    ?
                    parts[1]
                    :
                    string.Empty;


            bool containsElbow =
                item.IndexOf(
                    "Elbow",
                    StringComparison.OrdinalIgnoreCase)
                >= 0;


            return
                new ParsedTypeName(
                    item,
                    mate,
                    true,
                    containsElbow);
        }


        // =========================================================
        // Dimensions Parameter
        //
        // Size / Angle 공통
        // =========================================================

        private static RevitParameter?
            FindDimensionsParameter(
                SyncRunContext context,
                Element element,
                string parameterName)
        {
            Category? category =
                element.Category;


            if (category == null)
            {
                return null;
            }


            ElementId typeId =
                element.GetTypeId();


            long typeIdValue =
                typeId != null
                &&
                typeId != ElementId.InvalidElementId
                    ?
                    typeId.Value
                    :
                    -1;


            DimensionParameterCacheKey cacheKey =
                new DimensionParameterCacheKey(
                    category.Id.Value,
                    typeIdValue,
                    element.GetType(),
                    parameterName);


            if (!context.DimensionParameterModeCache.TryGetValue(
                    cacheKey,
                    out DimensionParameterLookupMode mode))
            {
                mode =
                    DetectDimensionsParameterLookupMode(
                        element,
                        parameterName);


                context.DimensionParameterModeCache[
                    cacheKey] =
                    mode;
            }


            switch (mode)
            {
                case DimensionParameterLookupMode.LookupParameter:

                    RevitParameter? lookupParameter =
                        element.LookupParameter(
                            parameterName);


                    if (IsDimensionsParameter(
                            lookupParameter,
                            parameterName))
                    {
                        return lookupParameter;
                    }


                    return
                        FindDimensionsParameterBySearch(
                            element,
                            parameterName);


                case DimensionParameterLookupMode.SearchSameName:

                    return
                        FindDimensionsParameterBySearch(
                            element,
                            parameterName);


                default:

                    return null;
            }
        }


        private static DimensionParameterLookupMode
            DetectDimensionsParameterLookupMode(
                Element element,
                string parameterName)
        {
            try
            {
                RevitParameter? parameter =
                    element.LookupParameter(
                        parameterName);


                if (IsDimensionsParameter(
                        parameter,
                        parameterName))
                {
                    return
                        DimensionParameterLookupMode.LookupParameter;
                }


                RevitParameter? searched =
                    FindDimensionsParameterBySearch(
                        element,
                        parameterName);


                if (searched != null)
                {
                    return
                        DimensionParameterLookupMode.SearchSameName;
                }
            }
            catch
            {
            }


            return
                DimensionParameterLookupMode.None;
        }


        private static RevitParameter?
            FindDimensionsParameterBySearch(
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
                if (IsDimensionsParameter(
                        parameter,
                        parameterName))
                {
                    return parameter;
                }
            }


            return null;
        }


        private static bool IsDimensionsParameter(
            RevitParameter? parameter,
            string parameterName)
        {
            if (parameter == null)
            {
                return false;
            }


            Definition? definition =
                parameter.Definition;


            if (definition == null)
            {
                return false;
            }


            if (!string.Equals(
                    definition.Name,
                    parameterName,
                    StringComparison.Ordinal))
            {
                return false;
            }


            try
            {
                ForgeTypeId groupTypeId =
                    definition.GetGroupTypeId();


                return
                    groupTypeId.Equals(
                        GroupTypeId.Geometry);
            }
            catch
            {
                return false;
            }
        }


        // =========================================================
        // Value Read
        // =========================================================

        private static string ReadParameterDisplayValue(
            RevitParameter parameter)
        {
            if (parameter == null)
            {
                return string.Empty;
            }


            try
            {
                if (parameter.StorageType ==
                    StorageType.String)
                {
                    string? value =
                        parameter.AsString();


                    if (!string.IsNullOrWhiteSpace(
                            value))
                    {
                        return value;
                    }
                }


                string? displayValue =
                    parameter.AsValueString();


                if (!string.IsNullOrWhiteSpace(
                        displayValue))
                {
                    return displayValue;
                }


                string? fallback =
                    parameter.AsString();


                return
                    fallback
                    ??
                    string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }


        // =========================================================
        // SK_* Write
        //
        // 동일하면 Set하지 않음
        // =========================================================

        private static bool TrySetTextValue(
            RevitParameter parameter,
            string value)
        {
            if (parameter == null)
            {
                return false;
            }


            if (parameter.IsReadOnly)
            {
                return false;
            }


            if (parameter.StorageType !=
                StorageType.String)
            {
                return false;
            }


            if (string.IsNullOrWhiteSpace(
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


        // =========================================================
        // Result
        // =========================================================

        private static TypeNameParameterBatchResult
            CreateBatchResult(
                SyncRunContext context)
        {
            return
                new TypeNameParameterBatchResult
                {
                    TargetCategories =
                        context.TargetCategories.Count
                };
        }


        private static void FinishResult(
            SyncRunContext context,
            TypeNameParameterBatchResult result,
            Stopwatch stopwatch)
        {
            stopwatch.Stop();


            result.TypeCacheEntries =
                context.TypeNameCache.Count;


            result.ElapsedMilliseconds =
                stopwatch.ElapsedMilliseconds;
        }


        // =========================================================
        // Internal
        // =========================================================

        private sealed class SyncRunContext
        {
            public SyncRunContext(
                Document document)
            {
                Document =
                    document;
            }


            public Document Document
            {
                get;
            }


            public Dictionary<long, CategoryTargetInfo>
                TargetCategories
            {
                get;
                set;
            }
                =
                new Dictionary<long, CategoryTargetInfo>();


            public Dictionary<long, ParsedTypeName>
                TypeNameCache
            {
                get;
            }
                =
                new Dictionary<long, ParsedTypeName>();


            public Dictionary
                <
                    DimensionParameterCacheKey,
                    DimensionParameterLookupMode
                >
                DimensionParameterModeCache
            {
                get;
            }
                =
                new Dictionary
                    <
                        DimensionParameterCacheKey,
                        DimensionParameterLookupMode
                    >();
        }


        private sealed class CategoryTargetInfo
        {
            public CategoryTargetInfo(
                ElementId categoryId)
            {
                CategoryId =
                    categoryId;
            }


            public ElementId CategoryId
            {
                get;
            }


            public bool HasItem
            {
                get;
                set;
            }


            public bool HasMate
            {
                get;
                set;
            }


            public bool HasSize
            {
                get;
                set;
            }
        }


        private sealed class ParsedTypeName
        {
            public static ParsedTypeName Empty
            {
                get;
            }
                =
                new ParsedTypeName(
                    string.Empty,
                    string.Empty,
                    false,
                    false);


            public ParsedTypeName(
                string item,
                string mate,
                bool hasComma,
                bool containsElbow)
            {
                Item =
                    item;


                Mate =
                    mate;


                HasComma =
                    hasComma;


                ContainsElbow =
                    containsElbow;
            }


            public string Item
            {
                get;
            }


            public string Mate
            {
                get;
            }


            public bool HasComma
            {
                get;
            }


            public bool ContainsElbow
            {
                get;
            }
        }


        private readonly struct DimensionParameterCacheKey
            : IEquatable<DimensionParameterCacheKey>
        {
            public DimensionParameterCacheKey(
                long categoryId,
                long typeId,
                Type runtimeType,
                string parameterName)
            {
                CategoryId =
                    categoryId;


                TypeId =
                    typeId;


                RuntimeType =
                    runtimeType;


                ParameterName =
                    parameterName;
            }


            private long CategoryId
            {
                get;
            }


            private long TypeId
            {
                get;
            }


            private Type RuntimeType
            {
                get;
            }


            private string ParameterName
            {
                get;
            }


            public bool Equals(
                DimensionParameterCacheKey other)
            {
                return
                    CategoryId ==
                    other.CategoryId
                    &&
                    TypeId ==
                    other.TypeId
                    &&
                    RuntimeType ==
                    other.RuntimeType
                    &&
                    string.Equals(
                        ParameterName,
                        other.ParameterName,
                        StringComparison.Ordinal);
            }


            public override bool Equals(
                object? obj)
            {
                return
                    obj is DimensionParameterCacheKey other
                    &&
                    Equals(
                        other);
            }


            public override int GetHashCode()
            {
                return
                    HashCode.Combine(
                        CategoryId,
                        TypeId,
                        RuntimeType,
                        ParameterName);
            }
        }


        private enum DimensionParameterLookupMode
        {
            None,

            LookupParameter,

            SearchSameName
        }


        private enum TargetParameterKind
        {
            None,

            Item,

            Mate,

            Size
        }
    }
}