using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using WonClick.Parameter.Models;

namespace WonClick.Parameter.Services
{
    public static class ParameterExportScanService
    {
        public static List<ParameterItem>
            ScanSelectedParameters(
                Document document,
                IEnumerable<Element> visibleElements,
                IEnumerable<ParameterSelectionItem>
                    selectedParameters)
        {
            List<ParameterSelectionItem> selectedItems =
                selectedParameters
                    .Where(
                        item => item.IsSelected)
                    .ToList();


            if (selectedItems.Count == 0)
            {
                return new List<ParameterItem>();
            }


            // =====================================================
            // SELECTED PARAMETER MAP
            // =====================================================

            Dictionary<string, ParameterSelectionItem>
                selectedMap =
                    new Dictionary<
                        string,
                        ParameterSelectionItem>(
                            StringComparer.Ordinal);


            foreach (ParameterSelectionItem item
                     in selectedItems)
            {
                string key =
                    ParameterDiscoveryService.BuildKey(
                        item.IsTypeParameter,
                        item.Name,
                        item.RawGroupTypeId,
                        item.DataTypeName,
                        item.Identifier);


                if (!selectedMap.ContainsKey(
                    key))
                {
                    selectedMap.Add(
                        key,
                        item);
                }
            }


            List<ParameterItem> result =
                new List<ParameterItem>();


            // =====================================================
            // TYPE PARAMETER CACHE
            //
            // 동일 Type의 Parameter 값은 한 번만 읽습니다.
            // =====================================================

            Dictionary<long, List<TypeParameterValue>>
                typeParameterCache =
                    new Dictionary<
                        long,
                        List<TypeParameterValue>>();


            foreach (Element element
                     in visibleElements)
            {
                // =================================================
                // ELEMENT INFO
                // =================================================

                ElementId typeId =
                    element.GetTypeId();


                long typeIdValue =
                    typeId
                    == ElementId.InvalidElementId
                        ? -1
                        : typeId.Value;


                Element? typeElement =
                    typeId
                    == ElementId.InvalidElementId
                        ? null
                        : document.GetElement(
                            typeId);


                string categoryName =
                    element.Category?.Name
                    ?? string.Empty;


                string elementName =
                    element.Name
                    ?? string.Empty;


                string familyName =
                    GetFamilyName(
                        typeElement);


                string typeName =
                    GetTypeName(
                        typeElement);


                // =================================================
                // INSTANCE PARAMETERS
                // =================================================

                ScanInstanceParameters(
                    document,
                    element,
                    typeIdValue,
                    categoryName,
                    elementName,
                    familyName,
                    typeName,
                    selectedMap,
                    result);


                // =================================================
                // TYPE PARAMETERS
                // =================================================

                if (typeElement == null
                    ||
                    typeIdValue < 0)
                {
                    continue;
                }


                if (!typeParameterCache.TryGetValue(
                    typeIdValue,
                    out List<TypeParameterValue>?
                        cachedTypeValues))
                {
                    cachedTypeValues =
                        ScanTypeParameters(
                            document,
                            typeElement,
                            selectedMap);


                    typeParameterCache.Add(
                        typeIdValue,
                        cachedTypeValues);
                }


                // -------------------------------------------------
                // Type Parameter 값도 각 Instance 행에 반복합니다.
                //
                // 나중에 Excel Import 시 동일 Type의 모든 행을
                // 비교하기 위한 안전장치입니다.
                // -------------------------------------------------

                foreach (TypeParameterValue
                         cachedValue
                         in cachedTypeValues)
                {
                    ParameterItem item =
                        new ParameterItem
                        {
                            ElementIdValue =
                                element.Id.Value,

                            ElementUniqueId =
                                element.UniqueId
                                ?? string.Empty,

                            ElementName =
                                elementName,

                            CategoryName =
                                categoryName,

                            TypeIdValue =
                                typeIdValue,

                            FamilyName =
                                familyName,

                            TypeName =
                                typeName,

                            Name =
                                cachedValue.SelectionItem.Name,

                            GroupName =
                                cachedValue.SelectionItem.GroupName,

                            RawGroupTypeId =
                                cachedValue.SelectionItem.RawGroupTypeId,

                            DataTypeName =
                                cachedValue.SelectionItem.DataTypeName,

                            IsTypeParameter =
                                true,

                            IsReadOnly =
                                cachedValue.IsReadOnly,

                            Identifier =
                                cachedValue.SelectionItem.Identifier,

                            SharedParameterGuid =
                                cachedValue.SelectionItem
                                    .SharedParameterGuid,

                            ParameterDefinitionId =
                                cachedValue.SelectionItem
                                    .ParameterDefinitionId,

                            Value =
                                cachedValue.Value
                        };


                    result.Add(
                        item);
                }
            }


            return result;
        }


        // =========================================================
        // INSTANCE PARAMETERS
        // =========================================================

        private static void ScanInstanceParameters(
            Document document,
            Element element,
            long typeIdValue,
            string categoryName,
            string elementName,
            string familyName,
            string typeName,
            Dictionary<string, ParameterSelectionItem>
                selectedMap,
            List<ParameterItem> result)
        {
            foreach (Autodesk.Revit.DB.Parameter parameter
                     in element.Parameters)
            {
                Definition? definition =
                    parameter.Definition;


                if (definition == null)
                {
                    continue;
                }


                string name =
                    definition.Name
                    ?? string.Empty;


                if (string.IsNullOrWhiteSpace(
                    name))
                {
                    continue;
                }


                string identifier =
                    ParameterDiscoveryService
                        .GetIdentifier(
                            parameter);


                string rawGroupTypeId =
                    GetRawGroupTypeId(
                        definition);


                string dataTypeName =
                    GetDataTypeName(
                        definition);


                string key =
                    ParameterDiscoveryService.BuildKey(
                        false,
                        name,
                        rawGroupTypeId,
                        dataTypeName,
                        identifier);


                if (!selectedMap.TryGetValue(
                    key,
                    out ParameterSelectionItem?
                        selectionItem))
                {
                    continue;
                }


                ParameterItem item =
                    new ParameterItem
                    {
                        ElementIdValue =
                            element.Id.Value,

                        ElementUniqueId =
                            element.UniqueId
                            ?? string.Empty,

                        ElementName =
                            elementName,

                        CategoryName =
                            categoryName,

                        TypeIdValue =
                            typeIdValue,

                        FamilyName =
                            familyName,

                        TypeName =
                            typeName,

                        Name =
                            selectionItem.Name,

                        GroupName =
                            selectionItem.GroupName,

                        RawGroupTypeId =
                            selectionItem.RawGroupTypeId,

                        DataTypeName =
                            selectionItem.DataTypeName,

                        IsTypeParameter =
                            false,

                        IsReadOnly =
                            parameter.IsReadOnly,

                        Identifier =
                            selectionItem.Identifier,

                        SharedParameterGuid =
                            selectionItem.SharedParameterGuid,

                        ParameterDefinitionId =
                            selectionItem.ParameterDefinitionId,

                        Value =
                            ParameterValueService
                                .GetValue(
                                    document,
                                    parameter)
                    };


                result.Add(
                    item);
            }
        }


        // =========================================================
        // TYPE PARAMETERS
        // =========================================================

        private static List<TypeParameterValue>
            ScanTypeParameters(
                Document document,
                Element typeElement,
                Dictionary<string, ParameterSelectionItem>
                    selectedMap)
        {
            List<TypeParameterValue> result =
                new List<TypeParameterValue>();


            foreach (Autodesk.Revit.DB.Parameter parameter
                     in typeElement.Parameters)
            {
                Definition? definition =
                    parameter.Definition;


                if (definition == null)
                {
                    continue;
                }


                string name =
                    definition.Name
                    ?? string.Empty;


                if (string.IsNullOrWhiteSpace(
                    name))
                {
                    continue;
                }


                string identifier =
                    ParameterDiscoveryService
                        .GetIdentifier(
                            parameter);


                string rawGroupTypeId =
                    GetRawGroupTypeId(
                        definition);


                string dataTypeName =
                    GetDataTypeName(
                        definition);


                string key =
                    ParameterDiscoveryService.BuildKey(
                        true,
                        name,
                        rawGroupTypeId,
                        dataTypeName,
                        identifier);


                if (!selectedMap.TryGetValue(
                    key,
                    out ParameterSelectionItem?
                        selectionItem))
                {
                    continue;
                }


                TypeParameterValue item =
                    new TypeParameterValue
                    {
                        SelectionItem =
                            selectionItem,

                        IsReadOnly =
                            parameter.IsReadOnly,

                        Value =
                            ParameterValueService
                                .GetValue(
                                    document,
                                    parameter)
                    };


                result.Add(
                    item);
            }


            return result;
        }


        // =========================================================
        // FAMILY NAME
        // =========================================================

        private static string GetFamilyName(
            Element? typeElement)
        {
            if (typeElement == null)
            {
                return string.Empty;
            }


            try
            {
                if (typeElement
                    is ElementType elementType)
                {
                    string familyName =
                        elementType.FamilyName
                        ?? string.Empty;


                    if (!string.IsNullOrWhiteSpace(
                        familyName))
                    {
                        return familyName;
                    }
                }
            }
            catch
            {
                // 아래 fallback 사용
            }


            return string.Empty;
        }


        // =========================================================
        // TYPE NAME
        // =========================================================

        private static string GetTypeName(
            Element? typeElement)
        {
            if (typeElement == null)
            {
                return string.Empty;
            }


            try
            {
                return
                    typeElement.Name
                    ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }


        // =========================================================
        // RAW GROUP TYPE ID
        // =========================================================

        private static string GetRawGroupTypeId(
            Definition definition)
        {
            try
            {
                ForgeTypeId groupTypeId =
                    definition.GetGroupTypeId();


                return
                    groupTypeId?.TypeId
                    ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }


        // =========================================================
        // DATA TYPE
        // =========================================================

        private static string GetDataTypeName(
            Definition definition)
        {
            try
            {
                ForgeTypeId dataTypeId =
                    definition.GetDataType();


                if (dataTypeId == null
                    ||
                    string.IsNullOrWhiteSpace(
                        dataTypeId.TypeId))
                {
                    return string.Empty;
                }


                try
                {
                    return
                        LabelUtils.GetLabelForSpec(
                            dataTypeId);
                }
                catch
                {
                    return
                        dataTypeId.TypeId;
                }
            }
            catch
            {
                return string.Empty;
            }
        }


        // =========================================================
        // TYPE PARAMETER CACHE ITEM
        // =========================================================

        private sealed class TypeParameterValue
        {
            public ParameterSelectionItem
                SelectionItem
            { get; set; } =
                    new ParameterSelectionItem();


            public bool IsReadOnly { get; set; }


            public string Value { get; set; } =
                string.Empty;
        }
    }
}