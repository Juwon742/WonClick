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
    public static class ExcelImportApplyService
    {
        private const string DataSheetName =
            "Parameter Data";

        private const string MetaSheetName =
            "_PARAMETER_META";

        private const string ElementMetadataTitle =
            "ELEMENT_METADATA";


        // =========================================================
        // APPLY
        //
        // Instance
        // → Element별 독립 적용
        //
        // Type
        // → 동일 Type + 동일 Parameter의
        //   모든 Export 행을 Excel에서 다시 검사
        // =========================================================

        public static List<ImportApplyResult> Apply(
            Document document,
            string filePath,
            IEnumerable<ImportChangeItem> changes)
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


            List<ImportChangeItem> changeItems =
                changes
                    ?.ToList()
                ?? new List<ImportChangeItem>();


            List<ImportApplyResult> results =
                new List<ImportApplyResult>();


            if (changeItems.Count == 0)
            {
                return results;
            }


            // =====================================================
            // EXCEL
            //
            // Type Parameter 안전 검사를 위해
            // 원본 Excel을 다시 읽습니다.
            // =====================================================

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


            List<ElementMetadata> elementMetadata =
                ReadElementMetadata(
                    metaWorksheet);


            if (elementMetadata.Count == 0)
            {
                throw new InvalidOperationException(
                    "Element Metadata가 없습니다.");
            }


            // =====================================================
            // TRANSACTION
            // =====================================================

            using Transaction transaction =
                new Transaction(
                    document,
                    "WonClick Parameter Import");


            TransactionStatus transactionStatus =
                transaction.Start();


            if (transactionStatus
                != TransactionStatus.Started)
            {
                throw new InvalidOperationException(
                    "Revit Import Transaction을 시작할 수 없습니다.");
            }


            try
            {
                // =================================================
                // INSTANCE
                // =================================================

                List<ImportChangeItem> instanceChanges =
                    changeItems
                        .Where(
                            item =>
                                !item.IsTypeParameter)
                        .ToList();


                foreach (ImportChangeItem change
                         in instanceChanges)
                {
                    ImportApplyResult result =
                        ApplyInstanceChange(
                            document,
                            change);


                    results.Add(
                        result);
                }


                // =================================================
                // TYPE
                // =================================================

                List<ImportChangeItem> typeChanges =
                    changeItems
                        .Where(
                            item =>
                                item.IsTypeParameter)
                        .ToList();


                IEnumerable<IGrouping<string, ImportChangeItem>>
                    typeGroups =
                        typeChanges
                            .GroupBy(
                                BuildTypeGroupKey,
                                StringComparer.Ordinal);


                foreach (IGrouping<string, ImportChangeItem>
                         typeGroup
                         in typeGroups)
                {
                    List<ImportApplyResult> typeResults =
                        ApplyTypeGroup(
                            document,
                            dataWorksheet,
                            elementMetadata,
                            typeGroup.ToList());


                    results.AddRange(
                        typeResults);
                }


                // =================================================
                // COMMIT
                // =================================================

                TransactionStatus commitStatus =
                    transaction.Commit();


                if (commitStatus
                    != TransactionStatus.Committed)
                {
                    throw new InvalidOperationException(
                        "Revit Import Transaction을 완료하지 못했습니다.");
                }
            }
            catch
            {
                if (transaction.GetStatus()
                    == TransactionStatus.Started)
                {
                    transaction.RollBack();
                }


                throw;
            }


            return results
                .OrderBy(
                    item =>
                        item.ExcelRow)
                .ThenBy(
                    item =>
                        item.ExcelColumn)
                .ToList();
        }


        // =========================================================
        // INSTANCE APPLY
        // =========================================================

        private static ImportApplyResult ApplyInstanceChange(
            Document document,
            ImportChangeItem change)
        {
            ImportApplyResult result =
                CreateResultFromChange(
                    change);


            Element? element =
                FindElement(
                    document,
                    change);


            if (element == null)
            {
                result.Status =
                    "Failed";


                result.Message =
                    "Revit Element를 찾을 수 없습니다.";


                return result;
            }


            Autodesk.Revit.DB.Parameter? parameter =
                FindParameter(
                    element,
                    change);


            if (parameter == null)
            {
                result.Status =
                    "Failed";


                result.Message =
                    "Revit Parameter를 찾을 수 없습니다.";


                return result;
            }


            if (parameter.IsReadOnly)
            {
                result.Status =
                    "Read Only";


                result.Message =
                    "읽기 전용 Parameter입니다.";


                return result;
            }


            // =====================================================
            // APPLY 직전 Revit 값 재검사
            // =====================================================

            string currentRevitValue =
                ParameterValueService
                    .GetValue(
                        document,
                        parameter);


            if (!string.Equals(
                currentRevitValue,
                change.RevitValue,
                StringComparison.Ordinal))
            {
                if (string.Equals(
                    currentRevitValue,
                    change.ExcelValue,
                    StringComparison.Ordinal))
                {
                    result.RevitValue =
                        currentRevitValue;


                    result.Status =
                        "Skipped";


                    result.Message =
                        "이미 Excel 값과 동일합니다.";


                    return result;
                }


                result.RevitValue =
                    currentRevitValue;


                result.Status =
                    "Conflict";


                result.Message =
                    "Analyze 이후 Revit 값이 변경되었습니다.";


                return result;
            }


            return ApplyParameterWithSubTransaction(
                document,
                parameter,
                change,
                result);
        }


        // =========================================================
        // TYPE GROUP
        // =========================================================

        private static List<ImportApplyResult> ApplyTypeGroup(
            Document document,
            IXLWorksheet dataWorksheet,
            List<ElementMetadata> elementMetadata,
            List<ImportChangeItem> changes)
        {
            List<ImportApplyResult> results =
                new List<ImportApplyResult>();


            if (changes.Count == 0)
            {
                return results;
            }


            List<ImportChangeItem> orderedChanges =
                changes
                    .OrderBy(
                        item =>
                            item.ExcelRow)
                    .ThenBy(
                        item =>
                            item.ExcelColumn)
                    .ToList();


            ImportChangeItem first =
                orderedChanges[0];


            // =====================================================
            // TYPE ID CHECK
            // =====================================================

            if (first.TypeIdValue <= 0)
            {
                return CreateStatusResults(
                    orderedChanges,
                    "Failed",
                    "TypeId가 유효하지 않습니다.");
            }


            // =====================================================
            // SAME TYPE의 모든 EXPORTED ROW
            // =====================================================

            List<ElementMetadata> sameTypeRows =
                elementMetadata
                    .Where(
                        item =>
                            item.TypeIdValue
                            == first.TypeIdValue)
                    .OrderBy(
                        item =>
                            item.ExcelRow)
                    .ToList();


            if (sameTypeRows.Count == 0)
            {
                return CreateStatusResults(
                    orderedChanges,
                    "Failed",
                    "동일 Type의 Export 행을 찾을 수 없습니다.");
            }


            // =====================================================
            // 모든 동일 Type 행의 Excel 값을 읽음
            // =====================================================

            List<string> allExcelValues =
                new List<string>();


            foreach (ElementMetadata metadata
                     in sameTypeRows)
            {
                string excelValue =
                    GetCellText(
                        dataWorksheet,
                        metadata.ExcelRow,
                        first.ExcelColumn);


                allExcelValues.Add(
                    excelValue);
            }


            // =====================================================
            // TYPE SAFETY RULE 1
            //
            // 동일 Type의 모든 Export 행이
            // 원래 값에서 변경되어 있어야 함.
            //
            // 하나라도 원래 값이면 Conflict.
            // =====================================================

            bool hasUnchangedRow =
                allExcelValues.Any(
                    value =>
                        string.Equals(
                            value,
                            first.RevitValue,
                            StringComparison.Ordinal));


            if (hasUnchangedRow)
            {
                return CreateStatusResults(
                    orderedChanges,
                    "Conflict",
                    "같은 Type의 일부 Excel 행이 변경되지 않았습니다.");
            }


            // =====================================================
            // TYPE SAFETY RULE 2
            //
            // 모든 행의 새 값이 동일해야 함.
            // =====================================================

            List<string> distinctNewValues =
                allExcelValues
                    .Distinct(
                        StringComparer.Ordinal)
                    .ToList();


            if (distinctNewValues.Count != 1)
            {
                return CreateStatusResults(
                    orderedChanges,
                    "Conflict",
                    "같은 Type의 Excel 값이 서로 다릅니다.");
            }


            string newValue =
                distinctNewValues[0];


            // =====================================================
            // CHANGE LIST와 실제 Excel 값도 일치해야 함
            // =====================================================

            bool changedRowsMismatch =
                orderedChanges.Any(
                    item =>
                        !string.Equals(
                            item.ExcelValue,
                            newValue,
                            StringComparison.Ordinal));


            if (changedRowsMismatch)
            {
                return CreateStatusResults(
                    orderedChanges,
                    "Conflict",
                    "Analyze 결과와 현재 Excel 값이 일치하지 않습니다.");
            }


            // =====================================================
            // TYPE ELEMENT
            // =====================================================

            Element? typeElement;


            try
            {
                typeElement =
                    document.GetElement(
                        new ElementId(
                            first.TypeIdValue));
            }
            catch
            {
                typeElement =
                    null;
            }


            if (typeElement == null)
            {
                return CreateStatusResults(
                    orderedChanges,
                    "Failed",
                    "Revit Type Element를 찾을 수 없습니다.");
            }


            // =====================================================
            // PARAMETER
            // =====================================================

            Autodesk.Revit.DB.Parameter? parameter =
                FindParameter(
                    typeElement,
                    first);


            if (parameter == null)
            {
                return CreateStatusResults(
                    orderedChanges,
                    "Failed",
                    "Revit Type Parameter를 찾을 수 없습니다.");
            }


            // =====================================================
            // READ ONLY
            // =====================================================

            if (parameter.IsReadOnly)
            {
                return CreateStatusResults(
                    orderedChanges,
                    "Read Only",
                    "읽기 전용 Type Parameter입니다.");
            }


            // =====================================================
            // APPLY 직전 현재 TYPE 값 재검사
            // =====================================================

            string currentRevitValue =
                ParameterValueService
                    .GetValue(
                        document,
                        parameter);


            if (!string.Equals(
                currentRevitValue,
                first.RevitValue,
                StringComparison.Ordinal))
            {
                if (string.Equals(
                    currentRevitValue,
                    newValue,
                    StringComparison.Ordinal))
                {
                    return CreateStatusResults(
                        orderedChanges,
                        "Skipped",
                        "이미 Excel 값과 동일합니다.");
                }


                List<ImportApplyResult> conflictResults =
                    CreateStatusResults(
                        orderedChanges,
                        "Conflict",
                        "Analyze 이후 Revit Type 값이 변경되었습니다.");


                foreach (ImportApplyResult result
                         in conflictResults)
                {
                    result.RevitValue =
                        currentRevitValue;
                }


                return conflictResults;
            }


            // =====================================================
            // TYPE PARAMETER SET
            //
            // 실제 Set은 딱 1회만 합니다.
            // =====================================================

            ImportApplyResult firstResult =
                CreateResultFromChange(
                    first);


            using SubTransaction subTransaction =
                new SubTransaction(
                    document);


            try
            {
                TransactionStatus startStatus =
                    subTransaction.Start();


                if (startStatus
                    != TransactionStatus.Started)
                {
                    return CreateStatusResults(
                        orderedChanges,
                        "Failed",
                        "Type SubTransaction을 시작할 수 없습니다.");
                }


                bool setSucceeded =
                    TrySetParameterValue(
                        parameter,
                        newValue,
                        out string errorMessage);


                if (!setSucceeded)
                {
                    subTransaction.RollBack();


                    return CreateStatusResults(
                        orderedChanges,
                        "Failed",
                        errorMessage);
                }


                TransactionStatus commitStatus =
                    subTransaction.Commit();


                if (commitStatus
                    != TransactionStatus.Committed)
                {
                    return CreateStatusResults(
                        orderedChanges,
                        "Failed",
                        "Type Parameter 변경을 Commit하지 못했습니다.");
                }


                // =================================================
                // APPLIED
                // =================================================

                string appliedValue =
                    ParameterValueService
                        .GetValue(
                            document,
                            parameter);


                firstResult.RevitValue =
                    appliedValue;


                firstResult.Status =
                    "Applied";


                firstResult.Message =
                    string.Empty;


                results.Add(
                    firstResult);


                // =================================================
                // SAME TYPE DUPLICATE ROWS
                //
                // Parameter 자체는 Type에 1번만 Set하므로
                // 나머지 변경 행은 Skipped 처리
                // =================================================

                for (int index = 1;
                     index < orderedChanges.Count;
                     index++)
                {
                    ImportApplyResult skippedResult =
                        CreateResultFromChange(
                            orderedChanges[index]);


                    skippedResult.RevitValue =
                        appliedValue;


                    skippedResult.Status =
                        "Skipped";


                    skippedResult.Message =
                        "같은 Type Parameter가 이미 적용되었습니다.";


                    results.Add(
                        skippedResult);
                }


                return results;
            }
            catch (Exception ex)
            {
                try
                {
                    if (subTransaction.GetStatus()
                        == TransactionStatus.Started)
                    {
                        subTransaction.RollBack();
                    }
                }
                catch
                {
                }


                return CreateStatusResults(
                    orderedChanges,
                    "Failed",
                    ex.Message);
            }
        }


        // =========================================================
        // PARAMETER APPLY
        // =========================================================

        private static ImportApplyResult
            ApplyParameterWithSubTransaction(
                Document document,
                Autodesk.Revit.DB.Parameter parameter,
                ImportChangeItem change,
                ImportApplyResult result)
        {
            using SubTransaction subTransaction =
                new SubTransaction(
                    document);


            try
            {
                TransactionStatus startStatus =
                    subTransaction.Start();


                if (startStatus
                    != TransactionStatus.Started)
                {
                    result.Status =
                        "Failed";


                    result.Message =
                        "SubTransaction을 시작할 수 없습니다.";


                    return result;
                }


                bool setSucceeded =
                    TrySetParameterValue(
                        parameter,
                        change.ExcelValue,
                        out string errorMessage);


                if (!setSucceeded)
                {
                    subTransaction.RollBack();


                    result.Status =
                        "Failed";


                    result.Message =
                        errorMessage;


                    return result;
                }


                TransactionStatus commitStatus =
                    subTransaction.Commit();


                if (commitStatus
                    != TransactionStatus.Committed)
                {
                    result.Status =
                        "Failed";


                    result.Message =
                        "Parameter 변경을 Commit하지 못했습니다.";


                    return result;
                }


                string appliedValue =
                    ParameterValueService
                        .GetValue(
                            document,
                            parameter);


                result.RevitValue =
                    appliedValue;


                result.Status =
                    "Applied";


                result.Message =
                    string.Empty;


                return result;
            }
            catch (Exception ex)
            {
                try
                {
                    if (subTransaction.GetStatus()
                        == TransactionStatus.Started)
                    {
                        subTransaction.RollBack();
                    }
                }
                catch
                {
                }


                result.Status =
                    "Failed";


                result.Message =
                    ex.Message;


                return result;
            }
        }


        // =========================================================
        // TYPE GROUP KEY
        // =========================================================

        private static string BuildTypeGroupKey(
            ImportChangeItem item)
        {
            string parameterIdentity;


            if (!string.IsNullOrWhiteSpace(
                item.SharedParameterGuid))
            {
                parameterIdentity =
                    "GUID:"
                    +
                    item.SharedParameterGuid;
            }
            else if (!string.IsNullOrWhiteSpace(
                item.Identifier))
            {
                parameterIdentity =
                    "ID:"
                    +
                    item.Identifier;
            }
            else if (item.ParameterDefinitionId != 0)
            {
                parameterIdentity =
                    "DEF:"
                    +
                    item.ParameterDefinitionId;
            }
            else
            {
                parameterIdentity =
                    "NAME:"
                    +
                    item.ParameterName;
            }


            return
                item.TypeIdValue
                +
                "|COL:"
                +
                item.ExcelColumn
                +
                "|"
                +
                parameterIdentity;
        }


        // =========================================================
        // STATUS RESULTS
        // =========================================================

        private static List<ImportApplyResult>
            CreateStatusResults(
                IEnumerable<ImportChangeItem> changes,
                string status,
                string message)
        {
            List<ImportApplyResult> results =
                new List<ImportApplyResult>();


            foreach (ImportChangeItem change
                     in changes)
            {
                ImportApplyResult result =
                    CreateResultFromChange(
                        change);


                result.Status =
                    status;


                result.Message =
                    message;


                results.Add(
                    result);
            }


            return results;
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
        // ELEMENT METADATA
        //
        // RangeUsed().Rows() 사용 금지
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


                TryParseLong(
                    GetCellTextByHeader(
                        metaWorksheet,
                        rowNumber,
                        headerMap,
                        "ElementId"),
                    out long elementIdValue);


                TryParseLong(
                    GetCellTextByHeader(
                        metaWorksheet,
                        rowNumber,
                        headerMap,
                        "TypeId"),
                    out long typeIdValue);


                result.Add(
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
                    });
            }


            return result;
        }


        // =========================================================
        // FIND TITLE ROW
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
        // ClosedXML의 RangeUsed().Rows()를 사용하지 않고
        // Cell(row, column) 좌표로만 읽습니다.
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
        // HEADER
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


        // =========================================================
        // PARSE
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
        // =========================================================

        private static Element? FindElement(
            Document document,
            ImportChangeItem change)
        {
            if (!string.IsNullOrWhiteSpace(
                change.ElementUniqueId))
            {
                try
                {
                    Element? element =
                        document.GetElement(
                            change.ElementUniqueId);


                    if (element != null)
                    {
                        return element;
                    }
                }
                catch
                {
                }
            }


            if (change.ElementIdValue > 0)
            {
                try
                {
                    return document.GetElement(
                        new ElementId(
                            change.ElementIdValue));
                }
                catch
                {
                    return null;
                }
            }


            return null;
        }


        // =========================================================
        // FIND PARAMETER
        // =========================================================

        private static Autodesk.Revit.DB.Parameter?
            FindParameter(
                Element ownerElement,
                ImportChangeItem change)
        {
            List<Autodesk.Revit.DB.Parameter> parameters =
                ownerElement
                    .Parameters
                    .Cast<Autodesk.Revit.DB.Parameter>()
                    .ToList();


            // =====================================================
            // 1. SHARED GUID
            // =====================================================

            if (!string.IsNullOrWhiteSpace(
                change.SharedParameterGuid)
                &&
                Guid.TryParse(
                    change.SharedParameterGuid,
                    out Guid sharedGuid))
            {
                foreach (Autodesk.Revit.DB.Parameter
                         parameter
                         in parameters)
                {
                    try
                    {
                        if (parameter.IsShared
                            &&
                            parameter.GUID
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


            // =====================================================
            // 2. IDENTIFIER
            // =====================================================

            if (!string.IsNullOrWhiteSpace(
                change.Identifier))
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
                        change.Identifier,
                        StringComparison.Ordinal))
                    {
                        return parameter;
                    }
                }
            }


            // =====================================================
            // 3. INTERNAL DEFINITION ID
            // =====================================================

            if (change.ParameterDefinitionId != 0)
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
                        == change.ParameterDefinitionId)
                    {
                        return parameter;
                    }
                }
            }


            // =====================================================
            // 4. NAME FALLBACK
            // =====================================================

            foreach (Autodesk.Revit.DB.Parameter
                     parameter
                     in parameters)
            {
                string name =
                    parameter.Definition?.Name
                    ?? string.Empty;


                if (string.Equals(
                    name,
                    change.ParameterName,
                    StringComparison.Ordinal))
                {
                    return parameter;
                }
            }


            return null;
        }


        // =========================================================
        // SET PARAMETER
        // =========================================================

        private static bool TrySetParameterValue(
            Autodesk.Revit.DB.Parameter parameter,
            string newValue,
            out string errorMessage)
        {
            errorMessage =
                string.Empty;


            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.String:
                        {
                            bool success =
                                parameter.Set(
                                    newValue
                                    ?? string.Empty);


                            if (!success)
                            {
                                errorMessage =
                                    "String 값을 설정하지 못했습니다.";
                            }


                            return success;
                        }


                    case StorageType.Integer:
                        {
                            return TrySetIntegerValue(
                                parameter,
                                newValue,
                                out errorMessage);
                        }


                    case StorageType.Double:
                        {
                            return TrySetDoubleValue(
                                parameter,
                                newValue,
                                out errorMessage);
                        }


                    case StorageType.ElementId:
                        {
                            return TrySetElementIdValue(
                                parameter,
                                newValue,
                                out errorMessage);
                        }


                    case StorageType.None:
                        {
                            errorMessage =
                                "StorageType.None Parameter는 수정할 수 없습니다.";


                            return false;
                        }


                    default:
                        {
                            errorMessage =
                                "지원하지 않는 Parameter StorageType입니다.";


                            return false;
                        }
                }
            }
            catch (Exception ex)
            {
                errorMessage =
                    ex.Message;


                return false;
            }
        }


        // =========================================================
        // INTEGER
        // =========================================================

        private static bool TrySetIntegerValue(
            Autodesk.Revit.DB.Parameter parameter,
            string newValue,
            out string errorMessage)
        {
            errorMessage =
                string.Empty;


            string source =
                newValue
                    ?.Trim()
                ?? string.Empty;


            try
            {
                if (parameter.SetValueString(
                    source))
                {
                    return true;
                }
            }
            catch
            {
            }


            if (TryParseBoolean(
                source,
                out int booleanValue))
            {
                try
                {
                    if (parameter.Set(
                        booleanValue))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }


            if (int.TryParse(
                source,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int integerValue))
            {
                try
                {
                    if (parameter.Set(
                        integerValue))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }


            if (int.TryParse(
                source,
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out integerValue))
            {
                try
                {
                    if (parameter.Set(
                        integerValue))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }


            errorMessage =
                "Integer 값으로 변환할 수 없습니다.";


            return false;
        }


        // =========================================================
        // DOUBLE
        // =========================================================

        private static bool TrySetDoubleValue(
            Autodesk.Revit.DB.Parameter parameter,
            string newValue,
            out string errorMessage)
        {
            errorMessage =
                string.Empty;


            string source =
                newValue
                    ?.Trim()
                ?? string.Empty;


            try
            {
                if (parameter.SetValueString(
                    source))
                {
                    return true;
                }
            }
            catch
            {
            }


            if (double.TryParse(
                source,
                NumberStyles.Float
                |
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out double doubleValue))
            {
                try
                {
                    if (parameter.Set(
                        doubleValue))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }


            if (double.TryParse(
                source,
                NumberStyles.Float
                |
                NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture,
                out doubleValue))
            {
                try
                {
                    if (parameter.Set(
                        doubleValue))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }


            errorMessage =
                "Double 값을 Revit 값으로 변환할 수 없습니다.";


            return false;
        }


        // =========================================================
        // ELEMENT ID
        // =========================================================

        private static bool TrySetElementIdValue(
            Autodesk.Revit.DB.Parameter parameter,
            string newValue,
            out string errorMessage)
        {
            errorMessage =
                string.Empty;


            string source =
                newValue
                    ?.Trim()
                ?? string.Empty;


            if (!long.TryParse(
                source,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long elementIdValue))
            {
                errorMessage =
                    "ElementId Parameter는 숫자 Element ID 값만 지원합니다.";


                return false;
            }


            try
            {
                ElementId elementId =
                    new ElementId(
                        elementIdValue);


                bool success =
                    parameter.Set(
                        elementId);


                if (!success)
                {
                    errorMessage =
                        "ElementId 값을 설정하지 못했습니다.";
                }


                return success;
            }
            catch (Exception ex)
            {
                errorMessage =
                    ex.Message;


                return false;
            }
        }


        // =========================================================
        // BOOLEAN
        // =========================================================

        private static bool TryParseBoolean(
            string source,
            out int value)
        {
            string normalized =
                source
                    .Trim()
                    .ToLowerInvariant();


            switch (normalized)
            {
                case "yes":
                case "true":
                case "1":
                case "예":
                case "네":
                case "참":

                    value =
                        1;


                    return true;


                case "no":
                case "false":
                case "0":
                case "아니오":
                case "아니요":
                case "거짓":

                    value =
                        0;


                    return true;


                default:

                    value =
                        0;


                    return false;
            }
        }


        // =========================================================
        // CREATE RESULT
        // =========================================================

        private static ImportApplyResult CreateResultFromChange(
            ImportChangeItem change)
        {
            return new ImportApplyResult
            {
                ExcelRow =
                    change.ExcelRow,

                ExcelColumn =
                    change.ExcelColumn,

                ElementIdValue =
                    change.ElementIdValue,

                ElementUniqueId =
                    change.ElementUniqueId,

                TypeIdValue =
                    change.TypeIdValue,

                CategoryName =
                    change.CategoryName,

                FamilyName =
                    change.FamilyName,

                TypeName =
                    change.TypeName,

                ParameterName =
                    change.ParameterName,

                IsTypeParameter =
                    change.IsTypeParameter,

                Identifier =
                    change.Identifier,

                SharedParameterGuid =
                    change.SharedParameterGuid,

                ParameterDefinitionId =
                    change.ParameterDefinitionId,

                RevitValue =
                    change.RevitValue,

                ExcelValue =
                    change.ExcelValue,

                Status =
                    string.Empty,

                Message =
                    string.Empty
            };
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