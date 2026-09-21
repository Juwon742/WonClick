using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using WonClick.TypeNameParameter.Models;

namespace WonClick.TypeNameParameter.Services
{
    public enum SKManagerSyncScope
    {
        Selection,
        CurrentView,
        EntireProject
    }


    public sealed class SKManagerStatusInfo
    {
        public bool AutoSyncEnabled
        {
            get;
            set;
        }


        public string ActiveViewName
        {
            get;
            set;
        } =
            string.Empty;


        public int SelectedElementCount
        {
            get;
            set;
        }


        public int ApproximateCandidateCount
        {
            get;
            set;
        }


        public bool Is3DView
        {
            get;
            set;
        }


        public bool IsSectionBoxActive
        {
            get;
            set;
        }


        public IReadOnlyList<string> ViewFillParameterNames
        {
            get;
            set;
        } =
            Array.Empty<string>();
    }


    public sealed class SKManagerOperationResult
    {
        public bool Succeeded
        {
            get;
            set;
        }


        public string Message
        {
            get;
            set;
        } =
            string.Empty;
    }


    /// <summary>
    /// SK Manager UI에서 사용하는 Revit API 작업을 모아둔다.
    ///
    /// Modeless Window는 Revit API를 직접 호출하지 않고
    /// ExternalEvent를 통해 이 Service를 실행한다.
    /// </summary>
    public static class SKManagerService
    {
        private const string ItemParameterName =
            "SK_ITEM";


        private const string MateParameterName =
            "SK_MATE";


        private const string SizeParameterName =
            "SK_SIZE";


        // =========================================================
        // Status
        // =========================================================

        public static SKManagerStatusInfo GetStatus(
            UIApplication uiApplication)
        {
            SKManagerStatusInfo status =
                new SKManagerStatusInfo
                {
                    AutoSyncEnabled =
                        SKAutoSyncSettingsService.IsEnabled
                };


            UIDocument? uiDocument =
                uiApplication.ActiveUIDocument;


            if (uiDocument == null)
            {
                status.ActiveViewName =
                    "활성 문서 없음";


                return status;
            }


            Document doc =
                uiDocument.Document;


            status.ActiveViewName =
                doc.ActiveView?.Name
                ??
                string.Empty;


            try
            {
                status.SelectedElementCount =
                    uiDocument
                        .Selection
                        .GetElementIds()
                        .Count;
            }
            catch
            {
                status.SelectedElementCount =
                    0;
            }


            status.ApproximateCandidateCount =
                GetApproximateCandidateCount(
                    doc);


            if (doc.ActiveView
                is View3D view3D
                &&
                !view3D.IsTemplate)
            {
                status.Is3DView =
                    true;


                try
                {
                    status.IsSectionBoxActive =
                        view3D.IsSectionBoxActive;
                }
                catch
                {
                    status.IsSectionBoxActive =
                        false;
                }


                if (status.IsSectionBoxActive)
                {
                    try
                    {
                        status.ViewFillParameterNames =
                            SKViewFillService
                                .GetAvailableParameterNames(
                                    doc,
                                    view3D.Id);
                    }
                    catch
                    {
                        status.ViewFillParameterNames =
                            Array.Empty<string>();
                    }
                }
            }


            return status;
        }


        // =========================================================
        // Auto Toggle
        // =========================================================

        public static SKManagerOperationResult ToggleAuto(
            UIApplication uiApplication)
        {
            bool enabled =
                SKAutoSyncSettingsService
                    .Toggle();


            if (enabled)
            {
                Document? doc =
                    uiApplication
                        .ActiveUIDocument?
                        .Document;


                if (doc != null)
                {
                    TypeNameParameterAutoSyncEngine
                        .RefreshDocument(
                            doc);
                }
            }


            return
                new SKManagerOperationResult
                {
                    Succeeded =
                        true,

                    Message =
                        enabled
                            ?
                            "SK Auto Sync가 ON으로 변경되었습니다."
                            :
                            "SK Auto Sync가 OFF로 변경되었습니다."
                };
        }


        // =========================================================
        // Manual Sync
        // =========================================================

        public static SKManagerOperationResult RunSync(
            UIApplication uiApplication,
            SKManagerSyncScope scope)
        {
            UIDocument? uiDocument =
                uiApplication.ActiveUIDocument;


            if (uiDocument == null)
            {
                return Failure(
                    "활성화된 Revit 문서가 없습니다.");
            }


            Document doc =
                uiDocument.Document;


            ICollection<ElementId>? selectedIds =
                null;


            if (scope ==
                SKManagerSyncScope.Selection)
            {
                selectedIds =
                    uiDocument
                        .Selection
                        .GetElementIds();


                if (selectedIds.Count == 0)
                {
                    return Failure(
                        "선택된 Element가 없습니다.");
                }
            }


            Transaction? transaction =
                null;


            Stopwatch totalWatch =
                Stopwatch.StartNew();


            try
            {
                string transactionName =
                    scope switch
                    {
                        SKManagerSyncScope.Selection
                            =>
                            "WonClick - SK Sync Selection",

                        SKManagerSyncScope.CurrentView
                            =>
                            "WonClick - SK Sync View",

                        _
                            =>
                            "WonClick - SK Sync All"
                    };


                transaction =
                    new Transaction(
                        doc,
                        transactionName);


                transaction.Start();


                Stopwatch syncWatch =
                    Stopwatch.StartNew();


                TypeNameParameterBatchResult result;


                int insulationChanged;


                switch (scope)
                {
                    case SKManagerSyncScope.Selection:

                        result =
                            TypeNameParameterService
                                .SyncElements(
                                    doc,
                                    selectedIds!);


                        insulationChanged =
                            InsulationParameterService
                                .SyncElements(
                                    doc,
                                    selectedIds!);

                        break;


                    case SKManagerSyncScope.CurrentView:

                        result =
                            TypeNameParameterService
                                .SyncView(
                                    doc,
                                    doc.ActiveView.Id);


                        insulationChanged =
                            InsulationParameterService
                                .SyncView(
                                    doc,
                                    doc.ActiveView.Id);

                        break;


                    default:

                        result =
                            TypeNameParameterService
                                .SyncDocument(
                                    doc);


                        insulationChanged =
                            InsulationParameterService
                                .SyncDocument(
                                    doc);

                        break;
                }


                syncWatch.Stop();


                Stopwatch commitWatch =
                    Stopwatch.StartNew();


                transaction.Commit();


                commitWatch.Stop();


                totalWatch.Stop();


                result.ElapsedMilliseconds =
                    syncWatch.ElapsedMilliseconds;


                result.CommitMilliseconds =
                    commitWatch.ElapsedMilliseconds;


                result.TotalMilliseconds =
                    totalWatch.ElapsedMilliseconds;


                return
                    new SKManagerOperationResult
                    {
                        Succeeded =
                            true,

                        Message =
                            BuildSyncResultMessage(
                                scope,
                                result,
                                insulationChanged)
                    };
            }
            catch (Exception ex)
            {
                totalWatch.Stop();


                try
                {
                    if (transaction != null
                        &&
                        transaction.GetStatus() ==
                        TransactionStatus.Started)
                    {
                        transaction.RollBack();
                    }
                }
                catch
                {
                }


                return Failure(
                    "SK Sync 실행 중 오류가 발생했습니다.\n\n"
                    +
                    ex.Message);
            }
            finally
            {
                transaction?.Dispose();
            }
        }


        // =========================================================
        // View Fill
        // =========================================================

        public static SKManagerOperationResult ApplyViewFill(
            UIApplication uiApplication,
            IReadOnlyList<SKViewFillRow> rows)
        {
            UIDocument? uiDocument =
                uiApplication.ActiveUIDocument;


            if (uiDocument == null)
            {
                return Failure(
                    "활성화된 Revit 문서가 없습니다.");
            }


            Document doc =
                uiDocument.Document;


            if (doc.ActiveView
                is not View3D view3D
                ||
                view3D.IsTemplate)
            {
                return Failure(
                    "View Fill은 3D View에서만 사용할 수 있습니다.");
            }


            bool sectionBoxActive;


            try
            {
                sectionBoxActive =
                    view3D.IsSectionBoxActive;
            }
            catch
            {
                sectionBoxActive =
                    false;
            }


            if (!sectionBoxActive)
            {
                return Failure(
                    "현재 3D View의 Section Box가 활성화되어 있지 않습니다.");
            }


            if (rows == null
                ||
                rows.Count == 0)
            {
                return Failure(
                    "적용할 View Fill 항목이 없습니다.");
            }


            Transaction? transaction =
                null;


            try
            {
                transaction =
                    new Transaction(
                        doc,
                        "WonClick - SK Sync View Fill");


                transaction.Start();


                SKViewFillResult result =
                    SKViewFillService.Apply(
                        doc,
                        view3D.Id,
                        rows);


                transaction.Commit();


                return
                    new SKManagerOperationResult
                    {
                        Succeeded =
                            true,

                        Message =
                            "View Fill 완료\n"
                            +
                            "검색 Element : "
                            +
                            result.ScannedElements
                            +
                            "\n숨김 객체 Skip : "
                            +
                            result.HiddenElementsSkipped
                            +
                            "\n변경 Element : "
                            +
                            result.UpdatedElements
                            +
                            "\n변경 Parameter : "
                            +
                            result.UpdatedParameters
                            +
                            "\n연동 Insulation : "
                            +
                            result.UpdatedInsulations
                    };
            }
            catch (Exception ex)
            {
                try
                {
                    if (transaction != null
                        &&
                        transaction.GetStatus() ==
                        TransactionStatus.Started)
                    {
                        transaction.RollBack();
                    }
                }
                catch
                {
                }


                return Failure(
                    "View Fill 실행 중 오류가 발생했습니다.\n\n"
                    +
                    ex.Message);
            }
            finally
            {
                transaction?.Dispose();
            }
        }


        // =========================================================
        // Candidate Count
        // =========================================================

        private static int GetApproximateCandidateCount(
            Document doc)
        {
            try
            {
                List<ElementId> categoryIds =
                    GetTargetCategoryIds(
                        doc);


                if (categoryIds.Count == 0)
                {
                    return 0;
                }


                ElementMulticategoryFilter filter =
                    new ElementMulticategoryFilter(
                        categoryIds);


                return
                    new FilteredElementCollector(
                        doc)
                        .WhereElementIsNotElementType()
                        .WherePasses(
                            filter)
                        .GetElementCount();
            }
            catch
            {
                return 0;
            }
        }


        private static List<ElementId> GetTargetCategoryIds(
            Document doc)
        {
            List<ElementId> result =
                new List<ElementId>();


            HashSet<long> added =
                new HashSet<long>();


            DefinitionBindingMapIterator iterator =
                doc
                    .ParameterBindings
                    .ForwardIterator();


            iterator.Reset();


            while (iterator.MoveNext())
            {
                Definition? definition =
                    iterator.Key;


                if (definition == null
                    ||
                    !IsCoreSKParameterName(
                        definition.Name))
                {
                    continue;
                }


                if (iterator.Current
                    is not InstanceBinding instanceBinding)
                {
                    continue;
                }


                foreach (Category category
                         in instanceBinding.Categories)
                {
                    if (category == null
                        ||
                        category.CategoryType !=
                        CategoryType.Model
                        ||
                        IsExcludedSystemCategory(
                            category.Id))
                    {
                        continue;
                    }


                    if (!added.Add(
                            category.Id.Value))
                    {
                        continue;
                    }


                    result.Add(
                        category.Id);
                }
            }


            return result;
        }


        private static bool IsCoreSKParameterName(
            string parameterName)
        {
            return
                string.Equals(
                    parameterName,
                    ItemParameterName,
                    StringComparison.Ordinal)

                ||

                string.Equals(
                    parameterName,
                    MateParameterName,
                    StringComparison.Ordinal)

                ||

                string.Equals(
                    parameterName,
                    SizeParameterName,
                    StringComparison.Ordinal);
        }


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
        // Message
        // =========================================================

        private static string BuildSyncResultMessage(
            SKManagerSyncScope scope,
            TypeNameParameterBatchResult result,
            int insulationChanged)
        {
            string scopeText =
                scope switch
                {
                    SKManagerSyncScope.Selection
                        =>
                        "Selected Elements",

                    SKManagerSyncScope.CurrentView
                        =>
                        "Current View",

                    _
                        =>
                        "Entire Project"
                };


            return
                "Sync 완료 - "
                +
                scopeText
                +
                "\n"
                +
                "검색 Element : "
                +
                result.ScannedElements
                +
                "\n, 없음 Skip : "
                +
                result.SkippedNoComma
                +
                "\n변경 Element : "
                +
                result.UpdatedElements
                +
                "\n변경 Insulation : "
                +
                insulationChanged
                +
                "\n전체 변경 Parameter : "
                +
                result.UpdatedParameters
                +
                "\nScan / Set : "
                +
                (result.ElapsedMilliseconds / 1000.0)
                    .ToString(
                        "0.000")
                +
                " sec"
                +
                "\nCommit : "
                +
                (result.CommitMilliseconds / 1000.0)
                    .ToString(
                        "0.000")
                +
                " sec"
                +
                "\nTotal : "
                +
                (result.TotalMilliseconds / 1000.0)
                    .ToString(
                        "0.000")
                +
                " sec";
        }


        private static SKManagerOperationResult Failure(
            string message)
        {
            return
                new SKManagerOperationResult
                {
                    Succeeded =
                        false,

                    Message =
                        message
                };
        }
    }
}
