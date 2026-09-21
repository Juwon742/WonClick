using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using WonClick.TypeNameParameter.Models;
using WonClick.TypeNameParameter.Services;
using WonClick.TypeNameParameter.UI;

namespace WonClick.TypeNameParameter.ExternalEvents
{
    public enum SKManagerRequestType
    {
        None,
        Refresh,
        ToggleAuto,
        SyncSelection,
        SyncCurrentView,
        SyncEntireProject,
        ApplyViewFill
    }


    /// <summary>
    /// Modeless SK Manager Window와 Revit API Context를 연결한다.
    /// </summary>
    public sealed class SKManagerExternalEventHandler
        : IExternalEventHandler
    {
        private SKManagerRequestType
            _request =
                SKManagerRequestType.None;


        private IReadOnlyList<SKViewFillRow>
            _viewFillRows =
                Array.Empty<SKViewFillRow>();


        private SKManagerWindow?
            _window;


        // =========================================================
        // Attach
        // =========================================================

        public void AttachWindow(
            SKManagerWindow window)
        {
            _window =
                window;
        }


        // =========================================================
        // Requests
        // =========================================================

        public void RequestRefresh()
        {
            _request =
                SKManagerRequestType.Refresh;
        }


        public void RequestToggleAuto()
        {
            _request =
                SKManagerRequestType.ToggleAuto;
        }


        public void RequestSyncSelection()
        {
            _request =
                SKManagerRequestType.SyncSelection;
        }


        public void RequestSyncCurrentView()
        {
            _request =
                SKManagerRequestType.SyncCurrentView;
        }


        public void RequestSyncEntireProject()
        {
            _request =
                SKManagerRequestType.SyncEntireProject;
        }


        public void RequestApplyViewFill(
            IReadOnlyList<SKViewFillRow> rows)
        {
            List<SKViewFillRow> copiedRows =
                new List<SKViewFillRow>();


            foreach (SKViewFillRow row
                     in rows)
            {
                if (row == null)
                {
                    continue;
                }


                copiedRows.Add(
                    new SKViewFillRow
                    {
                        ParameterName =
                            row.ParameterName,

                        Value =
                            row.Value
                    });
            }


            _viewFillRows =
                copiedRows;


            _request =
                SKManagerRequestType.ApplyViewFill;
        }


        // =========================================================
        // IExternalEventHandler
        // =========================================================

        public void Execute(
            UIApplication app)
        {
            SKManagerRequestType request =
                _request;


            _request =
                SKManagerRequestType.None;


            try
            {
                switch (request)
                {
                    case SKManagerRequestType.Refresh:

                        Refresh(
                            app);

                        break;


                    case SKManagerRequestType.ToggleAuto:

                        ShowOperationResult(
                            SKManagerService
                                .ToggleAuto(
                                    app));


                        Refresh(
                            app);

                        break;


                    case SKManagerRequestType.SyncSelection:

                        ShowOperationResult(
                            SKManagerService
                                .RunSync(
                                    app,
                                    SKManagerSyncScope.Selection));


                        Refresh(
                            app);

                        break;


                    case SKManagerRequestType.SyncCurrentView:

                        ShowOperationResult(
                            SKManagerService
                                .RunSync(
                                    app,
                                    SKManagerSyncScope.CurrentView));


                        Refresh(
                            app);

                        break;


                    case SKManagerRequestType.SyncEntireProject:

                        ShowOperationResult(
                            SKManagerService
                                .RunSync(
                                    app,
                                    SKManagerSyncScope.EntireProject));


                        Refresh(
                            app);

                        break;


                    case SKManagerRequestType.ApplyViewFill:

                        ShowOperationResult(
                            SKManagerService
                                .ApplyViewFill(
                                    app,
                                    _viewFillRows));


                        Refresh(
                            app);

                        break;
                }
            }
            catch (Exception ex)
            {
                _window?.SetResult(
                    "오류가 발생했습니다.\n\n"
                    +
                    ex.Message,
                    false);
            }
            finally
            {
                _window?.SetBusy(
                    false);
            }
        }


        public string GetName()
        {
            return
                "WonClick SK Manager External Event";
        }


        // =========================================================
        // Helpers
        // =========================================================

        private void Refresh(
            UIApplication app)
        {
            try
            {
                if (app.ActiveUIDocument != null)
                {
                    TypeNameParameterAutoSyncEngine
                        .RefreshDocument(
                            app.ActiveUIDocument.Document);
                }
            }
            catch
            {
            }


            SKManagerStatusInfo status =
                SKManagerService
                    .GetStatus(
                        app);


            _window?.UpdateStatus(
                status);
        }


        private void ShowOperationResult(
            SKManagerOperationResult result)
        {
            _window?.SetResult(
                result.Message,
                result.Succeeded);
        }
    }
}
