using Autodesk.Revit.DB;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using WonClick.Parameter.Models;
using WonClick.Parameter.Services;

namespace WonClick.Parameter.Views
{
    public partial class ParameterManagerWindow : Window
    {
        private readonly Document _document;

        private readonly List<Element>
            _visibleElements;

        private readonly List<ParameterSelectionItem>
            _allItems;

        private readonly ICollectionView
            _collectionView;

        private string _currentFilter =
            "All";


        public ParameterManagerWindow(
            Document document,
            string documentName,
            string viewName,
            List<Element> visibleElements,
            int uniqueTotalParameterCount,
            int uniqueInstanceParameterCount,
            int uniqueTypeParameterCount,
            int textGroupCount,
            int setGroupCount,
            int constructionGroupCount,
            int dimensionsGroupCount,
            int otherGroupCount,
            List<ParameterSelectionItem> selectionItems)
        {
            InitializeComponent();


            _document =
                document;


            _visibleElements =
                visibleElements;


            _allItems =
                selectionItems;


            // =====================================================
            // HEADER
            // =====================================================

            DocumentNameTextBlock.Text =
                documentName;


            ViewNameTextBlock.Text =
                viewName;


            ElementCountTextBlock.Text =
                visibleElements.Count
                    .ToString("N0");


            ParameterCountTextBlock.Text =
                uniqueTotalParameterCount
                    .ToString("N0");


            ScopeCountTextBlock.Text =
                $"I {uniqueInstanceParameterCount:N0}"
                +
                " / "
                +
                $"T {uniqueTypeParameterCount:N0}";


            // =====================================================
            // COLLECTION VIEW
            // =====================================================

            _collectionView =
                CollectionViewSource
                    .GetDefaultView(
                        _allItems);


            _collectionView.Filter =
                FilterItem;


            ParameterDataGrid.ItemsSource =
                _collectionView;


            // =====================================================
            // FILTER BUTTON TOOLTIPS
            // =====================================================

            SetFilterToolTips(
                textGroupCount,
                setGroupCount,
                constructionGroupCount,
                dimensionsGroupCount,
                otherGroupCount);


            SetCurrentFilter(
                "All");


            UpdateSearchUi();
            UpdateSelectionSummary();
            UpdateFilterCount();


            // =====================================================
            // WORK STATUS
            // =====================================================

            WorkStatusTextBlock.Text =
                "Ready";


            WorkDetailTextBlock.Text =
                $"{uniqueTotalParameterCount:N0} Parameters";


            WorkProgressBar.Value =
                0;


            WorkProgressBar.IsIndeterminate =
                false;


            // =====================================================
            // RESULT
            // =====================================================

            HideResultPanel();


            // =====================================================
            // EXPORT / IMPORT
            // =====================================================

            ExportButton.IsEnabled =
                true;


            ImportButton.IsEnabled =
                true;


            ExportButton.Click +=
                ExportButton_Click;


            ImportButton.Click +=
                ImportButton_Click;
        }


        // =========================================================
        // FILTER
        // =========================================================

        private bool FilterItem(
            object item)
        {
            if (item
                is not ParameterSelectionItem
                    parameterItem)
            {
                return false;
            }


            if (_currentFilter != "All"
                &&
                !string.Equals(
                    parameterItem.UiGroup,
                    _currentFilter,
                    StringComparison.Ordinal))
            {
                return false;
            }


            string searchText =
                SearchTextBox.Text
                    ?.Trim()
                ?? string.Empty;


            if (!string.IsNullOrWhiteSpace(
                searchText))
            {
                if (!parameterItem.Name.Contains(
                    searchText,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }


            return true;
        }


        // =========================================================
        // SEARCH
        // =========================================================

        private void SearchTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            UpdateSearchUi();


            if (_collectionView == null)
            {
                return;
            }


            _collectionView.Refresh();


            UpdateFilterCount();
            UpdateSelectionSummary();
        }


        private void UpdateSearchUi()
        {
            bool hasText =
                !string.IsNullOrEmpty(
                    SearchTextBox.Text);


            SearchPlaceholderTextBlock.Visibility =
                hasText
                    ? System.Windows.Visibility.Collapsed
                    : System.Windows.Visibility.Visible;


            SearchClearButton.Visibility =
                hasText
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
        }


        private void SearchClearButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SearchTextBox.Clear();


            SearchTextBox.Focus();
        }


        // =========================================================
        // FILTER BUTTON
        // =========================================================

        private void FilterButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender
                is not Button button)
            {
                return;
            }


            string filter =
                button.Tag
                    ?.ToString()
                ?? "All";


            SetCurrentFilter(
                filter);
        }


        private void SetCurrentFilter(
            string filter)
        {
            _currentFilter =
                filter;


            _collectionView.Refresh();


            ParameterDataGrid.UnselectAll();


            UpdateFilterCount();
            UpdateSelectionSummary();
        }


        // =========================================================
        // SELECT ALL
        // =========================================================

        private void SelectAllButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            List<ParameterSelectionItem>
                visibleItems =
                    GetVisibleItems();


            foreach (ParameterSelectionItem item
                     in visibleItems)
            {
                item.IsSelected =
                    true;
            }


            ParameterDataGrid.UnselectAll();


            ParameterDataGrid.Items.Refresh();


            UpdateSelectionSummary();
        }


        // =========================================================
        // CLEAR
        // =========================================================

        private void ClearSelectionButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ParameterDataGrid.CommitEdit(
                DataGridEditingUnit.Cell,
                true);


            ParameterDataGrid.CommitEdit(
                DataGridEditingUnit.Row,
                true);


            List<ParameterSelectionItem>
                visibleItems =
                    GetVisibleItems();


            foreach (ParameterSelectionItem item
                     in visibleItems)
            {
                item.IsSelected =
                    false;
            }


            ParameterDataGrid.UnselectAll();


            _collectionView.Refresh();


            ParameterDataGrid.Items.Refresh();


            UpdateSelectionSummary();
            UpdateFilterCount();


            ForceUiRefresh();
        }


        // =========================================================
        // CHECKBOX
        // =========================================================

        private void SelectionCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(
                new Action(
                    UpdateSelectionSummary),
                DispatcherPriority.Background);
        }


        // =========================================================
        // VISIBLE ITEMS
        // =========================================================

        private List<ParameterSelectionItem>
            GetVisibleItems()
        {
            return _collectionView
                .Cast<ParameterSelectionItem>()
                .ToList();
        }


        // =========================================================
        // SELECTION SUMMARY
        // =========================================================

        private void UpdateSelectionSummary()
        {
            List<ParameterSelectionItem>
                visibleItems =
                    GetVisibleItems();


            int totalSelected =
                _allItems.Count(
                    item =>
                        item.IsSelected);


            int visibleSelected =
                visibleItems.Count(
                    item =>
                        item.IsSelected);


            int visibleCount =
                visibleItems.Count;


            SelectedCountTextBlock.Text =
                $"Selected "
                +
                $"{totalSelected:N0}"
                +
                $"  |  Current "
                +
                $"{visibleSelected:N0}"
                +
                "/"
                +
                $"{visibleCount:N0}";
        }


        // =========================================================
        // FILTER COUNT
        // =========================================================

        private void UpdateFilterCount()
        {
            int visibleCount =
                GetVisibleItems()
                    .Count;


            FilterCountTextBlock.Text =
                $"현재 목록 : "
                +
                $"{visibleCount:N0}"
                +
                " / "
                +
                $"{_allItems.Count:N0}";
        }


        // =========================================================
        // EXCEL EXPORT
        // =========================================================

        private void ExportButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HideResultPanel();


            List<ParameterSelectionItem>
                selectedItems =
                    _allItems
                        .Where(
                            item =>
                                item.IsSelected)
                        .ToList();


            // =====================================================
            // NO PARAMETER SELECTED
            // =====================================================

            if (selectedItems.Count == 0)
            {
                SetWorkReady(
                    "Export Ready",
                    "Parameter를 선택하세요.");


                ShowSimpleResult(
                    ResultType.Warning,
                    "Export 준비",
                    "Excel Export할 Parameter를 선택하세요.");


                return;
            }


            string safeDocumentName =
                GetSafeFileName(
                    _document.Title);


            string defaultFileName =
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmmss")
                +
                "_"
                +
                safeDocumentName
                +
                ".xlsx";


            SaveFileDialog saveFileDialog =
                new SaveFileDialog
                {
                    Title =
                        "WonClick Parameter Excel Export",

                    Filter =
                        "Excel Workbook (*.xlsx)|*.xlsx",

                    DefaultExt =
                        ".xlsx",

                    AddExtension =
                        true,

                    OverwritePrompt =
                        true,

                    FileName =
                        defaultFileName
                };


            bool? dialogResult =
                saveFileDialog.ShowDialog(
                    this);


            if (dialogResult != true)
            {
                SetWorkReady(
                    "Export Cancelled",
                    "Excel Export가 취소되었습니다.");


                return;
            }


            string filePath =
                saveFileDialog.FileName;


            if (string.IsNullOrWhiteSpace(
                filePath))
            {
                SetWorkReady(
                    "Export Cancelled",
                    "저장 위치가 선택되지 않았습니다.");


                ShowSimpleResult(
                    ResultType.Warning,
                    "Export 취소",
                    "저장할 Excel 파일 위치가 선택되지 않았습니다.");


                return;
            }


            try
            {
                // =================================================
                // SCAN
                // =================================================

                SetWorkStarted(
                    "Scanning...",
                    $"{selectedItems.Count:N0} Parameters");


                List<ParameterItem> scanItems =
                    ParameterExportScanService
                        .ScanSelectedParameters(
                            _document,
                            _visibleElements,
                            selectedItems);


                // =================================================
                // NO DATA
                // =================================================

                if (scanItems.Count == 0)
                {
                    SetWorkReady(
                        "No Data",
                        "Export할 값이 없습니다.");


                    ShowSimpleResult(
                        ResultType.Warning,
                        "Export 결과",
                        "선택한 Parameter에서 Export할 값을 찾지 못했습니다.");


                    return;
                }


                // =================================================
                // CREATE EXCEL
                // =================================================

                WorkStatusTextBlock.Text =
                    "Creating Excel...";


                WorkDetailTextBlock.Text =
                    $"{scanItems.Count:N0} Values";


                ForceUiRefresh();


                ExcelExportService.Export(
                    filePath,
                    _document.Title,
                    ViewNameTextBlock.Text,
                    selectedItems,
                    scanItems);


                // =================================================
                // SUMMARY
                // =================================================

                int instanceItemCount =
                    scanItems.Count(
                        item =>
                            !item.IsTypeParameter);


                int typeItemCount =
                    scanItems.Count(
                        item =>
                            item.IsTypeParameter);


                int writableCount =
                    scanItems.Count(
                        item =>
                            item.IsWritable);


                int readOnlyCount =
                    scanItems.Count(
                        item =>
                            item.IsReadOnly);


                int elementCount =
                    scanItems
                        .Select(
                            item =>
                                !string.IsNullOrWhiteSpace(
                                    item.ElementUniqueId)
                                    ? item.ElementUniqueId
                                    : item.ElementIdValue
                                        .ToString())
                        .Distinct(
                            StringComparer.Ordinal)
                        .Count();


                SetWorkCompleted(
                    "Export Completed",
                    $"{scanItems.Count:N0} Values");


                ShowExportResult(
                    filePath,
                    selectedItems.Count,
                    elementCount,
                    scanItems.Count,
                    instanceItemCount,
                    typeItemCount,
                    writableCount,
                    readOnlyCount);
            }
            catch (Exception ex)
            {
                SetWorkReady(
                    "Export Failed",
                    ex.Message);


                ShowSimpleResult(
                    ResultType.Error,
                    "Export Failed",
                    "Excel Export 중 오류가 발생했습니다.",
                    ex.Message);
            }
        }


        // =========================================================
        // EXCEL IMPORT
        // =========================================================

        private void ImportButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HideResultPanel();


            OpenFileDialog openFileDialog =
                new OpenFileDialog
                {
                    Title =
                        "WonClick Parameter Excel Import",

                    Filter =
                        "Excel Workbook (*.xlsx)|*.xlsx",

                    DefaultExt =
                        ".xlsx",

                    CheckFileExists =
                        true,

                    Multiselect =
                        false
                };


            bool? dialogResult =
                openFileDialog.ShowDialog(
                    this);


            if (dialogResult != true)
            {
                SetWorkReady(
                    "Import Cancelled",
                    "Excel Import가 취소되었습니다.");


                return;
            }


            string filePath =
                openFileDialog.FileName;


            if (string.IsNullOrWhiteSpace(
                filePath))
            {
                SetWorkReady(
                    "Import Cancelled",
                    "Import할 Excel 파일이 선택되지 않았습니다.");


                ShowSimpleResult(
                    ResultType.Warning,
                    "Import 취소",
                    "Import할 Excel 파일이 선택되지 않았습니다.");


                return;
            }


            try
            {
                // =================================================
                // ANALYZE
                // =================================================

                SetWorkStarted(
                    "Analyzing Excel...",
                    Path.GetFileName(
                        filePath));


                List<ImportChangeItem> changes =
                    ExcelImportService
                        .Analyze(
                            _document,
                            filePath);


                // =================================================
                // NO CHANGE
                // =================================================

                if (changes.Count == 0)
                {
                    SetWorkCompleted(
                        "Analyze Completed",
                        "No Changes");


                    ShowImportNoChangeResult(
                        filePath);


                    return;
                }


                // =================================================
                // PREVIEW
                // =================================================

                SetWorkCompleted(
                    "Analyze Completed",
                    $"{changes.Count:N0} Changes");


                ImportPreviewWindow previewWindow =
                    new ImportPreviewWindow(
                        _document,
                        filePath,
                        changes);


                previewWindow.Owner =
                    this;


                previewWindow.ShowDialog();


                // =================================================
                // APPLY RESULT
                // =================================================

                if (previewWindow.WasApplied)
                {
                    IReadOnlyList<ImportApplyResult> results =
                        previewWindow.ApplyResults;


                    int appliedCount =
                        results.Count(
                            item =>
                                item.IsApplied);


                    int skippedCount =
                        results.Count(
                            item =>
                                item.IsSkipped);


                    int conflictCount =
                        results.Count(
                            item =>
                                item.IsConflict);


                    int failedCount =
                        results.Count(
                            item =>
                                item.IsFailed);


                    int readOnlyCount =
                        results.Count(
                            item =>
                                item.IsReadOnly);


                    WorkStatusTextBlock.Text =
                        "Import Completed";


                    WorkDetailTextBlock.Text =
                        $"Applied {appliedCount:N0}"
                        +
                        "  |  "
                        +
                        $"Skipped {skippedCount:N0}"
                        +
                        "  |  "
                        +
                        $"Conflict {conflictCount:N0}"
                        +
                        "  |  "
                        +
                        $"Failed {failedCount:N0}"
                        +
                        "  |  "
                        +
                        $"Read Only {readOnlyCount:N0}";


                    WorkProgressBar.IsIndeterminate =
                        false;


                    WorkProgressBar.Value =
                        100;


                    ExportButton.IsEnabled =
                        true;


                    ImportButton.IsEnabled =
                        true;


                    ForceUiRefresh();


                    return;
                }


                // =================================================
                // PREVIEW ONLY
                // =================================================

                int instanceCount =
                    changes.Count(
                        item =>
                            !item.IsTypeParameter);


                int typeCount =
                    changes.Count(
                        item =>
                            item.IsTypeParameter);


                int readOnlyAnalyzeCount =
                    changes.Count(
                        item =>
                            item.IsReadOnly);


                int writableCount =
                    changes.Count
                    -
                    readOnlyAnalyzeCount;


                WorkStatusTextBlock.Text =
                    "Analyze Completed";


                WorkDetailTextBlock.Text =
                    $"{changes.Count:N0} Changes"
                    +
                    "  |  "
                    +
                    $"I {instanceCount:N0}"
                    +
                    " / "
                    +
                    $"T {typeCount:N0}"
                    +
                    "  |  "
                    +
                    $"Writable {writableCount:N0}"
                    +
                    " / "
                    +
                    $"Read Only {readOnlyAnalyzeCount:N0}";


                WorkProgressBar.IsIndeterminate =
                    false;


                WorkProgressBar.Value =
                    100;


                ExportButton.IsEnabled =
                    true;


                ImportButton.IsEnabled =
                    true;


                ForceUiRefresh();
            }
            catch (Exception ex)
            {
                SetWorkReady(
                    "Import Failed",
                    ex.Message);


                ShowSimpleResult(
                    ResultType.Error,
                    "Import Failed",
                    "Excel Import 중 오류가 발생했습니다.",
                    ex.Message);
            }
        }


        // =========================================================
        // EXPORT RESULT
        // =========================================================

        private void ShowExportResult(
            string filePath,
            int selectedParameterCount,
            int elementCount,
            int valueCount,
            int instanceCount,
            int typeCount,
            int writableCount,
            int readOnlyCount)
        {
            ApplyResultTheme(
                ResultType.Success);


            ResultTitleTextBlock.Text =
                "Export Completed";


            ResultMessageTextBlock.Text =
                "Excel Export가 완료되었습니다.";


            ResultSummaryPanel.Visibility =
                System.Windows.Visibility.Visible;


            ResultCard1LabelTextBlock.Text =
                "Selected Parameter";


            ResultCard1ValueTextBlock.Text =
                selectedParameterCount
                    .ToString("N0");


            ResultCard2LabelTextBlock.Text =
                "Export Element";


            ResultCard2ValueTextBlock.Text =
                elementCount
                    .ToString("N0");


            ResultCard3LabelTextBlock.Text =
                "Parameter Value";


            ResultCard3ValueTextBlock.Text =
                valueCount
                    .ToString("N0");


            ResultCard4LabelTextBlock.Text =
                "Writable / Read Only";


            ResultCard4ValueTextBlock.Text =
                $"{writableCount:N0} / {readOnlyCount:N0}";


            ResultDetailTextBlock.Text =
                $"Instance {instanceCount:N0}"
                +
                "  |  "
                +
                $"Type {typeCount:N0}"
                +
                "  |  "
                +
                $"Writable {writableCount:N0}"
                +
                "  |  "
                +
                $"Read Only {readOnlyCount:N0}";


            ResultFilePanel.Visibility =
                System.Windows.Visibility.Visible;


            ResultFileNameTextBlock.Text =
                Path.GetFileName(
                    filePath);


            ResultFilePathTextBlock.Text =
                filePath;


            ResultPanelBorder.Visibility =
                System.Windows.Visibility.Visible;


            ForceUiRefresh();
        }


        // =========================================================
        // IMPORT NO CHANGE RESULT
        // =========================================================

        private void ShowImportNoChangeResult(
            string filePath)
        {
            ApplyResultTheme(
                ResultType.Info);


            ResultTitleTextBlock.Text =
                "Import Result";


            ResultMessageTextBlock.Text =
                "변경된 Parameter가 없습니다.";


            ResultSummaryPanel.Visibility =
                System.Windows.Visibility.Collapsed;


            ResultDetailTextBlock.Text =
                "현재 Revit 값과 Excel 값이 동일합니다.";


            ResultFilePanel.Visibility =
                System.Windows.Visibility.Visible;


            ResultFileNameTextBlock.Text =
                Path.GetFileName(
                    filePath);


            ResultFilePathTextBlock.Text =
                filePath;


            ResultPanelBorder.Visibility =
                System.Windows.Visibility.Visible;


            ForceUiRefresh();
        }


        // =========================================================
        // SIMPLE RESULT
        // =========================================================

        private void ShowSimpleResult(
            ResultType resultType,
            string title,
            string message,
            string detail = "")
        {
            ApplyResultTheme(
                resultType);


            ResultTitleTextBlock.Text =
                title;


            ResultMessageTextBlock.Text =
                message;


            ResultSummaryPanel.Visibility =
                System.Windows.Visibility.Collapsed;


            ResultDetailTextBlock.Text =
                detail;


            ResultFilePanel.Visibility =
                System.Windows.Visibility.Collapsed;


            ResultFileNameTextBlock.Text =
                string.Empty;


            ResultFilePathTextBlock.Text =
                string.Empty;


            ResultPanelBorder.Visibility =
                System.Windows.Visibility.Visible;


            ForceUiRefresh();
        }


        // =========================================================
        // RESULT THEME
        // =========================================================

        private void ApplyResultTheme(
            ResultType resultType)
        {
            BrushConverter converter =
                new BrushConverter();


            switch (resultType)
            {
                case ResultType.Success:

                    ResultPanelBorder.Background =
                        (Brush)converter.ConvertFromString(
                            "#E8F5E9")!;


                    ResultPanelBorder.BorderBrush =
                        (Brush)converter.ConvertFromString(
                            "#81C784")!;


                    ResultTitleTextBlock.Foreground =
                        (Brush)converter.ConvertFromString(
                            "#2E7D32")!;

                    break;


                case ResultType.Warning:

                    ResultPanelBorder.Background =
                        (Brush)converter.ConvertFromString(
                            "#FFF4E5")!;


                    ResultPanelBorder.BorderBrush =
                        (Brush)converter.ConvertFromString(
                            "#FFB74D")!;


                    ResultTitleTextBlock.Foreground =
                        (Brush)converter.ConvertFromString(
                            "#B26A00")!;

                    break;


                case ResultType.Error:

                    ResultPanelBorder.Background =
                        (Brush)converter.ConvertFromString(
                            "#FDECEC")!;


                    ResultPanelBorder.BorderBrush =
                        (Brush)converter.ConvertFromString(
                            "#EF9A9A")!;


                    ResultTitleTextBlock.Foreground =
                        (Brush)converter.ConvertFromString(
                            "#C62828")!;

                    break;


                default:

                    ResultPanelBorder.Background =
                        (Brush)converter.ConvertFromString(
                            "#EEF4FB")!;


                    ResultPanelBorder.BorderBrush =
                        (Brush)converter.ConvertFromString(
                            "#90B4D8")!;


                    ResultTitleTextBlock.Foreground =
                        (Brush)converter.ConvertFromString(
                            "#2D5F9A")!;

                    break;
            }
        }


        // =========================================================
        // RESULT CLOSE
        // =========================================================

        private void ResultCloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HideResultPanel();
        }


        private void HideResultPanel()
        {
            ResultPanelBorder.Visibility =
                System.Windows.Visibility.Collapsed;


            ResultSummaryPanel.Visibility =
                System.Windows.Visibility.Collapsed;


            ResultFilePanel.Visibility =
                System.Windows.Visibility.Collapsed;


            ResultTitleTextBlock.Text =
                string.Empty;


            ResultMessageTextBlock.Text =
                string.Empty;


            ResultDetailTextBlock.Text =
                string.Empty;


            ResultFileNameTextBlock.Text =
                string.Empty;


            ResultFilePathTextBlock.Text =
                string.Empty;
        }


        // =========================================================
        // SAFE FILE NAME
        // =========================================================

        private static string GetSafeFileName(
            string source)
        {
            if (string.IsNullOrWhiteSpace(
                source))
            {
                return "RevitModel";
            }


            string result =
                source;


            foreach (char invalidCharacter
                     in Path.GetInvalidFileNameChars())
            {
                result =
                    result.Replace(
                        invalidCharacter,
                        '_');
            }


            result =
                result.Trim();


            if (string.IsNullOrWhiteSpace(
                result))
            {
                return "RevitModel";
            }


            return result;
        }


        // =========================================================
        // FILTER TOOLTIPS
        // =========================================================

        private void SetFilterToolTips(
            int textGroupCount,
            int setGroupCount,
            int constructionGroupCount,
            int dimensionsGroupCount,
            int otherGroupCount)
        {
            foreach (object child
                     in FindFilterButtons())
            {
                if (child is not Button button)
                {
                    continue;
                }


                string tag =
                    button.Tag
                        ?.ToString()
                    ?? string.Empty;


                switch (tag)
                {
                    case "All":

                        button.ToolTip =
                            $"All : {_allItems.Count:N0}";

                        break;


                    case "Text":

                        button.ToolTip =
                            $"Text : {textGroupCount:N0}";

                        break;


                    case "Set":

                        button.ToolTip =
                            $"Set : {setGroupCount:N0}";

                        break;


                    case "Construction":

                        button.ToolTip =
                            $"Construction : "
                            +
                            $"{constructionGroupCount:N0}";

                        break;


                    case "Dimensions":

                        button.ToolTip =
                            $"Dimensions : "
                            +
                            $"{dimensionsGroupCount:N0}";

                        break;


                    case "Other":

                        button.ToolTip =
                            $"Other : {otherGroupCount:N0}";

                        break;
                }
            }
        }


        private IEnumerable<object>
            FindFilterButtons()
        {
            List<object> result =
                new List<object>();


            FindButtonsRecursive(
                this,
                result);


            return result
                .OfType<Button>()
                .Where(
                    button =>
                        button.Tag != null);
        }


        private static void FindButtonsRecursive(
            DependencyObject parent,
            List<object> result)
        {
            int count =
                VisualTreeHelper
                    .GetChildrenCount(
                        parent);


            for (int index = 0;
                 index < count;
                 index++)
            {
                DependencyObject child =
                    VisualTreeHelper
                        .GetChild(
                            parent,
                            index);


                if (child is Button)
                {
                    result.Add(
                        child);
                }


                FindButtonsRecursive(
                    child,
                    result);
            }
        }


        // =========================================================
        // WORK STARTED
        // =========================================================

        public void SetWorkStarted(
            string status,
            string detail = "")
        {
            WorkStatusTextBlock.Text =
                status;


            WorkDetailTextBlock.Text =
                detail;


            WorkDetailTextBlock.ToolTip =
                null;


            WorkProgressBar.Value =
                0;


            WorkProgressBar.IsIndeterminate =
                true;


            ExportButton.IsEnabled =
                false;


            ImportButton.IsEnabled =
                false;


            ForceUiRefresh();
        }


        // =========================================================
        // WORK COMPLETED
        // =========================================================

        public void SetWorkCompleted(
            string status,
            string detail = "")
        {
            WorkStatusTextBlock.Text =
                status;


            WorkDetailTextBlock.Text =
                detail;


            WorkProgressBar.IsIndeterminate =
                false;


            WorkProgressBar.Value =
                100;


            ExportButton.IsEnabled =
                true;


            ImportButton.IsEnabled =
                true;


            ForceUiRefresh();
        }


        // =========================================================
        // WORK READY / FAILED
        // =========================================================

        private void SetWorkReady(
            string status,
            string detail = "")
        {
            WorkStatusTextBlock.Text =
                status;


            WorkDetailTextBlock.Text =
                detail;


            WorkProgressBar.IsIndeterminate =
                false;


            WorkProgressBar.Value =
                0;


            ExportButton.IsEnabled =
                true;


            ImportButton.IsEnabled =
                true;


            ForceUiRefresh();
        }


        // =========================================================
        // FORCE UI REFRESH
        // =========================================================

        private void ForceUiRefresh()
        {
            Dispatcher.Invoke(
                DispatcherPriority.Background,
                new Action(
                    delegate
                    {
                    }));
        }


        // =========================================================
        // CLOSE
        // =========================================================

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }


        // =========================================================
        // RESULT TYPE
        // =========================================================

        private enum ResultType
        {
            Info,
            Success,
            Warning,
            Error
        }
    }
}