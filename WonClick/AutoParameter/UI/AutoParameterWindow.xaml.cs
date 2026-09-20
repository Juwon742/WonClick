using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WonClick.AutoParameter.ExternalEvents;
using WonClick.AutoParameter.Models;
using WonClick.AutoParameter.Services;

namespace WonClick.AutoParameter.UI
{
    public partial class AutoParameterWindow : Window
    {
        // =========================================================
        // Current Window
        // =========================================================

        public static AutoParameterWindow? Current
        {
            get;
            private set;
        }


        // =========================================================
        // Document
        // =========================================================

        public Document Document
        {
            get
            {
                return _doc;
            }
        }


        private readonly Document _doc;


        // =========================================================
        // Parameter 목록
        // =========================================================

        public ObservableCollection<InstanceParameterItem>
            ParameterItems
        {
            get;
        } =
            new ObservableCollection<InstanceParameterItem>();


        // =========================================================
        // Rule
        // =========================================================

        public ObservableCollection<AutoParameterRuleRow>
            RuleRows
        {
            get;
        } =
            new ObservableCollection<AutoParameterRuleRow>();


        // =========================================================
        // External Event
        // =========================================================

        private readonly
            AutoParameterExternalEventHandler
            _externalEventHandler;


        private readonly ExternalEvent
            _externalEvent;


        // =========================================================
        // 상태
        // =========================================================

        private bool _catalogLoaded;

        private bool _windowClosed;


        // =========================================================
        // Constructor
        //
        // 여기서는 Document 전체를 Scan하지 않는다.
        // Window를 먼저 표시한 뒤 ExternalEvent에서
        // Parameter Catalog를 작성한다.
        // =========================================================

        public AutoParameterWindow(
            Document doc)
        {
            InitializeComponent();


            _doc =
                doc
                ?? throw new ArgumentNullException(
                    nameof(doc));


            Current =
                this;


            DataContext =
                this;


            // =====================================================
            // External Event
            // =====================================================

            _externalEventHandler =
                new AutoParameterExternalEventHandler();


            _externalEvent =
                ExternalEvent.Create(
                    _externalEventHandler);


            _externalEventHandler.Completed =
                ExternalEventCompleted;


            _externalEventHandler.CatalogLoaded =
                CatalogLoaded;


            // =====================================================
            // Window가 실제 표시된 뒤 Catalog 요청
            // =====================================================

            Loaded +=
                AutoParameterWindow_Loaded;


            Closed +=
                AutoParameterWindow_Closed;


            SetLoadingState(
                true);
        }


        // =========================================================
        // Window Loaded
        // =========================================================

        private void AutoParameterWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -=
                AutoParameterWindow_Loaded;


            BeginCatalogLoad();
        }


        // =========================================================
        // Catalog Load 요청
        // =========================================================

        private void BeginCatalogLoad()
        {
            SetLoadingState(
                true);


            CatalogInfoTextBlock.Text =
                "Parameter catalog 준비 중...";


            _externalEventHandler.Document =
                _doc;


            _externalEventHandler.Request =
                AutoParameterRequest.LoadCatalog;


            ExternalEventRequest request =
                _externalEvent.Raise();


            if (request ==
                ExternalEventRequest.Denied)
            {
                SetLoadingState(
                    false);


                CatalogInfoTextBlock.Text =
                    "Parameter catalog을 불러오지 못했습니다.";


                MessageBox.Show(
                    "현재 Revit에서 Parameter 목록을 불러올 수 없습니다.",
                    "WonClick - Auto Parameter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }


        // =========================================================
        // Catalog 완료
        // =========================================================

        private void CatalogLoaded(
            bool success,
            string message,
            AutoParameterCatalogResult? result)
        {
            if (_windowClosed)
            {
                return;
            }


            ParameterItems.Clear();


            if (!success ||
                result == null)
            {
                _catalogLoaded =
                    false;


                SetLoadingState(
                    false);


                CatalogInfoTextBlock.Text =
                    "Parameter catalog Load 실패";


                if (RuleRows.Count == 0)
                {
                    AddNewRule(
                        saveHold: false);
                }


                MessageBox.Show(
                    string.IsNullOrWhiteSpace(
                        message)
                        ?
                        "Parameter 목록을 불러오지 못했습니다."
                        :
                        message,
                    "WonClick - Auto Parameter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);


                return;
            }


            // =====================================================
            // Catalog → UI
            // =====================================================

            foreach (InstanceParameterItem item
                     in result.Items)
            {
                ParameterItems.Add(
                    item);
            }


            _catalogLoaded =
                true;


            // =====================================================
            // Hold 설정 복원
            // =====================================================

            RuleRows.Clear();


            bool restored =
                RestoreHeldSettings();


            if (!restored)
            {
                AddNewRule(
                    saveHold: false);
            }


            // =====================================================
            // Catalog 정보
            // =====================================================

            if (result.FromCache)
            {
                CatalogInfoTextBlock.Text =
                    $"Parameter catalog : {ParameterItems.Count}개 · Cache";
            }
            else
            {
                CatalogInfoTextBlock.Text =
                    $"Parameter catalog : {ParameterItems.Count}개 · "
                    +
                    $"{result.ElapsedMilliseconds} ms";
            }


            SetLoadingState(
                false);


            UpdateStatus();
        }


        // =========================================================
        // Loading UI
        // =========================================================

        private void SetLoadingState(
            bool loading)
        {
            // =====================================================
            // 중요
            //
            // Window.Visibility 속성과
            // System.Windows.Visibility enum 이름 충돌 방지
            // =====================================================

            LoadingOverlay.Visibility =
                loading
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;


            RuleDataGrid.IsEnabled =
                !loading;


            AddRuleButton.IsEnabled =
                !loading;


            ExportSettingsButton.IsEnabled =
                !loading;


            ImportSettingsButton.IsEnabled =
                !loading;


            ApplyButton.IsEnabled =
                !loading;


            if (loading)
            {
                DisableButton.IsEnabled =
                    false;
            }
            else
            {
                UpdateStatus();
            }
        }


        // =========================================================
        // Add Rule
        // =========================================================

        private void AddNewRule(
            bool saveHold = true)
        {
            AutoParameterRuleRow row =
                new AutoParameterRuleRow
                {
                    Enabled =
                        true,

                    Value =
                        string.Empty
                };


            if (ParameterItems.Count > 0)
            {
                row.SelectedParameter =
                    ParameterItems[0];
            }


            RuleRows.Add(
                row);


            if (saveHold)
            {
                SaveHeldSettingsSafe();
            }
        }


        // =========================================================
        // + Add Rule
        // =========================================================

        private void AddRuleButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_catalogLoaded)
            {
                return;
            }


            AddNewRule();
        }


        // =========================================================
        // Remove
        // =========================================================

        private void RemoveRuleButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }


            if (button.DataContext
                is not AutoParameterRuleRow row)
            {
                return;
            }


            RuleRows.Remove(
                row);


            if (RuleRows.Count == 0)
            {
                AddNewRule(
                    saveHold: false);
            }


            SaveHeldSettingsSafe();
        }


        // =========================================================
        // Hold Restore
        // =========================================================

        private bool RestoreHeldSettings()
        {
            try
            {
                AutoParameterPreset? preset =
                    AutoParameterPresetService
                        .TryLoadHold();


                if (preset == null ||
                    preset.Rules == null ||
                    preset.Rules.Count == 0)
                {
                    return false;
                }


                int restoredCount =
                    0;


                foreach (AutoParameterPresetRule presetRule
                         in preset.Rules)
                {
                    InstanceParameterItem? parameterItem =
                        FindParameterItem(
                            presetRule);


                    if (parameterItem == null)
                    {
                        continue;
                    }


                    RuleRows.Add(
                        new AutoParameterRuleRow
                        {
                            Enabled =
                                presetRule.Enabled,

                            SelectedParameter =
                                parameterItem,

                            Value =
                                presetRule.Value
                                ?? string.Empty
                        });


                    restoredCount++;
                }


                return
                    restoredCount > 0;
            }
            catch
            {
                return false;
            }
        }


        // =========================================================
        // Hold Save
        // =========================================================

        private void SaveHeldSettingsSafe()
        {
            try
            {
                if (!_catalogLoaded)
                {
                    return;
                }


                AutoParameterPreset preset =
                    BuildPresetFromCurrentRows();


                AutoParameterPresetService.SaveHold(
                    preset);
            }
            catch
            {
                // Hold 저장 실패가
                // Auto Parameter 기능을 방해하지 않도록 무시
            }
        }


        // =========================================================
        // Export
        // =========================================================

        private void ExportSettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                AutoParameterPreset preset =
                    BuildPresetFromCurrentRows();


                if (preset.Rules.Count == 0)
                {
                    MessageBox.Show(
                        "Export할 Auto Parameter Rule이 없습니다.",
                        "WonClick - Auto Parameter",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }


                SaveFileDialog dialog =
                    new SaveFileDialog
                    {
                        Title =
                            "Auto Parameter Settings Export",

                        Filter =
                            "WonClick Auto Parameter (*.wcap)|*.wcap|JSON (*.json)|*.json",

                        DefaultExt =
                            ".wcap",

                        AddExtension =
                            true,

                        FileName =
                            "AutoParameter"
                    };


                bool? result =
                    dialog.ShowDialog(
                        this);


                if (result != true)
                {
                    return;
                }


                AutoParameterPresetService.Save(
                    dialog.FileName,
                    preset);


                MessageBox.Show(
                    "Auto Parameter 설정을 Export했습니다.",
                    "WonClick - Auto Parameter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Auto Parameter 설정 Export 중 오류가 발생했습니다."
                    +
                    "\n\n"
                    +
                    ex.Message,
                    "WonClick - Auto Parameter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =========================================================
        // Import
        // =========================================================

        private void ImportSettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog dialog =
                    new OpenFileDialog
                    {
                        Title =
                            "Auto Parameter Settings Import",

                        Filter =
                            "WonClick Auto Parameter (*.wcap)|*.wcap|JSON (*.json)|*.json",

                        Multiselect =
                            false
                    };


                bool? result =
                    dialog.ShowDialog(
                        this);


                if (result != true)
                {
                    return;
                }


                AutoParameterPreset preset =
                    AutoParameterPresetService.Load(
                        dialog.FileName);


                int importedCount =
                    0;


                int notFoundCount =
                    0;


                RuleRows.Clear();


                foreach (AutoParameterPresetRule presetRule
                         in preset.Rules)
                {
                    InstanceParameterItem? parameterItem =
                        FindParameterItem(
                            presetRule);


                    if (parameterItem == null)
                    {
                        notFoundCount++;

                        continue;
                    }


                    RuleRows.Add(
                        new AutoParameterRuleRow
                        {
                            Enabled =
                                presetRule.Enabled,

                            SelectedParameter =
                                parameterItem,

                            Value =
                                presetRule.Value
                                ?? string.Empty
                        });


                    importedCount++;
                }


                if (RuleRows.Count == 0)
                {
                    AddNewRule(
                        saveHold: false);
                }


                SaveHeldSettingsSafe();


                string resultMessage =
                    "Import 완료"
                    +
                    "\n\n"
                    +
                    "Imported : "
                    +
                    importedCount;


                if (notFoundCount > 0)
                {
                    resultMessage +=
                        "\n"
                        +
                        "Not Found : "
                        +
                        notFoundCount;
                }


                MessageBox.Show(
                    resultMessage,
                    "WonClick - Auto Parameter",
                    MessageBoxButton.OK,
                    notFoundCount > 0
                        ? MessageBoxImage.Warning
                        : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Auto Parameter 설정 Import 중 오류가 발생했습니다."
                    +
                    "\n\n"
                    +
                    ex.Message,
                    "WonClick - Auto Parameter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =========================================================
        // UI → Preset
        // =========================================================

        private AutoParameterPreset
            BuildPresetFromCurrentRows()
        {
            AutoParameterPreset preset =
                new AutoParameterPreset
                {
                    Version =
                        1,

                    Name =
                        "Auto Parameter Hold"
                };


            foreach (AutoParameterRuleRow row
                     in RuleRows)
            {
                InstanceParameterItem? parameter =
                    row.SelectedParameter;


                if (parameter == null)
                {
                    continue;
                }


                preset.Rules.Add(
                    new AutoParameterPresetRule
                    {
                        Enabled =
                            row.Enabled,

                        ParameterId =
                            parameter.ParameterId,

                        ParameterName =
                            parameter.Name,

                        SharedParameterGuid =
                            parameter.SharedParameterGuid.HasValue
                                ?
                                parameter
                                    .SharedParameterGuid
                                    .Value
                                    .ToString()
                                :
                                null,

                        Value =
                            row.Value
                            ?? string.Empty
                    });
            }


            return
                preset;
        }


        // =========================================================
        // Import / Hold Parameter Matching
        //
        // 1. GUID
        // 2. Name
        // 3. ParameterId
        // =========================================================

        private InstanceParameterItem?
            FindParameterItem(
                AutoParameterPresetRule presetRule)
        {
            // =====================================================
            // 1. GUID
            // =====================================================

            if (!string.IsNullOrWhiteSpace(
                    presetRule.SharedParameterGuid)
                &&
                Guid.TryParse(
                    presetRule.SharedParameterGuid,
                    out Guid targetGuid))
            {
                InstanceParameterItem? guidMatch =
                    ParameterItems
                        .FirstOrDefault(
                            item =>
                                item.SharedParameterGuid.HasValue
                                &&
                                item.SharedParameterGuid.Value ==
                                targetGuid);


                if (guidMatch != null)
                {
                    return
                        guidMatch;
                }
            }


            // =====================================================
            // 2. Name
            // =====================================================

            if (!string.IsNullOrWhiteSpace(
                    presetRule.ParameterName))
            {
                List<InstanceParameterItem> nameMatches =
                    ParameterItems
                        .Where(
                            item =>
                                string.Equals(
                                    item.Name,
                                    presetRule.ParameterName,
                                    StringComparison.Ordinal))
                        .ToList();


                if (nameMatches.Count == 1)
                {
                    return
                        nameMatches[0];
                }
            }


            // =====================================================
            // 3. ParameterId
            // =====================================================

            return
                ParameterItems
                    .FirstOrDefault(
                        item =>
                            item.ParameterId ==
                            presetRule.ParameterId);
        }


        // =========================================================
        // Apply / Update
        // =========================================================

        private void ApplyButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_catalogLoaded)
            {
                return;
            }


            List<AutoParameterRule> rules =
                new List<AutoParameterRule>();


            HashSet<string> usedParameters =
                new HashSet<string>(
                    StringComparer.Ordinal);


            foreach (AutoParameterRuleRow row
                     in RuleRows)
            {
                if (!row.Enabled)
                {
                    continue;
                }


                InstanceParameterItem? parameterItem =
                    row.SelectedParameter;


                if (parameterItem == null)
                {
                    MessageBox.Show(
                        "사용하도록 체크된 Rule 중 Parameter가 선택되지 않은 항목이 있습니다.",
                        "WonClick - Auto Parameter",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }


                string value =
                    row.Value
                    ?? string.Empty;


                if (string.IsNullOrWhiteSpace(
                        value))
                {
                    MessageBox.Show(
                        "'"
                        +
                        parameterItem.Name
                        +
                        "' Parameter의 Value가 비어 있습니다.",
                        "WonClick - Auto Parameter",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }


                // =================================================
                // 동일 Parameter 중복 방지
                // =================================================

                string parameterKey;


                if (parameterItem.SharedParameterGuid.HasValue)
                {
                    parameterKey =
                        "GUID:"
                        +
                        parameterItem
                            .SharedParameterGuid
                            .Value;
                }
                else
                {
                    parameterKey =
                        "ID:"
                        +
                        parameterItem.ParameterId
                        +
                        "|NAME:"
                        +
                        parameterItem.Name;
                }


                if (!usedParameters.Add(
                        parameterKey))
                {
                    MessageBox.Show(
                        "'"
                        +
                        parameterItem.Name
                        +
                        "' Parameter가 두 번 이상 선택되어 있습니다."
                        +
                        "\n\n"
                        +
                        "동일 Parameter는 하나의 Rule만 사용할 수 있습니다.",
                        "WonClick - Auto Parameter",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }


                rules.Add(
                    new AutoParameterRule
                    {
                        Enabled =
                            true,

                        ParameterId =
                            parameterItem.ParameterId,

                        ParameterName =
                            parameterItem.Name,

                        SharedParameterGuid =
                            parameterItem.SharedParameterGuid,

                        Value =
                            value
                    });
            }


            if (rules.Count == 0)
            {
                MessageBox.Show(
                    "사용할 Auto Parameter Rule을 하나 이상 선택해 주세요.",
                    "WonClick - Auto Parameter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            // =====================================================
            // 현재 세팅 Hold
            // =====================================================

            SaveHeldSettingsSafe();


            // =====================================================
            // External Event
            // =====================================================

            _externalEventHandler.Document =
                _doc;


            _externalEventHandler.Rules =
                rules;


            _externalEventHandler.Request =
                AutoParameterRequest.EnableOrUpdate;


            ExternalEventRequest request =
                _externalEvent.Raise();


            if (request ==
                ExternalEventRequest.Denied)
            {
                MessageBox.Show(
                    "현재 Revit에서 Auto Parameter 요청을 실행할 수 없습니다.",
                    "WonClick - Auto Parameter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }


        // =========================================================
        // OFF
        // =========================================================

        private void DisableButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SaveHeldSettingsSafe();


            _externalEventHandler.Document =
                _doc;


            _externalEventHandler.Request =
                AutoParameterRequest.Disable;


            ExternalEventRequest request =
                _externalEvent.Raise();


            if (request ==
                ExternalEventRequest.Denied)
            {
                MessageBox.Show(
                    "현재 Revit에서 Auto Parameter 요청을 실행할 수 없습니다.",
                    "WonClick - Auto Parameter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }


        // =========================================================
        // Enable / Disable 완료
        // =========================================================

        private void ExternalEventCompleted(
            bool success,
            string message)
        {
            if (_windowClosed)
            {
                return;
            }


            UpdateStatus();


            if (!success)
            {
                MessageBox.Show(
                    message,
                    "WonClick - Auto Parameter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =========================================================
        // Status
        // =========================================================

        private void UpdateStatus()
        {
            if (_windowClosed)
            {
                return;
            }


            bool enabled =
                AutoParameterEngine.IsEnabled(
                    _doc);


            StatusTextBlock.Text =
                enabled
                    ? "ON"
                    : "OFF";


            if (_catalogLoaded)
            {
                ApplyButton.IsEnabled =
                    true;


                DisableButton.IsEnabled =
                    enabled;
            }
        }


        // =========================================================
        // Close
        // =========================================================

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SaveHeldSettingsSafe();


            Close();
        }


        // =========================================================
        // Closed
        // =========================================================

        private void AutoParameterWindow_Closed(
            object? sender,
            EventArgs e)
        {
            _windowClosed =
                true;


            SaveHeldSettingsSafe();


            if (ReferenceEquals(
                    Current,
                    this))
            {
                Current =
                    null;
            }


            try
            {
                _externalEvent.Dispose();
            }
            catch
            {
            }
        }
    }
}