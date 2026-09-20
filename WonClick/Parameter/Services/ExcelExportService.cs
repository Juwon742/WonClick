using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WonClick.Parameter.Models;

namespace WonClick.Parameter.Services
{
    public static class ExcelExportService
    {
        private const string DataSheetName =
            "Parameter Data";

        private const string MetaSheetName =
            "_PARAMETER_META";


        // =========================================================
        // EXPORT
        // =========================================================

        public static void Export(
            string filePath,
            string documentName,
            string viewName,
            IEnumerable<ParameterSelectionItem>
                selectedParameters,
            IEnumerable<ParameterItem>
                parameterItems)
        {
            if (string.IsNullOrWhiteSpace(
                filePath))
            {
                throw new ArgumentException(
                    "Excel 저장 경로가 없습니다.",
                    nameof(filePath));
            }


            string? directory =
                Path.GetDirectoryName(
                    filePath);


            if (!string.IsNullOrWhiteSpace(
                directory)
                &&
                !Directory.Exists(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }


            List<ParameterSelectionItem>
                selectedItems =
                    selectedParameters
                        .Where(
                            item =>
                                item.IsSelected)
                        .ToList();


            if (selectedItems.Count == 0)
            {
                throw new InvalidOperationException(
                    "선택된 Parameter가 없습니다.");
            }


            List<ParameterItem> scanItems =
                parameterItems
                    .ToList();


            // =====================================================
            // SELECTED PARAMETER COLUMN INFO
            // =====================================================

            List<ExportParameterColumn>
                parameterColumns =
                    BuildParameterColumns(
                        selectedItems);


            Dictionary<string, ExportParameterColumn>
                parameterColumnMap =
                    parameterColumns
                        .ToDictionary(
                            item =>
                                item.Key,
                            item =>
                                item,
                            StringComparer.Ordinal);


            // =====================================================
            // ELEMENT ROW INFO
            //
            // Scan 결과에 존재하는 Element만 Excel 행으로 만듭니다.
            // Parameter가 적용되지 않은 Element는 현재 상세 Scan에
            // 존재하지 않으므로 이번 단계에서는 포함되지 않습니다.
            // =====================================================

            List<ExportElementRow>
                elementRows =
                    BuildElementRows(
                        scanItems);


            using XLWorkbook workbook =
                new XLWorkbook();


            IXLWorksheet dataWorksheet =
                workbook.Worksheets.Add(
                    DataSheetName);


            IXLWorksheet metaWorksheet =
                workbook.Worksheets.Add(
                    MetaSheetName);


            // =====================================================
            // PARAMETER DATA
            // =====================================================

            WriteParameterDataSheet(
                dataWorksheet,
                documentName,
                elementRows,
                scanItems,
                parameterColumns,
                parameterColumnMap);


            // =====================================================
            // META DATA
            // =====================================================

            WriteMetadataSheet(
                metaWorksheet,
                documentName,
                viewName,
                parameterColumns,
                elementRows);


            // =====================================================
            // META SHEET
            //
            // 일반 Excel UI에서 사용자가 직접 표시하기 어렵게
            // VeryHidden으로 설정합니다.
            // =====================================================

            metaWorksheet.Visibility =
                XLWorksheetVisibility.VeryHidden;


            // =====================================================
            // SAVE
            // =====================================================

            workbook.SaveAs(
                filePath);
        }


        // =========================================================
        // PARAMETER COLUMNS
        // =========================================================

        private static List<ExportParameterColumn>
            BuildParameterColumns(
                List<ParameterSelectionItem>
                    selectedItems)
        {
            List<ExportParameterColumn> result =
                new List<ExportParameterColumn>();


            Dictionary<string, int>
                duplicateNameCounts =
                    selectedItems
                        .GroupBy(
                            item =>
                                item.Name,
                            StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            group =>
                                group.Key,
                            group =>
                                group.Count(),
                            StringComparer.OrdinalIgnoreCase);


            const int firstParameterColumn =
                6;


            for (int index = 0;
                 index < selectedItems.Count;
                 index++)
            {
                ParameterSelectionItem item =
                    selectedItems[index];


                string key =
                    ParameterDiscoveryService.BuildKey(
                        item.IsTypeParameter,
                        item.Name,
                        item.RawGroupTypeId,
                        item.DataTypeName,
                        item.Identifier);


                string displayName =
                    item.Name;


                if (duplicateNameCounts.TryGetValue(
                    item.Name,
                    out int duplicateCount)
                    &&
                    duplicateCount > 1)
                {
                    displayName =
                        item.Name
                        +
                        " ("
                        +
                        item.ScopeName
                        +
                        ")";
                }


                ExportParameterColumn column =
                    new ExportParameterColumn
                    {
                        ExcelColumn =
                            firstParameterColumn
                            +
                            index,

                        DisplayName =
                            displayName,

                        Key =
                            key,

                        SelectionItem =
                            item
                    };


                result.Add(
                    column);
            }


            return result;
        }


        // =========================================================
        // ELEMENT ROWS
        // =========================================================

        private static List<ExportElementRow>
            BuildElementRows(
                List<ParameterItem> scanItems)
        {
            List<ExportElementRow> result =
                new List<ExportElementRow>();


            IEnumerable<IGrouping<string, ParameterItem>>
                groups =
                    scanItems
                        .GroupBy(
                            item =>
                                BuildElementKey(
                                    item),
                            StringComparer.Ordinal);


            int excelRow =
                2;


            foreach (IGrouping<string, ParameterItem>
                     group
                     in groups
                         .OrderBy(
                             group =>
                                 group.First()
                                     .CategoryName)
                         .ThenBy(
                             group =>
                                 group.First()
                                     .FamilyName)
                         .ThenBy(
                             group =>
                                 group.First()
                                     .TypeName)
                         .ThenBy(
                             group =>
                                 group.First()
                                     .ElementIdValue))
            {
                ParameterItem first =
                    group.First();


                ExportElementRow row =
                    new ExportElementRow
                    {
                        ExcelRow =
                            excelRow,

                        ElementIdValue =
                            first.ElementIdValue,

                        ElementUniqueId =
                            first.ElementUniqueId,

                        CategoryName =
                            first.CategoryName,

                        FamilyName =
                            first.FamilyName,

                        TypeName =
                            first.TypeName,

                        TypeIdValue =
                            first.TypeIdValue
                    };


                result.Add(
                    row);


                excelRow++;
            }


            return result;
        }


        // =========================================================
        // PARAMETER DATA SHEET
        // =========================================================

        private static void WriteParameterDataSheet(
            IXLWorksheet worksheet,
            string documentName,
            List<ExportElementRow> elementRows,
            List<ParameterItem> scanItems,
            List<ExportParameterColumn> parameterColumns,
            Dictionary<string, ExportParameterColumn>
                parameterColumnMap)
        {
            // =====================================================
            // FIXED HEADERS
            // =====================================================

            worksheet.Cell(
                1,
                1).Value =
                    "RVT File Name";


            worksheet.Cell(
                1,
                2).Value =
                    "Category";


            worksheet.Cell(
                1,
                3).Value =
                    "Family Name";


            worksheet.Cell(
                1,
                4).Value =
                    "Type Name";


            worksheet.Cell(
                1,
                5).Value =
                    "Element ID";


            // =====================================================
            // PARAMETER HEADERS
            // =====================================================

            foreach (ExportParameterColumn column
                     in parameterColumns)
            {
                worksheet.Cell(
                    1,
                    column.ExcelColumn).Value =
                        column.DisplayName;
            }


            // =====================================================
            // HEADER STYLE
            // =====================================================

            int lastColumn =
                5
                +
                parameterColumns.Count;


            IXLRange headerRange =
                worksheet.Range(
                    1,
                    1,
                    1,
                    lastColumn);


            headerRange.Style.Font.Bold =
                true;


            headerRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;


            headerRange.Style.Alignment.WrapText =
                true;


            worksheet.Row(
                1).Height =
                    24;


            // =====================================================
            // ELEMENT ROW MAP
            // =====================================================

            Dictionary<string, ExportElementRow>
                elementRowMap =
                    elementRows
                        .ToDictionary(
                            row =>
                                BuildElementKey(
                                    row),
                            row =>
                                row,
                            StringComparer.Ordinal);


            // =====================================================
            // FIXED ELEMENT DATA
            // =====================================================

            foreach (ExportElementRow row
                     in elementRows)
            {
                worksheet.Cell(
                    row.ExcelRow,
                    1).Value =
                        documentName;


                worksheet.Cell(
                    row.ExcelRow,
                    2).Value =
                        row.CategoryName;


                worksheet.Cell(
                    row.ExcelRow,
                    3).Value =
                        row.FamilyName;


                worksheet.Cell(
                    row.ExcelRow,
                    4).Value =
                        row.TypeName;


                worksheet.Cell(
                    row.ExcelRow,
                    5).Value =
                        row.ElementIdValue;
            }


            // =====================================================
            // PARAMETER VALUES
            // =====================================================

            foreach (ParameterItem item
                     in scanItems)
            {
                string elementKey =
                    BuildElementKey(
                        item);


                if (!elementRowMap.TryGetValue(
                    elementKey,
                    out ExportElementRow?
                        elementRow))
                {
                    continue;
                }


                string parameterKey =
                    ParameterDiscoveryService.BuildKey(
                        item.IsTypeParameter,
                        item.Name,
                        item.RawGroupTypeId,
                        item.DataTypeName,
                        item.Identifier);


                if (!parameterColumnMap.TryGetValue(
                    parameterKey,
                    out ExportParameterColumn?
                        parameterColumn))
                {
                    continue;
                }


                worksheet.Cell(
                    elementRow.ExcelRow,
                    parameterColumn.ExcelColumn).Value =
                        item.Value;
            }


            // =====================================================
            // DATA RANGE / AUTO FILTER
            // =====================================================

            int lastRow =
                elementRows.Count
                +
                1;


            if (lastRow >= 1
                &&
                lastColumn >= 1)
            {
                IXLRange dataRange =
                    worksheet.Range(
                        1,
                        1,
                        lastRow,
                        lastColumn);


                dataRange.SetAutoFilter();
            }


            // =====================================================
            // FREEZE
            //
            // Row 1
            // First 5 fixed columns
            // =====================================================

            worksheet.SheetView.FreezeRows(
                1);


            worksheet.SheetView.FreezeColumns(
                5);


            // =====================================================
            // COLUMN WIDTH
            //
            // 대용량 모델에서 AdjustToContents()를 전체 데이터에
            // 적용하면 Export 시간이 크게 증가할 수 있으므로
            // 고정 폭을 사용합니다.
            // =====================================================

            worksheet.Column(
                1).Width =
                    28;


            worksheet.Column(
                2).Width =
                    22;


            worksheet.Column(
                3).Width =
                    28;


            worksheet.Column(
                4).Width =
                    28;


            worksheet.Column(
                5).Width =
                    14;


            foreach (ExportParameterColumn column
                     in parameterColumns)
            {
                worksheet.Column(
                    column.ExcelColumn).Width =
                        20;
            }


            // =====================================================
            // ALIGNMENT
            // =====================================================

            if (lastRow >= 2)
            {
                worksheet.Range(
                    2,
                    1,
                    lastRow,
                    lastColumn)
                    .Style
                    .Alignment
                    .Vertical =
                        XLAlignmentVerticalValues.Center;
            }
        }


        // =========================================================
        // METADATA SHEET
        // =========================================================

        private static void WriteMetadataSheet(
            IXLWorksheet worksheet,
            string documentName,
            string viewName,
            List<ExportParameterColumn>
                parameterColumns,
            List<ExportElementRow>
                elementRows)
        {
            // =====================================================
            // DOCUMENT METADATA
            // =====================================================

            worksheet.Cell(
                1,
                1).Value =
                    "WonClick Parameter Export Metadata";


            worksheet.Cell(
                1,
                1).Style.Font.Bold =
                    true;


            worksheet.Cell(
                3,
                1).Value =
                    "DocumentName";


            worksheet.Cell(
                3,
                2).Value =
                    documentName;


            worksheet.Cell(
                4,
                1).Value =
                    "ViewName";


            worksheet.Cell(
                4,
                2).Value =
                    viewName;


            worksheet.Cell(
                5,
                1).Value =
                    "ExportedAt";


            worksheet.Cell(
                5,
                2).Value =
                    DateTime.Now.ToString(
                        "yyyy-MM-dd HH:mm:ss");


            // =====================================================
            // PARAMETER METADATA
            // =====================================================

            const int parameterTitleRow =
                8;


            const int parameterHeaderRow =
                9;


            worksheet.Cell(
                parameterTitleRow,
                1).Value =
                    "PARAMETER_METADATA";


            worksheet.Cell(
                parameterTitleRow,
                1).Style.Font.Bold =
                    true;


            string[] parameterHeaders =
            {
                "ExcelColumn",
                "ParameterName",
                "UiGroup",
                "Scope",
                "Identifier",
                "SharedParameterGuid",
                "ParameterDefinitionId",
                "RawGroupTypeId",
                "DataType"
            };


            for (int index = 0;
                 index < parameterHeaders.Length;
                 index++)
            {
                worksheet.Cell(
                    parameterHeaderRow,
                    index + 1).Value =
                        parameterHeaders[index];
            }


            worksheet.Range(
                parameterHeaderRow,
                1,
                parameterHeaderRow,
                parameterHeaders.Length)
                .Style
                .Font
                .Bold =
                    true;


            int currentRow =
                parameterHeaderRow
                +
                1;


            foreach (ExportParameterColumn column
                     in parameterColumns)
            {
                ParameterSelectionItem item =
                    column.SelectionItem;


                worksheet.Cell(
                    currentRow,
                    1).Value =
                        column.ExcelColumn;


                worksheet.Cell(
                    currentRow,
                    2).Value =
                        item.Name;


                worksheet.Cell(
                    currentRow,
                    3).Value =
                        item.UiGroup;


                worksheet.Cell(
                    currentRow,
                    4).Value =
                        item.ScopeName;


                worksheet.Cell(
                    currentRow,
                    5).Value =
                        item.Identifier;


                worksheet.Cell(
                    currentRow,
                    6).Value =
                        item.SharedParameterGuid;


                worksheet.Cell(
                    currentRow,
                    7).Value =
                        item.ParameterDefinitionId;


                worksheet.Cell(
                    currentRow,
                    8).Value =
                        item.RawGroupTypeId;


                worksheet.Cell(
                    currentRow,
                    9).Value =
                        item.DataTypeName;


                currentRow++;
            }


            // =====================================================
            // ELEMENT METADATA
            // =====================================================

            currentRow +=
                2;


            int elementTitleRow =
                currentRow;


            int elementHeaderRow =
                elementTitleRow
                +
                1;


            worksheet.Cell(
                elementTitleRow,
                1).Value =
                    "ELEMENT_METADATA";


            worksheet.Cell(
                elementTitleRow,
                1).Style.Font.Bold =
                    true;


            string[] elementHeaders =
            {
                "ExcelRow",
                "ElementId",
                "UniqueId",
                "TypeId"
            };


            for (int index = 0;
                 index < elementHeaders.Length;
                 index++)
            {
                worksheet.Cell(
                    elementHeaderRow,
                    index + 1).Value =
                        elementHeaders[index];
            }


            worksheet.Range(
                elementHeaderRow,
                1,
                elementHeaderRow,
                elementHeaders.Length)
                .Style
                .Font
                .Bold =
                    true;


            currentRow =
                elementHeaderRow
                +
                1;


            foreach (ExportElementRow row
                     in elementRows)
            {
                worksheet.Cell(
                    currentRow,
                    1).Value =
                        row.ExcelRow;


                worksheet.Cell(
                    currentRow,
                    2).Value =
                        row.ElementIdValue;


                worksheet.Cell(
                    currentRow,
                    3).Value =
                        row.ElementUniqueId;


                worksheet.Cell(
                    currentRow,
                    4).Value =
                        row.TypeIdValue;


                currentRow++;
            }


            // =====================================================
            // META COLUMN WIDTH
            // =====================================================

            worksheet.Column(
                1).Width =
                    24;


            worksheet.Column(
                2).Width =
                    40;


            worksheet.Column(
                3).Width =
                    22;


            worksheet.Column(
                4).Width =
                    18;


            worksheet.Column(
                5).Width =
                    48;


            worksheet.Column(
                6).Width =
                    40;


            worksheet.Column(
                7).Width =
                    24;


            worksheet.Column(
                8).Width =
                    48;


            worksheet.Column(
                9).Width =
                    36;
        }


        // =========================================================
        // ELEMENT KEY
        // =========================================================

        private static string BuildElementKey(
            ParameterItem item)
        {
            if (!string.IsNullOrWhiteSpace(
                item.ElementUniqueId))
            {
                return
                    "UID|"
                    +
                    item.ElementUniqueId;
            }


            return
                "ID|"
                +
                item.ElementIdValue;
        }


        private static string BuildElementKey(
            ExportElementRow row)
        {
            if (!string.IsNullOrWhiteSpace(
                row.ElementUniqueId))
            {
                return
                    "UID|"
                    +
                    row.ElementUniqueId;
            }


            return
                "ID|"
                +
                row.ElementIdValue;
        }


        // =========================================================
        // INTERNAL MODELS
        // =========================================================

        private sealed class ExportParameterColumn
        {
            public int ExcelColumn { get; set; }


            public string DisplayName { get; set; } =
                string.Empty;


            public string Key { get; set; } =
                string.Empty;


            public ParameterSelectionItem
                SelectionItem
            { get; set; } =
                    new ParameterSelectionItem();
        }


        private sealed class ExportElementRow
        {
            public int ExcelRow { get; set; }


            public long ElementIdValue { get; set; }


            public string ElementUniqueId { get; set; } =
                string.Empty;


            public string CategoryName { get; set; } =
                string.Empty;


            public string FamilyName { get; set; } =
                string.Empty;


            public string TypeName { get; set; } =
                string.Empty;


            public long TypeIdValue { get; set; }
        }
    }
}