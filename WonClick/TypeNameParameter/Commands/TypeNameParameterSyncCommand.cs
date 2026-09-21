using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using WonClick.TypeNameParameter.Models;
using WonClick.TypeNameParameter.Services;

namespace WonClick.TypeNameParameter.Commands
{
    // =============================================================
    // Entire Project
    // =============================================================

    [Transaction(TransactionMode.Manual)]
    public class TypeNameParameterSyncCommand
        : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            return
                TypeNameParameterSyncRunner.Run(
                    commandData,
                    TypeNameParameterSyncScope.EntireProject);
        }
    }


    // =============================================================
    // Current View
    // =============================================================

    [Transaction(TransactionMode.Manual)]
    public class TypeNameParameterSyncViewCommand
        : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            return
                TypeNameParameterSyncRunner.Run(
                    commandData,
                    TypeNameParameterSyncScope.CurrentView);
        }
    }


    // =============================================================
    // Selection
    // =============================================================

    [Transaction(TransactionMode.Manual)]
    public class TypeNameParameterSyncSelectionCommand
        : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            return
                TypeNameParameterSyncRunner.Run(
                    commandData,
                    TypeNameParameterSyncScope.Selection);
        }
    }


    internal enum TypeNameParameterSyncScope
    {
        EntireProject,

        CurrentView,

        Selection
    }


    internal static class TypeNameParameterSyncRunner
    {
        public static Result Run(
            ExternalCommandData commandData,
            TypeNameParameterSyncScope scope)
        {
            UIApplication uiApplication =
                commandData.Application;


            UIDocument? uiDocument =
                uiApplication.ActiveUIDocument;


            if (uiDocument == null)
            {
                TaskDialog.Show(
                    "WonClick - SK Sync",
                    "활성화된 Revit 문서가 없습니다.");


                return Result.Cancelled;
            }


            Document document =
                uiDocument.Document;


            ICollection<ElementId>? selectedIds =
                null;


            if (scope ==
                TypeNameParameterSyncScope.Selection)
            {
                selectedIds =
                    uiDocument
                        .Selection
                        .GetElementIds();


                if (selectedIds.Count == 0)
                {
                    TaskDialog.Show(
                        "WonClick - SK Sync",
                        "선택된 Element가 없습니다."
                        +
                        "\n\n"
                        +
                        "먼저 Sync할 Element를 선택한 후 "
                        +
                        "SK Select를 실행해 주세요.");


                    return Result.Cancelled;
                }
            }


            Transaction? transaction =
                null;


            Stopwatch totalWatch =
                Stopwatch.StartNew();


            try
            {
                string transactionName =
                    GetTransactionName(
                        scope);


                transaction =
                    new Transaction(
                        document,
                        transactionName);


                transaction.Start();


                TypeNameParameterBatchResult result;


                Stopwatch syncWatch =
                    Stopwatch.StartNew();


                int insulationChanged;


                switch (scope)
                {
                    case TypeNameParameterSyncScope.CurrentView:

                        result =
                            TypeNameParameterService
                                .SyncView(
                                    document,
                                    document.ActiveView.Id);


                        insulationChanged =
                            InsulationParameterService
                                .SyncView(
                                    document,
                                    document.ActiveView.Id);

                        break;


                    case TypeNameParameterSyncScope.Selection:

                        result =
                            TypeNameParameterService
                                .SyncElements(
                                    document,
                                    selectedIds!);


                        insulationChanged =
                            InsulationParameterService
                                .SyncElements(
                                    document,
                                    selectedIds!);

                        break;


                    default:

                        result =
                            TypeNameParameterService
                                .SyncDocument(
                                    document);


                        insulationChanged =
                            InsulationParameterService
                                .SyncDocument(
                                    document);

                        break;
                }


                syncWatch.Stop();


                // 일반 Sync + Insulation Sync 전체 시간을 반영
                result.ElapsedMilliseconds =
                    syncWatch.ElapsedMilliseconds;


                Stopwatch commitWatch =
                    Stopwatch.StartNew();


                transaction.Commit();


                commitWatch.Stop();


                totalWatch.Stop();


                result.CommitMilliseconds =
                    commitWatch.ElapsedMilliseconds;


                result.TotalMilliseconds =
                    totalWatch.ElapsedMilliseconds;


                ShowResult(
                    scope,
                    result,
                    insulationChanged);


                return Result.Succeeded;
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


                TaskDialog.Show(
                    "WonClick - SK Sync",
                    "SK Sync 실행 중 오류가 발생했습니다."
                    +
                    "\n\n"
                    +
                    ex.Message);


                return Result.Failed;
            }
            finally
            {
                transaction?.Dispose();
            }
        }


        private static string GetTransactionName(
            TypeNameParameterSyncScope scope)
        {
            return scope switch
            {
                TypeNameParameterSyncScope.CurrentView
                    =>
                    "WonClick - SK Sync View",

                TypeNameParameterSyncScope.Selection
                    =>
                    "WonClick - SK Sync Selection",

                _
                    =>
                    "WonClick - SK Sync All"
            };
        }


        private static void ShowResult(
            TypeNameParameterSyncScope scope,
            TypeNameParameterBatchResult result,
            int insulationChanged)
        {
            string scopeName =
                scope switch
                {
                    TypeNameParameterSyncScope.CurrentView
                        =>
                        "Current View",

                    TypeNameParameterSyncScope.Selection
                        =>
                        "Selected Elements",

                    _
                        =>
                        "Entire Project"
                };


            double syncSeconds =
                result.ElapsedMilliseconds
                /
                1000.0;


            double commitSeconds =
                result.CommitMilliseconds
                /
                1000.0;


            double totalSeconds =
                result.TotalMilliseconds
                /
                1000.0;


            TaskDialog dialog =
                new TaskDialog(
                    "WonClick - SK Sync");


            dialog.MainInstruction =
                "SK Parameter 동기화가 완료되었습니다.";


            if (result.TargetCategories == 0)
            {
                dialog.MainContent =
                    "SK_ITEM / SK_MATE / SK_SIZE의 "
                    +
                    "Instance Parameter Binding Category를 "
                    +
                    "찾지 못했습니다.";


                dialog.Show();

                return;
            }


            dialog.MainContent =
                "Sync Scope : "
                +
                scopeName
                +
                "\n\n"
                +
                "SK 대상 Category : "
                +
                result.TargetCategories
                +
                "\n"
                +
                "검색 Element : "
                +
                result.ScannedElements
                +
                "\n"
                +
                "\",\" 없음 Skip : "
                +
                result.SkippedNoComma
                +
                "\n"
                +
                "실제 SK 대상 Element : "
                +
                result.TargetElements
                +
                "\n"
                +
                "변경 Element : "
                +
                result.UpdatedElements
                +
                "\n"
                +
                "변경 Insulation : "
                +
                insulationChanged
                +
                "\n\n"
                +
                "SK_ITEM : "
                +
                result.ItemUpdated
                +
                "\n"
                +
                "SK_MATE : "
                +
                result.MateUpdated
                +
                "\n"
                +
                "SK_SIZE : "
                +
                result.SizeUpdated
                +
                "\n"
                +
                "일반 변경 Parameter : "
                +
                result.UpdatedParameters
                +
                "\n\n"
                +
                "분석 Type 수 : "
                +
                result.TypeCacheEntries
                +
                "\n\n"
                +
                "Scan / Set : "
                +
                syncSeconds.ToString(
                    "0.000")
                +
                " sec"
                +
                "\n"
                +
                "Commit : "
                +
                commitSeconds.ToString(
                    "0.000")
                +
                " sec"
                +
                "\n"
                +
                "Total : "
                +
                totalSeconds.ToString(
                    "0.000")
                +
                " sec";


            if (result.ErrorElements > 0)
            {
                dialog.MainContent +=
                    "\n\n"
                    +
                    "처리 오류 Element : "
                    +
                    result.ErrorElements;
            }


            dialog.Show();
        }
    }
}
