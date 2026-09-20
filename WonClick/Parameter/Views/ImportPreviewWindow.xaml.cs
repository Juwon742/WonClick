using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WonClick.Parameter.Models;
using WonClick.Parameter.Services;

namespace WonClick.Parameter.Views
{
    public partial class ImportPreviewWindow : Window
    {
        private readonly Document _document;

        private readonly string _filePath;

        private readonly List<ImportChangeItem>
            _changes;

        private bool _isApplying;

        private ICollectionView? _resultCollectionView;

        private string _currentResultFilter =
            "All";


        public bool WasApplied { get; private set; }


        public IReadOnlyList<ImportApplyResult>
            ApplyResults
        { get; private set; } =
                Array.Empty<ImportApplyResult>();


        public ImportPreviewWindow(
            Document document,
            string filePath,
            List<ImportChangeItem> changes)
        {
            InitializeComponent();


            _document =
                document;


            _filePath =
                filePath;


            _changes =
                changes
                    ?? new List<ImportChangeItem>();


            InitializePreview();
        }


        // =========================================================
        // INITIALIZE PREVIEW
        // =========================================================

        private void InitializePreview()
        {
            WasApplied =
                false;


            _isApplying =
                false;


            ApplyResults =
                Array.Empty<ImportApplyResult>();


            _resultCollectionView =
                null;


            _currentResultFilter =
                "All";


            Title =
                "WonClick - Import Preview";


            WindowTitleTextBlock.Text =
                "Import Preview";


            WindowSubtitleTextBlock.Text =
                "Excel과 현재 Revit 값을 비교한 변경 항목";


            ListTitleTextBlock.Text =
                "Change List";


            FileNameTextBlock.Text =
                GetDisplayFileName(
                    _filePath);


            ChangeDataGrid.ItemsSource =
                _changes;


            TotalCountTextBlock.Text =
                _changes.Count
                    .ToString("N0");


            AppliedCountTextBlock.Text =
                "0";


            SkippedCountTextBlock.Text =
                "0";


            ConflictCountTextBlock.Text =
                "0";


            FailedCountTextBlock.Text =
                "0";


            ReadOnlyCountTextBlock.Text =
                "0";


            ResetSummaryCardStyles();


            UpdatePreviewListCount();


            ResultFilterPanel.Visibility =
                System.Windows.Visibility.Collapsed;


            FooterMessageTextBlock.Text =
                "Preview 단계에서는 Revit Parameter를 수정하지 않습니다.";


            ApplyConfirmationPanel.Visibility =
                System.Windows.Visibility.Collapsed;


            ApplyImportButton.Visibility =
                System.Windows.Visibility.Visible;


            ApplyImportButton.IsEnabled =
                _changes.Count > 0;


            CloseButton.IsEnabled =
                true;
        }


        // =========================================================
        // PREVIEW COUNT
        // =========================================================

        private void UpdatePreviewListCount()
        {
            int instanceCount =
                _changes.Count(
                    item =>
                        !item.IsTypeParameter);


            int typeCount =
                _changes.Count(
                    item =>
                        item.IsTypeParameter);


            ListCountTextBlock.Text =
                $"{_changes.Count:N0} Changes"
                +
                "  |  "
                +
                $"Instance {instanceCount:N0}"
                +
                "  |  "
                +
                $"Type {typeCount:N0}";
        }


        // =========================================================
        // APPLY BUTTON
        // =========================================================

        private void ApplyImportButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isApplying)
            {
                return;
            }


            if (_changes.Count == 0)
            {
                return;
            }


            ApplyConfirmationPanel.Visibility =
                System.Windows.Visibility.Visible;


            ApplyImportButton.IsEnabled =
                false;


            FooterMessageTextBlock.Text =
                "적용 내용을 확인한 뒤 '적용 실행'을 눌러주세요.";
        }


        // =========================================================
        // CANCEL APPLY
        // =========================================================

        private void CancelApplyButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isApplying)
            {
                return;
            }


            ApplyConfirmationPanel.Visibility =
                System.Windows.Visibility.Collapsed;


            ApplyImportButton.IsEnabled =
                _changes.Count > 0;


            FooterMessageTextBlock.Text =
                "Preview 단계에서는 Revit Parameter를 수정하지 않습니다.";
        }


        // =========================================================
        // CONFIRM APPLY
        // =========================================================

        private void ConfirmApplyButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isApplying)
            {
                return;
            }


            if (_changes.Count == 0)
            {
                return;
            }


            ExecuteApply();
        }


        // =========================================================
        // EXECUTE APPLY
        // =========================================================

        private void ExecuteApply()
        {
            _isApplying =
                true;


            ApplyConfirmationPanel.Visibility =
                System.Windows.Visibility.Collapsed;


            ApplyImportButton.IsEnabled =
                false;


            ConfirmApplyButton.IsEnabled =
                false;


            CancelApplyButton.IsEnabled =
                false;


            CloseButton.IsEnabled =
                false;


            FooterMessageTextBlock.Text =
                "Revit Parameter에 Excel 변경 내용을 적용하는 중입니다...";


            try
            {
                List<ImportApplyResult> results =
                    ExcelImportApplyService
                        .Apply(
                            _document,
                            _filePath,
                            _changes);


                WasApplied =
                    true;


                ApplyResults =
                    results;


                ShowImportResult(
                    results);
            }
            catch (Exception ex)
            {
                FooterMessageTextBlock.Text =
                    "Import Apply 오류: "
                    +
                    ex.Message;


                ApplyImportButton.IsEnabled =
                    true;


                ConfirmApplyButton.IsEnabled =
                    true;


                CancelApplyButton.IsEnabled =
                    true;


                CloseButton.IsEnabled =
                    true;
            }
            finally
            {
                _isApplying =
                    false;
            }
        }


        // =========================================================
        // SHOW IMPORT RESULT
        // =========================================================

        private void ShowImportResult(
            List<ImportApplyResult> results)
        {
            Title =
                "WonClick - Import Result";


            WindowTitleTextBlock.Text =
                "Import Result";


            WindowSubtitleTextBlock.Text =
                "Revit Parameter Import 적용 결과";


            ListTitleTextBlock.Text =
                "Result List";


            _resultCollectionView =
                CollectionViewSource
                    .GetDefaultView(
                        results);


            _resultCollectionView.Filter =
                FilterResultItem;


            ChangeDataGrid.ItemsSource =
                _resultCollectionView;


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


            TotalCountTextBlock.Text =
                results.Count
                    .ToString("N0");


            AppliedCountTextBlock.Text =
                appliedCount
                    .ToString("N0");


            SkippedCountTextBlock.Text =
                skippedCount
                    .ToString("N0");


            ConflictCountTextBlock.Text =
                conflictCount
                    .ToString("N0");


            FailedCountTextBlock.Text =
                failedCount
                    .ToString("N0");


            ReadOnlyCountTextBlock.Text =
                readOnlyCount
                    .ToString("N0");


            ApplySummaryCardStyles(
                appliedCount,
                conflictCount,
                failedCount,
                readOnlyCount);


            _currentResultFilter =
                "All";


            ResultFilterPanel.Visibility =
                System.Windows.Visibility.Visible;


            UpdateResultListCount();


            FooterMessageTextBlock.Text =
                BuildResultSummaryText(
                    results);


            ApplyConfirmationPanel.Visibility =
                System.Windows.Visibility.Collapsed;


            ApplyImportButton.Visibility =
                System.Windows.Visibility.Collapsed;


            ConfirmApplyButton.IsEnabled =
                true;


            CancelApplyButton.IsEnabled =
                true;


            CloseButton.IsEnabled =
                true;


            ChangeDataGrid.UnselectAll();


            ChangeDataGrid.Items.Refresh();
        }


        // =========================================================
        // RESULT FILTER
        // =========================================================

        private bool FilterResultItem(
            object item)
        {
            if (item
                is not ImportApplyResult result)
            {
                return false;
            }


            if (_currentResultFilter == "All")
            {
                return true;
            }


            return string.Equals(
                result.Status,
                _currentResultFilter,
                StringComparison.Ordinal);
        }


        private void ResultFilterButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender
                is not Button button)
            {
                return;
            }


            _currentResultFilter =
                button.Tag
                    ?.ToString()
                ?? "All";


            _resultCollectionView?.Refresh();


            ChangeDataGrid.UnselectAll();


            UpdateResultListCount();
        }


        // =========================================================
        // RESULT LIST COUNT
        // =========================================================

        private void UpdateResultListCount()
        {
            if (_resultCollectionView == null)
            {
                return;
            }


            int visibleCount =
                _resultCollectionView
                    .Cast<object>()
                    .Count();


            int totalCount =
                ApplyResults.Count;


            ListCountTextBlock.Text =
                $"{visibleCount:N0} / {totalCount:N0} Results"
                +
                "  |  "
                +
                _currentResultFilter;
        }


        // =========================================================
        // SUMMARY CARD COLORS
        // =========================================================

        private void ResetSummaryCardStyles()
        {
            Brush normalBackground =
                CreateBrush(
                    "#FFFFFF");


            Brush normalBorder =
                CreateBrush(
                    "#D8DCE2");


            Brush normalText =
                CreateBrush(
                    "#202124");


            AppliedCardBorder.Background =
                normalBackground;


            AppliedCardBorder.BorderBrush =
                normalBorder;


            AppliedCountTextBlock.Foreground =
                normalText;


            AppliedCountTextBlock.FontWeight =
                FontWeights.SemiBold;


            ConflictCardBorder.Background =
                normalBackground;


            ConflictCardBorder.BorderBrush =
                normalBorder;


            ConflictCountTextBlock.Foreground =
                normalText;


            ConflictCountTextBlock.FontWeight =
                FontWeights.SemiBold;


            FailedCardBorder.Background =
                normalBackground;


            FailedCardBorder.BorderBrush =
                normalBorder;


            FailedCountTextBlock.Foreground =
                normalText;


            FailedCountTextBlock.FontWeight =
                FontWeights.SemiBold;


            ReadOnlyCardBorder.Background =
                normalBackground;


            ReadOnlyCardBorder.BorderBrush =
                normalBorder;


            ReadOnlyCountTextBlock.Foreground =
                normalText;


            ReadOnlyCountTextBlock.FontWeight =
                FontWeights.SemiBold;
        }


        private void ApplySummaryCardStyles(
            int appliedCount,
            int conflictCount,
            int failedCount,
            int readOnlyCount)
        {
            ResetSummaryCardStyles();


            if (appliedCount > 0)
            {
                AppliedCardBorder.Background =
                    CreateBrush(
                        "#E8F5E9");


                AppliedCardBorder.BorderBrush =
                    CreateBrush(
                        "#81C784");


                AppliedCountTextBlock.Foreground =
                    CreateBrush(
                        "#2E7D32");


                AppliedCountTextBlock.FontWeight =
                    FontWeights.Bold;
            }


            if (conflictCount > 0)
            {
                ConflictCardBorder.Background =
                    CreateBrush(
                        "#FFF4E5");


                ConflictCardBorder.BorderBrush =
                    CreateBrush(
                        "#FFB74D");


                ConflictCountTextBlock.Foreground =
                    CreateBrush(
                        "#B26A00");


                ConflictCountTextBlock.FontWeight =
                    FontWeights.Bold;
            }


            if (failedCount > 0)
            {
                FailedCardBorder.Background =
                    CreateBrush(
                        "#FDECEC");


                FailedCardBorder.BorderBrush =
                    CreateBrush(
                        "#EF9A9A");


                FailedCountTextBlock.Foreground =
                    CreateBrush(
                        "#C62828");


                FailedCountTextBlock.FontWeight =
                    FontWeights.Bold;
            }


            if (readOnlyCount > 0)
            {
                ReadOnlyCardBorder.Background =
                    CreateBrush(
                        "#FDECEC");


                ReadOnlyCardBorder.BorderBrush =
                    CreateBrush(
                        "#EF9A9A");


                ReadOnlyCountTextBlock.Foreground =
                    CreateBrush(
                        "#C62828");


                ReadOnlyCountTextBlock.FontWeight =
                    FontWeights.Bold;
            }
        }


        // =========================================================
        // BRUSH
        // =========================================================

        private static Brush CreateBrush(
            string colorCode)
        {
            return
                (Brush)new BrushConverter()
                    .ConvertFromString(
                        colorCode)!;
        }


        // =========================================================
        // RESULT SUMMARY
        // =========================================================

        private static string BuildResultSummaryText(
            List<ImportApplyResult> results)
        {
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


            return
                $"완료"
                +
                "  |  "
                +
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
        }


        // =========================================================
        // FILE NAME
        // =========================================================

        private static string GetDisplayFileName(
            string filePath)
        {
            if (string.IsNullOrWhiteSpace(
                filePath))
            {
                return string.Empty;
            }


            try
            {
                return
                    Path.GetFileName(
                        filePath)
                    ?? string.Empty;
            }
            catch
            {
                return filePath;
            }
        }


        // =========================================================
        // CLOSE
        // =========================================================

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isApplying)
            {
                return;
            }


            Close();
        }
    }
}