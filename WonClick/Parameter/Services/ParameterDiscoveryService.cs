using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using WonClick.Parameter.Models;

namespace WonClick.Parameter.Services
{
    public static class ParameterDiscoveryService
    {
        public static List<ParameterSelectionItem>
            DiscoverParameters(
                Document document,
                IEnumerable<Element> elements)
        {
            Dictionary<string, ParameterSelectionItem>
                parameterMap =
                    new Dictionary<
                        string,
                        ParameterSelectionItem>(
                            StringComparer.Ordinal);


            // 동일 Type은 Type Parameter를 한 번만 조사
            HashSet<long> scannedTypeIds =
                new HashSet<long>();


            // 이미 처리한 Parameter Definition은
            // 비싼 메타데이터 계산을 반복하지 않도록 캐시
            Dictionary<string, ParameterMetadata>
                metadataCache =
                    new Dictionary<
                        string,
                        ParameterMetadata>(
                            StringComparer.Ordinal);


            foreach (Element element
                     in elements)
            {
                // =================================================
                // INSTANCE PARAMETERS
                // =================================================

                ScanParameters(
                    element,
                    false,
                    parameterMap,
                    metadataCache);


                // =================================================
                // TYPE PARAMETERS
                // =================================================

                ElementId typeId =
                    element.GetTypeId();


                if (typeId
                    == ElementId.InvalidElementId)
                {
                    continue;
                }


                long typeIdValue =
                    typeId.Value;


                if (!scannedTypeIds.Add(
                    typeIdValue))
                {
                    continue;
                }


                Element? typeElement =
                    document.GetElement(
                        typeId);


                if (typeElement == null)
                {
                    continue;
                }


                ScanParameters(
                    typeElement,
                    true,
                    parameterMap,
                    metadataCache);
            }


            return parameterMap
                .Values
                .OrderBy(
                    item => item.Name)
                .ThenBy(
                    item => item.IsTypeParameter)
                .ToList();
        }


        // =========================================================
        // PARAMETER SCAN
        // =========================================================

        private static void ScanParameters(
            Element ownerElement,
            bool isTypeParameter,
            Dictionary<string, ParameterSelectionItem>
                parameterMap,
            Dictionary<string, ParameterMetadata>
                metadataCache)
        {
            ParameterSet parameters =
                ownerElement.Parameters;


            foreach (Autodesk.Revit.DB.Parameter parameter
                     in parameters)
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


                // -------------------------------------------------
                // 먼저 가장 저렴한 식별 정보만 얻는다.
                // -------------------------------------------------

                string identifier =
                    GetIdentifier(
                        parameter);


                string preliminaryKey =
                    BuildPreliminaryKey(
                        isTypeParameter,
                        name,
                        identifier);


                // -------------------------------------------------
                // 이미 최종 Parameter 목록에 존재하는 경우
                //
                // Identifier가 있으면 Group / DataType /
                // LabelUtils 등의 비싼 작업을 하지 않는다.
                // -------------------------------------------------

                if (!string.IsNullOrWhiteSpace(
                    identifier))
                {
                    if (parameterMap.ContainsKey(
                        preliminaryKey))
                    {
                        continue;
                    }
                }


                // -------------------------------------------------
                // Metadata Cache
                // -------------------------------------------------

                string metadataCacheKey =
                    BuildMetadataCacheKey(
                        definition,
                        name,
                        identifier);


                if (!metadataCache.TryGetValue(
                    metadataCacheKey,
                    out ParameterMetadata? metadata))
                {
                    metadata =
                        CreateMetadata(
                            parameter,
                            definition,
                            name,
                            identifier);


                    metadataCache[
                        metadataCacheKey] =
                            metadata;
                }


                // -------------------------------------------------
                // 최종 Unique Key
                // -------------------------------------------------

                string key =
                    BuildKey(
                        isTypeParameter,
                        metadata.Name,
                        metadata.RawGroupTypeId,
                        metadata.DataTypeName,
                        metadata.Identifier);


                if (parameterMap.ContainsKey(
                    key))
                {
                    continue;
                }


                ParameterSelectionItem item =
                    new ParameterSelectionItem
                    {
                        Name =
                            metadata.Name,

                        GroupName =
                            metadata.GroupName,

                        RawGroupTypeId =
                            metadata.RawGroupTypeId,

                        DataTypeName =
                            metadata.DataTypeName,

                        IsTypeParameter =
                            isTypeParameter,

                        Identifier =
                            metadata.Identifier,

                        SharedParameterGuid =
                            metadata.SharedParameterGuid,

                        ParameterDefinitionId =
                            metadata.ParameterDefinitionId
                    };


                item.UiGroup =
                    ParameterGroupService
                        .GetUiGroup(item);


                parameterMap.Add(
                    key,
                    item);
            }
        }


        // =========================================================
        // METADATA
        // =========================================================

        private static ParameterMetadata
            CreateMetadata(
                Autodesk.Revit.DB.Parameter parameter,
                Definition definition,
                string name,
                string identifier)
        {
            return new ParameterMetadata
            {
                Name =
                    name,

                GroupName =
                    GetGroupName(
                        definition),

                RawGroupTypeId =
                    GetRawGroupTypeId(
                        definition),

                DataTypeName =
                    GetDataTypeName(
                        definition),

                Identifier =
                    identifier,

                SharedParameterGuid =
                    GetSharedParameterGuid(
                        parameter),

                ParameterDefinitionId =
                    GetParameterDefinitionId(
                        definition)
            };
        }


        // =========================================================
        // PRELIMINARY KEY
        // =========================================================

        private static string BuildPreliminaryKey(
            bool isTypeParameter,
            string name,
            string identifier)
        {
            string scope =
                isTypeParameter
                    ? "TYPE"
                    : "INSTANCE";


            if (!string.IsNullOrWhiteSpace(
                identifier))
            {
                return
                    scope
                    +
                    "|ID|"
                    +
                    identifier;
            }


            return
                scope
                +
                "|NAME|"
                +
                name;
        }


        // =========================================================
        // METADATA CACHE KEY
        // =========================================================

        private static string BuildMetadataCacheKey(
            Definition definition,
            string name,
            string identifier)
        {
            if (!string.IsNullOrWhiteSpace(
                identifier))
            {
                return
                    "ID|"
                    +
                    identifier;
            }


            if (definition is InternalDefinition
                internalDefinition)
            {
                return
                    "DEF|"
                    +
                    internalDefinition.Id.Value;
            }


            return
                "NAME|"
                +
                name;
        }


        // =========================================================
        // UNIQUE KEY
        // =========================================================

        public static string BuildKey(
            bool isTypeParameter,
            string name,
            string rawGroupTypeId,
            string dataTypeName,
            string identifier)
        {
            string scope =
                isTypeParameter
                    ? "TYPE"
                    : "INSTANCE";


            if (!string.IsNullOrWhiteSpace(
                identifier))
            {
                return
                    scope
                    +
                    "|ID|"
                    +
                    identifier;
            }


            return
                scope
                +
                "|FALLBACK|"
                +
                name
                +
                "|"
                +
                rawGroupTypeId
                +
                "|"
                +
                dataTypeName;
        }


        // =========================================================
        // IDENTIFIER
        // =========================================================

        public static string GetIdentifier(
            Autodesk.Revit.DB.Parameter parameter)
        {
            try
            {
                // Shared Parameter는 GUID가 가장 안정적
                if (parameter.IsShared)
                {
                    Guid guid =
                        parameter.GUID;


                    if (guid != Guid.Empty)
                    {
                        return
                            "SHARED:"
                            +
                            guid.ToString("D");
                    }
                }


                Definition? definition =
                    parameter.Definition;


                if (definition
                    is InternalDefinition
                    internalDefinition)
                {
                    return
                        "INTERNAL:"
                        +
                        internalDefinition.Id.Value;
                }
            }
            catch
            {
                // Identifier를 얻을 수 없는 경우
                // fallback key 사용
            }


            return string.Empty;
        }


        // =========================================================
        // SHARED PARAMETER GUID
        // =========================================================

        private static string
            GetSharedParameterGuid(
                Autodesk.Revit.DB.Parameter parameter)
        {
            try
            {
                if (!parameter.IsShared)
                {
                    return string.Empty;
                }


                Guid guid =
                    parameter.GUID;


                if (guid == Guid.Empty)
                {
                    return string.Empty;
                }


                return guid.ToString("D");
            }
            catch
            {
                return string.Empty;
            }
        }


        // =========================================================
        // PARAMETER DEFINITION ID
        // =========================================================

        private static long
            GetParameterDefinitionId(
                Definition definition)
        {
            try
            {
                if (definition
                    is InternalDefinition
                    internalDefinition)
                {
                    return
                        internalDefinition.Id.Value;
                }
            }
            catch
            {
                // 무시
            }


            return 0;
        }


        // =========================================================
        // RAW GROUP TYPE ID
        // =========================================================

        private static string
            GetRawGroupTypeId(
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
        // GROUP NAME
        // =========================================================

        private static string
            GetGroupName(
                Definition definition)
        {
            try
            {
                ForgeTypeId groupTypeId =
                    definition.GetGroupTypeId();


                if (groupTypeId == null
                    ||
                    string.IsNullOrWhiteSpace(
                        groupTypeId.TypeId))
                {
                    return string.Empty;
                }


                return
                    LabelUtils.GetLabelForGroup(
                        groupTypeId);
            }
            catch
            {
                return string.Empty;
            }
        }


        // =========================================================
        // DATA TYPE
        // =========================================================

        private static string
            GetDataTypeName(
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
        // INTERNAL METADATA CACHE MODEL
        // =========================================================

        private sealed class ParameterMetadata
        {
            public string Name { get; set; } =
                string.Empty;


            public string GroupName { get; set; } =
                string.Empty;


            public string RawGroupTypeId { get; set; } =
                string.Empty;


            public string DataTypeName { get; set; } =
                string.Empty;


            public string Identifier { get; set; } =
                string.Empty;


            public string SharedParameterGuid { get; set; } =
                string.Empty;


            public long ParameterDefinitionId { get; set; }
        }
    }
}