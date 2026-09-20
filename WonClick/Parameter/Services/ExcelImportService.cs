using Autodesk.Revit.DB;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using WonClick.Parameter.Models;

namespace WonClick.Parameter.Services
{
    public static class ExcelImportService
    {
        private const string DataSheetName =
            "Parameter Data";

        private const string MetaSheetName =
            "_PARAMETER_META";

        private const string ParameterMetadataTitle =
            "PARAMETER_METADATA";

        private const string ElementMetadataTitle =
            "ELEMENT_METADATA";


        // =========================================================
        // ANALYZE
        //
        // 이 메서드는 Revit 값을 수정하지 않습니다.
        //
        // Excel과 현재 Revit 값을 비교한 후
        // 변경된 항목만 ImportChangeItem으로 반환합니다.
        // =========================================================

        public static List<ImportChangeItem> Analyze(
            Document document,
            string filePath)
        {
            if (document == null)
            {
                throw new ArgumentNullException(
                    nameof(document));
            }


            if (string.IsNullOrWhiteSpace(
                filePath))
            {
                throw new ArgumentException(
                    "Excel 파일 경로가 없습니다.",
                    nameof(filePath));
            }


            if (!File.Exists(
                filePath))
            {
                throw new FileNotFoundException(
                    "Excel 파일을 찾을 수 없습니다.",
                    filePath);
            }


            using XLWorkbook workbook =
                new XLWorkbook(
                    filePath);


            IXLWorksheet? dataWorksheet =
                FindWorksheet(
                    workbook,
                    DataSheetName);


            if (dataWorksheet == null)
            {
                throw new InvalidOperationException(
                    $"'{DataSheetName}' 시트를 찾을 수 없습니다.");
            }


            IXLWorksheet? metaWorksheet =
                FindWorksheet(
                    workbook,
                    MetaSheetName);


            if (metaWorksheet == null)
            {
                throw new InvalidOperationException(
                    $"'{MetaSheetName}' 시트를 찾을 수 없습니다.");
            }


            // =====================================================
            // DOCUMENT CHECK
            // =====================================================

            string exportedDocumentName =
                ReadDocumentName(
                    metaWorksheet);


            if (string.IsNullOrWhiteSpace(
                exportedDocumentName))
            {
                throw new InvalidOperationException(
                    "Excel Metadata에서 DocumentName을 찾을 수 없습니다.");
            }


            if (!IsSameDocumentName(
                document.Title,
                exportedDocumentName))
            {
                throw new InvalidOperationException(
                    "현재 Revit 파일과 Excel Export 파일이 일치하지 않습니다."
                    +
                    "\n\n"
                    +
                    $"현재 RVT : {document.Title}"
                    +
                    "\n"
                    +
                    $"Excel RVT : {exportedDocumentName}");
            }


            // =====================================================
            // METADATA
            // =====================================================

            List<ParameterMetadata> parameterMetadata =
                ReadParameterMetadata(
                    metaWorksheet);


            List<ElementMetadata> elementMetadata =
                ReadElementMetadata(
                    metaWorksheet);


            if (parameterMetadata.Count == 0)
            {
                throw new InvalidOperationException(
                    "Parameter Metadata가 없습니다.");
            }


            if (elementMetadata.Count == 0)
            {
                throw new InvalidOperationException(
                    "Element Metadata가 없습니다.");
            }


            // =====================================================
            // ANALYZE
            // =====================================================

            List<ImportChangeItem> changes =
                new List<ImportChangeItem>();


            foreach (ElementMetadata elementMeta
                     in elementMetadata)
            {
                Element? instanceElement =
                    FindElement(
                        document,
                        elementMeta);


                if (instanceElement == null)
                {
                    continue;
                }


                foreach (ParameterMetadata parameterMeta
                         in parameterMetadata)
                {
                    string excelValue =
                        GetCellText(
                            dataWorksheet,
                            elementMeta.ExcelRow,
                            parameterMeta.ExcelColumn);


                    Element? ownerElement;


                    if (parameterMeta.IsTypeParameter)
                    {
                        ownerElement =
                            GetTypeElement(
                                document,
                                instanceElement,
                                elementMeta.TypeIdValue);
                    }
                    else
                    {
                        ownerElement =
                            instanceElement;
                    }


                    if (ownerElement == null)
                    {
                        continue;
                    }


                    Autodesk.Revit.DB.Parameter? parameter =
                        FindParameter(
                            ownerElement,
                            parameterMeta);


                    if (parameter == null)
                    {
                        continue;
                    }


                    string revitValue =
                        ParameterValueService
                            .GetValue(
                                document,
                                parameter);


                    // -------------------------------------------------
                    // 정확한 문자열 비교
                    //
                    // Analyze 단계에서는 임의 Trim이나
                    // 대소문자 보정을 하지 않습니다.
                    // -------------------------------------------------

                    if (string.Equals(
                        revitValue,
                        excelValue,
                        StringComparison.Ordinal))
                    {
                        continue;
                    }


                    ImportChangeItem change =
                        new ImportChangeItem
                        {
                            ExcelRow =
                                elementMeta.ExcelRow,

                            ExcelColumn =
                                parameterMeta.ExcelColumn,

                            ElementIdValue =
                                instanceElement.Id.Value,

                            ElementUniqueId =
                                instanceElement.UniqueId
                                ?? string.Empty,

                            TypeIdValue =
                                elementMeta.TypeIdValue,

                            RvtFileName =
                                document.Title,

                            CategoryName =
                                instanceElement.Category?.Name
                                ?? string.Empty,

                            FamilyName =
                                GetFamilyName(
                                    document,
                                    instanceElement),

                            TypeName =
                                GetTypeName(
                                    document,
                                    instanceElement),

                            ParameterName =
                                parameterMeta.ParameterName,

                            UiGroup =
                                parameterMeta.UiGroup,

                            DataTypeName =
                                parameterMeta.DataTypeName,

                            IsTypeParameter =
                                parameterMeta.IsTypeParameter,

                            Identifier =
                                parameterMeta.Identifier,

                            SharedParameterGuid =
                                parameterMeta.SharedParameterGuid,

                            ParameterDefinitionId =
                                parameterMeta.ParameterDefinitionId,

                            RevitValue =
                                revitValue,

                            ExcelValue =
                                excelValue,

                            IsReadOnly =
                                parameter.IsReadOnly,

                            Status =
                                "Change",

                            Message =
                                string.Empty
                        };


                    changes.Add(
                        change);
                }
            }


            return changes
                .OrderBy(
                    item =>
                        item.ExcelRow)
                .ThenBy(
                    item =>
                        item.ExcelColumn)
                .ToList();
        }


        // =========================================================
        // FIND WORKSHEET
        // =========================================================

        private static IXLWorksheet? FindWorksheet(
            XLWorkbook workbook,
            string worksheetName)
        {
            foreach (IXLWorksheet worksheet
                     in workbook.Worksheets)
            {
                if (string.Equals(
                    worksheet.Name,
                    worksheetName,
                    StringComparison.Ordinal))
                {
                    return worksheet;
                }
            }


            return null;
        }


        // =========================================================
        // DOCUMENT NAME
        // =========================================================

        private static string ReadDocumentName(
            IXLWorksheet metaWorksheet)
        {
            IXLRange? usedRange =
                metaWorksheet.RangeUsed();


            if (usedRange == null)
            {
                return string.Empty;
            }


            int firstRow =
                usedRange
                    .RangeAddress
                    .FirstAddress
                    .RowNumber;


            int lastRow =
                usedRange
                    .RangeAddress
                    .LastAddress
                    .RowNumber;


            for (int rowNumber = firstRow;
                 rowNumber <= lastRow;
                 rowNumber++)
            {
                string key =
                    GetCellText(
                        metaWorksheet,
                        rowNumber,
                        1);


                if (!string.Equals(
                    key,
                    "DocumentName",
                    StringComparison.Ordinal))
                {
                    continue;
                }


                return GetCellText(
                    metaWorksheet,
                    rowNumber,
                    2);
            }


            return string.Empty;
        }


        // =========================================================
        // DOCUMENT NAME COMPARE
        // =========================================================

        private static bool IsSameDocumentName(
            string currentDocumentName,
            string exportedDocumentName)
        {
            string current =
                NormalizeDocumentName(
                    currentDocumentName);


            string exported =
                NormalizeDocumentName(
                    exportedDocumentName);


            return string.Equals(
                current,
                exported,
                StringComparison.OrdinalIgnoreCase);
        }


        private static string NormalizeDocumentName(
            string source)
        {
            if (string.IsNullOrWhiteSpace(
                source))
            {
                return string.Empty;
            }


            string result =
                source.Trim();


            try
            {
                result =
                    Path.GetFileNameWithoutExtension(
                        result);
            }
            catch
            {
                // 원본 사용
            }


            return result.Trim();
        }


        // =========================================================
        // PARAMETER METADATA
        // =========================================================

        private static List<ParameterMetadata>
            ReadParameterMetadata(
                IXLWorksheet metaWorksheet)
        {
            int titleRow =
                FindTitleRow(
                    metaWorksheet,
                    ParameterMetadataTitle);


            if (titleRow <= 0)
            {
                throw new InvalidOperationException(
                    "PARAMETER_METADATA 영역을 찾을 수 없습니다.");
            }


            int headerRow =
                titleRow + 1;


            Dictionary<string, int> headerMap =
                BuildHeaderMap(
                    metaWorksheet,
                    headerRow);


            RequireHeader(
                headerMap,
                "ExcelColumn");


            RequireHeader(
                headerMap,
                "ParameterName");


            RequireHeader(
                headerMap,
                "Scope");


            RequireHeader(
                headerMap,
                "Identifier");


            int elementTitleRow =
                FindTitleRow(
                    metaWorksheet,
                    ElementMetadataTitle);


            if (elementTitleRow <= 0)
            {
                throw new InvalidOperationException(
                    "ELEMENT_METADATA 영역을 찾을 수 없습니다.");
            }


            int firstDataRow =
                headerRow + 1;


            int lastDataRow =
                elementTitleRow - 1;


            List<ParameterMetadata> result =
                new List<ParameterMetadata>();


            for (int rowNumber = firstDataRow;
                 rowNumber <= lastDataRow;
                 rowNumber++)
            {
                string excelColumnText =
                    GetCellTextByHeader(
                        metaWorksheet,
                        rowNumber,
                        headerMap,
                        "ExcelColumn");


                if (string.IsNullOrWhiteSpace(
                    excelColumnText))
                {
                    continue;
                }


                if (!TryParseInt(
                    excelColumnText,
                    out int excelColumn))
                {
                    continue;
                }


                string parameterName =
                    GetCellTextByHeader(
                        metaWorksheet,
                        rowNumber,
                        headerMap,
                        "ParameterName");


                if (string.IsNullOrWhiteSpace(
                    parameterName))
                {
                    continue;
                }


                string scope =
                    GetCellTextByHeader(
                        metaWorksheet,
                        rowNumber,
                        headerMap,
                        "Scope");


                ParameterMetadata item =
                    new ParameterMetadata
                    {
                        ExcelColumn =
                            excelColumn,

                        ParameterName =
                            parameterName,

                        UiGroup =
                            GetOptionalCellTextByHeader(
                                metaWorksheet,
                                rowNumber,
                                headerMap,
                                "UiGroup"),

                        IsTypeParameter =
                            string.Equals(
                                scope,
                                "Type",
                                StringComparison.OrdinalIgnoreCase),

                        Identifier =
                            GetCellTextByHeader(
                                metaWorksheet,
                                rowNumber,
                                headerMap,
                                "Identifier"),

                        SharedParameterGuid =
                            GetOptionalCellTextByHeader(
                                metaWorksheet,
                                rowNumber,
                                headerMap,
                                "SharedParameterGuid"),

                        ParameterDefinitionId =
                            GetOptionalLongByHeader(
                                metaWorksheet,
                                rowNumber,
                                headerMap,
                                "ParameterDefinitionId"),

                        RawGroupTypeId =
                            GetOptionalCellTextByHeader(
                                metaWorksheet,
                                rowNumber,
                                headerMap,
                                "RawGroupTypeId"),

                        DataTypeName =
                            GetOptionalCellTextByHeader(
                                metaWorksheet,
                                rowNumber,
                                headerMap,
                                "DataType")
                    };


                result.Add(
                    item);
            }


            return result;
        }


        // =========================================================
        // ELEMENT METADATA
        // =========================================================

        private static List<ElementMetadata>
            ReadElementMetadata(
                IXLWorksheet metaWorksheet)
        {
            int titleRow =
                FindTitleRow(
                    metaWorksheet,
                    ElementMetadataTitle);


            if (titleRow <= 0)
            {
                throw new InvalidOperationException(
                    "ELEMENT_METADATA 영역을 찾을 수 없습니다.");
            }


            int headerRow =
                titleRow + 1;


            Dictionary<string, int> headerMap =
                BuildHeaderMap(
                    metaWorksheet,
                    headerRow);


            RequireHeader(
                headerMap,
                "ExcelRow");


            RequireHeader(
                headerMap,
                "ElementId");


            RequireHeader(
                headerMap,
                "UniqueId");


            RequireHeader(
                headerMap,
                "TypeId");


            IXLRange? usedRange =
                metaWorksheet.RangeUsed();


            if (usedRange == null)
            {
                return new List<ElementMetadata>();
            }


            int lastRow =
                usedRange
                    .RangeAddress
                    .LastAddress
                    .RowNumber;


            List<ElementMetadata> result =
                new List<ElementMetadata>();


            for (int rowNumber = headerRow + 1;
                 rowNumber <= lastRow;
                 rowNumber++)
            {
                string excelRowText =
                    GetCellTextByHeader(
                        metaWorksheet,
                        rowNumber,
                        headerMap,
                        "ExcelRow");


                if (!TryParseInt(
                    excelRowText,
                    out int excelRow))
                {
                    continue;
                }


                string elementIdText =
                    GetCellTextByHeader(
                        metaWorksheet,
                        rowNumber,
                        headerMap,
                        "ElementId");


                TryParseLong(
                    elementIdText,
                    out long elementIdValue);


                string typeIdText =
                    GetCellTextByHeader(
                        metaWorksheet,
                        rowNumber,
                        headerMap,
                        "TypeId");


                TryParseLong(
                    typeIdText,
                    out long typeIdValue);


                ElementMetadata item =
                    new ElementMetadata
                    {
                        ExcelRow =
                            excelRow,

                        ElementIdValue =
                            elementIdValue,

                        ElementUniqueId =
                            GetCellTextByHeader(
                                metaWorksheet,
                                rowNumber,
                                headerMap,
                                "UniqueId"),

                        TypeIdValue =
                            typeIdValue
                    };


                result.Add(
                    item);
            }


            return result;
        }


        // =========================================================
        // FIND TITLE ROW
        //
        // RangeUsed().Rows()는 사용하지 않습니다.
        // 셀 좌표를 직접 순회합니다.
        // =========================================================

        private static int FindTitleRow(
            IXLWorksheet worksheet,
            string title)
        {
            IXLRange? usedRange =
                worksheet.RangeUsed();


            if (usedRange == null)
            {
                return -1;
            }


            int firstRow =
                usedRange
                    .RangeAddress
                    .FirstAddress
                    .RowNumber;


            int lastRow =
                usedRange
                    .RangeAddress
                    .LastAddress
                    .RowNumber;


            for (int rowNumber = firstRow;
                 rowNumber <= lastRow;
                 rowNumber++)
            {
                string value =
                    GetCellText(
                        worksheet,
                        rowNumber,
                        1);


                if (string.Equals(
                    value,
                    title,
                    StringComparison.Ordinal))
                {
                    return rowNumber;
                }
            }


            return -1;
        }


        // =========================================================
        // HEADER MAP
        //
        // RangeUsed().Rows()를 사용하지 않습니다.
        // =========================================================

        private static Dictionary<string, int>
            BuildHeaderMap(
                IXLWorksheet worksheet,
                int headerRow)
        {
            Dictionary<string, int> result =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);


            IXLRange? usedRange =
                worksheet.RangeUsed();


            if (usedRange == null)
            {
                return result;
            }


            int firstColumn =
                usedRange
                    .RangeAddress
                    .FirstAddress
                    .ColumnNumber;


            int lastColumn =
                usedRange
                    .RangeAddress
                    .LastAddress
                    .ColumnNumber;


            for (int columnNumber = firstColumn;
                 columnNumber <= lastColumn;
                 columnNumber++)
            {
                string header =
                    GetCellText(
                        worksheet,
                        headerRow,
                        columnNumber);


                if (string.IsNullOrWhiteSpace(
                    header))
                {
                    continue;
                }


                if (!result.ContainsKey(
                    header))
                {
                    result.Add(
                        header,
                        columnNumber);
                }
            }


            return result;
        }


        // =========================================================
        // HEADER VALIDATION
        // =========================================================

        private static void RequireHeader(
            Dictionary<string, int> headerMap,
            string headerName)
        {
            if (!headerMap.ContainsKey(
                headerName))
            {
                throw new InvalidOperationException(
                    $"Excel Metadata에 '{headerName}' 열이 없습니다.");
            }
        }


        // =========================================================
        // CELL
        // =========================================================

        private static string GetCellText(
            IXLWorksheet worksheet,
            int rowNumber,
            int columnNumber)
        {
            if (rowNumber <= 0
                ||
                columnNumber <= 0)
            {
                return string.Empty;
            }


            IXLCell cell =
                worksheet.Cell(
                    rowNumber,
                    columnNumber);


            try
            {
                return
                    cell.GetString()
                    ?? string.Empty;
            }
            catch
            {
                try
                {
                    return
                        cell.GetFormattedString()
                        ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }


        private static string GetCellTextByHeader(
            IXLWorksheet worksheet,
            int rowNumber,
            Dictionary<string, int> headerMap,
            string headerName)
        {
            if (!headerMap.TryGetValue(
                headerName,
                out int columnNumber))
            {
                return string.Empty;
            }


            return GetCellText(
                worksheet,
                rowNumber,
                columnNumber);
        }


        private static string GetOptionalCellTextByHeader(
            IXLWorksheet worksheet,
            int rowNumber,
            Dictionary<string, int> headerMap,
            string headerName)
        {
            if (!headerMap.TryGetValue(
                headerName,
                out int columnNumber))
            {
                return string.Empty;
            }


            return GetCellText(
                worksheet,
                rowNumber,
                columnNumber);
        }


        private static long GetOptionalLongByHeader(
            IXLWorksheet worksheet,
            int rowNumber,
            Dictionary<string, int> headerMap,
            string headerName)
        {
            string value =
                GetOptionalCellTextByHeader(
                    worksheet,
                    rowNumber,
                    headerMap,
                    headerName);


            if (TryParseLong(
                value,
                out long result))
            {
                return result;
            }


            return 0;
        }


        // =========================================================
        // NUMBER PARSE
        // =========================================================

        private static bool TryParseInt(
            string source,
            out int value)
        {
            if (int.TryParse(
                source,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value))
            {
                return true;
            }


            if (double.TryParse(
                source,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double doubleValue))
            {
                value =
                    Convert.ToInt32(
                        doubleValue);


                return true;
            }


            value =
                0;


            return false;
        }


        private static bool TryParseLong(
            string source,
            out long value)
        {
            if (long.TryParse(
                source,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value))
            {
                return true;
            }


            if (double.TryParse(
                source,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double doubleValue))
            {
                value =
                    Convert.ToInt64(
                        doubleValue);


                return true;
            }


            value =
                0;


            return false;
        }


        // =========================================================
        // FIND ELEMENT
        //
        // 1. UniqueId
        // 2. ElementId
        // =========================================================

        private static Element? FindElement(
            Document document,
            ElementMetadata metadata)
        {
            if (!string.IsNullOrWhiteSpace(
                metadata.ElementUniqueId))
            {
                try
                {
                    Element? element =
                        document.GetElement(
                            metadata.ElementUniqueId);


                    if (element != null)
                    {
                        return element;
                    }
                }
                catch
                {
                    // ElementId fallback
                }
            }


            if (metadata.ElementIdValue > 0)
            {
                try
                {
                    ElementId elementId =
                        new ElementId(
                            metadata.ElementIdValue);


                    return document.GetElement(
                        elementId);
                }
                catch
                {
                    return null;
                }
            }


            return null;
        }


        // =========================================================
        // GET TYPE ELEMENT
        // =========================================================

        private static Element? GetTypeElement(
            Document document,
            Element instanceElement,
            long metadataTypeIdValue)
        {
            if (metadataTypeIdValue > 0)
            {
                try
                {
                    Element? metadataType =
                        document.GetElement(
                            new ElementId(
                                metadataTypeIdValue));


                    if (metadataType != null)
                    {
                        return metadataType;
                    }
                }
                catch
                {
                    // 아래 현재 TypeId fallback
                }
            }


            ElementId currentTypeId =
                instanceElement.GetTypeId();


            if (currentTypeId
                == ElementId.InvalidElementId)
            {
                return null;
            }


            return document.GetElement(
                currentTypeId);
        }


        // =========================================================
        // FIND PARAMETER
        //
        // 1. SharedParameterGuid
        // 2. Identifier
        // 3. InternalDefinition Id
        // 4. Name fallback
        // =========================================================

        private static Autodesk.Revit.DB.Parameter?
            FindParameter(
                Element ownerElement,
                ParameterMetadata metadata)
        {
            List<Autodesk.Revit.DB.Parameter>
                parameters =
                    ownerElement.Parameters
                        .Cast<Autodesk.Revit.DB.Parameter>()
                        .ToList();


            // -----------------------------------------------------
            // 1. SHARED GUID
            // -----------------------------------------------------

            if (!string.IsNullOrWhiteSpace(
                metadata.SharedParameterGuid)
                &&
                Guid.TryParse(
                    metadata.SharedParameterGuid,
                    out Guid sharedGuid))
            {
                foreach (Autodesk.Revit.DB.Parameter
                         parameter
                         in parameters)
                {
                    try
                    {
                        if (!parameter.IsShared)
                        {
                            continue;
                        }


                        if (parameter.GUID
                            == sharedGuid)
                        {
                            return parameter;
                        }
                    }
                    catch
                    {
                    }
                }
            }


            // -----------------------------------------------------
            // 2. IDENTIFIER
            // -----------------------------------------------------

            if (!string.IsNullOrWhiteSpace(
                metadata.Identifier))
            {
                foreach (Autodesk.Revit.DB.Parameter
                         parameter
                         in parameters)
                {
                    string identifier =
                        ParameterDiscoveryService
                            .GetIdentifier(
                                parameter);


                    if (string.Equals(
                        identifier,
                        metadata.Identifier,
                        StringComparison.Ordinal))
                    {
                        return parameter;
                    }
                }
            }


            // -----------------------------------------------------
            // 3. INTERNAL DEFINITION ID
            // -----------------------------------------------------

            if (metadata.ParameterDefinitionId != 0)
            {
                foreach (Autodesk.Revit.DB.Parameter
                         parameter
                         in parameters)
                {
                    Definition? definition =
                        parameter.Definition;


                    if (definition
                        is not InternalDefinition
                            internalDefinition)
                    {
                        continue;
                    }


                    if (internalDefinition.Id.Value
                        == metadata.ParameterDefinitionId)
                    {
                        return parameter;
                    }
                }
            }


            // -----------------------------------------------------
            // 4. NAME FALLBACK
            // -----------------------------------------------------

            foreach (Autodesk.Revit.DB.Parameter
                     parameter
                     in parameters)
            {
                Definition? definition =
                    parameter.Definition;


                string name =
                    definition?.Name
                    ?? string.Empty;


                if (string.Equals(
                    name,
                    metadata.ParameterName,
                    StringComparison.Ordinal))
                {
                    return parameter;
                }
            }


            return null;
        }


        // =========================================================
        // FAMILY NAME
        // =========================================================

        private static string GetFamilyName(
            Document document,
            Element instanceElement)
        {
            ElementId typeId =
                instanceElement.GetTypeId();


            if (typeId
                == ElementId.InvalidElementId)
            {
                return string.Empty;
            }


            Element? typeElement =
                document.GetElement(
                    typeId);


            if (typeElement
                is ElementType elementType)
            {
                try
                {
                    return
                        elementType.FamilyName
                        ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }


            return string.Empty;
        }


        // =========================================================
        // TYPE NAME
        // =========================================================

        private static string GetTypeName(
            Document document,
            Element instanceElement)
        {
            ElementId typeId =
                instanceElement.GetTypeId();


            if (typeId
                == ElementId.InvalidElementId)
            {
                return string.Empty;
            }


            Element? typeElement =
                document.GetElement(
                    typeId);


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
        // INTERNAL PARAMETER METADATA
        // =========================================================

        private sealed class ParameterMetadata
        {
            public int ExcelColumn { get; set; }


            public string ParameterName { get; set; } =
                string.Empty;


            public string UiGroup { get; set; } =
                string.Empty;


            public bool IsTypeParameter { get; set; }


            public string Identifier { get; set; } =
                string.Empty;


            public string SharedParameterGuid { get; set; } =
                string.Empty;


            public long ParameterDefinitionId { get; set; }


            public string RawGroupTypeId { get; set; } =
                string.Empty;


            public string DataTypeName { get; set; } =
                string.Empty;
        }


        // =========================================================
        // INTERNAL ELEMENT METADATA
        // =========================================================

        private sealed class ElementMetadata
        {
            public int ExcelRow { get; set; }


            public long ElementIdValue { get; set; }


            public string ElementUniqueId { get; set; } =
                string.Empty;


            public long TypeIdValue { get; set; }
        }
    }
}