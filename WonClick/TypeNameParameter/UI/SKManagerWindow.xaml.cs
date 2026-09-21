using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WonClick.TypeNameParameter.ExternalEvents;
using WonClick.TypeNameParameter.Models;
using WonClick.TypeNameParameter.Services;

namespace WonClick.TypeNameParameter.UI
{
    public partial class SKManagerWindow
        : Window
    {
        private readonly SKManagerExternalEventHandler
            _handler;


        private readonly ExternalEvent
            _externalEvent;


        private int
            _approximateCandidateCount;


        private bool
            _viewFillAvailable;


        public ObservableCollection<SKViewFillRow> Rows
        {
            get;
        } =
            new ObservableCollection<SKViewFillRow>();


        public ObservableCollection<string> ParameterNames
        {
            get;
        } =
            new ObservableCollection<string>();


        // =========================================================
        // Constructor
        // =========================================================

        public SKManagerWindow(
            SKManagerExternalEventHandler handler,
            ExternalEvent externalEvent)
        {
            InitializeComponent();


            _handler =
                handler;


            _externalEvent =
                externalEvent;


            DataContext =
                this;


            AddNewRow();
        }


        // =========================================================
        // Public API for Handler
        // =========================================================

        public void RequestRefresh()
        {
            SetBusy(
                true);


            _handler.RequestRefresh();


            RaiseExternalEvent();
        }


        public void UpdateStatus(
            SKManagerStatusInfo status)
        {
            AutoStatusText.Text =
                status.AutoSyncEnabled
                    ?
                    "● ON"
                    :
                    "○ OFF";


            AutoToggleButton.Content =
                status.AutoSyncEnabled
                    ?
                    "Turn OFF"
                    :
                    "Turn ON";


            CurrentViewText.Text =
                string.IsNullOrWhiteSpace(
                    status.ActiveViewName)
                    ?
                    "-"
                    :
                    status.ActiveViewName;


            SelectedCountText.Text =
                status.SelectedElementCount
                    .ToString("N0")
                +
                " Elements";


            _approximateCandidateCount =
                status.ApproximateCandidateCount;


            CandidateCountText.Text =
                "약 "
                +
                _approximateCandidateCount
                    .ToString("N0")
                +
                " Elements";


            ParameterNames.Clear();


            foreach (string parameterName
                     in status.ViewFillParameterNames)
            {
                ParameterNames.Add(
                    parameterName);
            }


            _viewFillAvailable =
                status.Is3DView
                &&
                status.IsSectionBoxActive
                &&
                ParameterNames.Count > 0;


            if (!status.Is3DView)
            {
                ViewFillStatusText.Text =
                    "View Fill 비활성: 현재 View가 3D View가 아닙니다.";
            }
            else if (!status.IsSectionBoxActive)
            {
                ViewFillStatusText.Text =
                    "View Fill 비활성: 현재 3D View의 Section Box가 OFF입니다.";
            }
            else if (ParameterNames.Count == 0)
            {
                ViewFillStatusText.Text =
                    "View Fill 비활성: 현재 표시 범위에서 입력 가능한 SK_* Parameter를 찾지 못했습니다.";
            }
            else
            {
                ViewFillStatusText.Text =
                    "현재 3D View + Section Box에서 실제로 보이는 객체만 입력합니다. View Filter / Hide in View / Category OFF 객체는 제외됩니다.";
            }


            UpdateViewFillControls();


            // 첫 Row의 Parameter가 비어 있으면
            // 첫 사용 가능한 Parameter를 자동 선택
            if (Rows.Count > 0
                &&
                string.IsNullOrWhiteSpace(
                    Rows[0].ParameterName)
                &&
                ParameterNames.Count > 0)
            {
                Rows[0].ParameterName =
                    ParameterNames[0];


                ViewFillGrid.Items.Refresh();
            }
        }


        public void SetResult(
            string message,
            bool succeeded)
        {
            ResultTextBox.Text =
                message
                ??
                string.Empty;


            if (!succeeded)
            {
                System.Media.SystemSounds
                    .Exclamation
                    .Play();
            }
        }


        public void SetBusy(
            bool busy)
        {
            AutoToggleButton.IsEnabled =
                !busy;


            RefreshButton.IsEnabled =
                !busy;


            SyncSelectionButton.IsEnabled =
                !busy;


            SyncViewButton.IsEnabled =
                !busy;


            SyncAllButton.IsEnabled =
                !busy;


            AddRowButton.IsEnabled =
                !busy;


            RemoveRowButton.IsEnabled =
                !busy;


            BusyText.Text =
                busy
                    ?
                    "처리 중..."
                    :
                    string.Empty;


            Cursor =
                busy
                    ?
                    Cursors.Wait
                    :
                    Cursors.Arrow;


            UpdateViewFillControls(
                busy);
        }


        // =========================================================
        // Buttons
        // =========================================================

        private void RefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            RequestRefresh();
        }


        private void AutoToggleButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SetBusy(
                true);


            _handler.RequestToggleAuto();


            RaiseExternalEvent();
        }


        private void SyncSelectionButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SetBusy(
                true);


            _handler.RequestSyncSelection();


            RaiseExternalEvent();
        }


        private void SyncViewButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SetBusy(
                true);


            _handler.RequestSyncCurrentView();


            RaiseExternalEvent();
        }


        private void SyncAllButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string warning =
                "프로젝트 전체 SK Sync를 실행합니다."
                +
                "\n\n"
                +
                "예상 대상 범위: 약 "
                +
                _approximateCandidateCount
                    .ToString("N0")
                +
                " Elements"
                +
                "\n\n"
                +
                "대형 프로젝트에서는 Transaction Commit에 "
                +
                "상당한 시간이 소요될 수 있습니다."
                +
                "\n\n"
                +
                "정말 Entire Project Sync를 실행하시겠습니까?";


            MessageBoxResult answer =
                MessageBox.Show(
                    this,
                    warning,
                    "WonClick - Entire Project Sync",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);


            if (answer !=
                MessageBoxResult.Yes)
            {
                return;
            }


            SetBusy(
                true);


            _handler.RequestSyncEntireProject();


            RaiseExternalEvent();
        }


        private void AddRowButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            AddNewRow();
        }


        private void RemoveRowButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (ViewFillGrid.SelectedItem
                is SKViewFillRow selectedRow)
            {
                Rows.Remove(
                    selectedRow);
            }


            if (Rows.Count == 0)
            {
                AddNewRow();
            }
        }


        private void ApplyViewFillButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_viewFillAvailable)
            {
                MessageBox.Show(
                    this,
                    "View Fill을 사용하려면 활성 3D View에서 Section Box가 ON이어야 합니다.",
                    "WonClick - SK Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);


                return;
            }


            List<SKViewFillRow> rows =
                Rows
                    .Where(
                        row =>
                            row != null
                            &&
                            !string.IsNullOrWhiteSpace(
                                row.ParameterName))
                    .Select(
                        row =>
                            new SKViewFillRow
                            {
                                ParameterName =
                                    row.ParameterName,

                                Value =
                                    row.Value
                            })
                    .ToList();


            if (rows.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "적용할 Parameter를 하나 이상 선택해 주세요.",
                    "WonClick - SK Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);


                return;
            }


            SetBusy(
                true);


            _handler.RequestApplyViewFill(
                rows);


            RaiseExternalEvent();
        }


        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }


        // =========================================================
        // Helpers
        // =========================================================

        private void AddNewRow()
        {
            SKViewFillRow row =
                new SKViewFillRow();


            if (ParameterNames.Count > 0)
            {
                row.ParameterName =
                    ParameterNames[0];
            }


            Rows.Add(
                row);
        }


        private void UpdateViewFillControls(
            bool busy = false)
        {
            bool enabled =
                _viewFillAvailable
                &&
                !busy;


            ViewFillGrid.IsEnabled =
                enabled;


            ApplyViewFillButton.IsEnabled =
                enabled;
        }


        private void RaiseExternalEvent()
        {
            try
            {
                _externalEvent.Raise();
            }
            catch (Exception ex)
            {
                SetBusy(
                    false);


                SetResult(
                    "Revit ExternalEvent 요청에 실패했습니다.\n\n"
                    +
                    ex.Message,
                    false);
            }
        }
    }
}
