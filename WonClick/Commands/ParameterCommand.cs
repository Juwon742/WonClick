using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using WonClick.Parameter.Models;
using WonClick.Parameter.Services;
using WonClick.Parameter.Views;

namespace WonClick.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ParameterCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIApplication uiApplication =
                    commandData.Application;


                UIDocument? uiDocument =
                    uiApplication.ActiveUIDocument;


                // =================================================
                // ACTIVE DOCUMENT
                // =================================================

                if (uiDocument == null)
                {
                    return Result.Cancelled;
                }


                Document document =
                    uiDocument.Document;


                View activeView =
                    document.ActiveView;


                // =================================================
                // 3D VIEW
                // =================================================
                //
                // Parameter 기능의 유일한 안내 TaskDialog입니다.
                //
                // Parameter Manager 자체를 열 수 없는 실행 조건이므로
                // 초보 사용자도 바로 원인을 알 수 있도록
                // 이 메시지만 팝업으로 유지합니다.
                //
                // =================================================

                if (activeView is not View3D view3D)
                {
                    TaskDialog.Show(
                        "WonClick - Parameter",
                        "3D View에서 실행하세요.\n\n");


                    return Result.Cancelled;
                }


                // =================================================
                // VIEW TEMPLATE
                // =================================================

                if (view3D.IsTemplate)
                {
                    return Result.Cancelled;
                }


                // =================================================
                // VISIBLE MODEL ELEMENTS
                // =================================================

                List<Element> visibleElements =
                    ElementInfoService
                        .GetVisibleModelElements(
                            document,
                            view3D);


                // =================================================
                // FAST PARAMETER DISCOVERY
                // =================================================

                List<ParameterSelectionItem>
                    selectionItems =
                        ParameterDiscoveryService
                            .DiscoverParameters(
                                document,
                                visibleElements);


                foreach (ParameterSelectionItem item
                         in selectionItems)
                {
                    item.UiGroup =
                        ParameterGroupService
                            .GetUiGroup(
                                item);
                }


                // =================================================
                // SUMMARY
                // =================================================

                int uniqueTotalParameterCount =
                    selectionItems.Count;


                int uniqueInstanceParameterCount =
                    selectionItems.Count(
                        item =>
                            !item.IsTypeParameter);


                int uniqueTypeParameterCount =
                    selectionItems.Count(
                        item =>
                            item.IsTypeParameter);


                int textGroupCount =
                    selectionItems.Count(
                        item =>
                            item.UiGroup == "Text");


                int setGroupCount =
                    selectionItems.Count(
                        item =>
                            item.UiGroup == "Set");


                int constructionGroupCount =
                    selectionItems.Count(
                        item =>
                            item.UiGroup
                            == "Construction");


                int dimensionsGroupCount =
                    selectionItems.Count(
                        item =>
                            item.UiGroup
                            == "Dimensions");


                int otherGroupCount =
                    selectionItems.Count(
                        item =>
                            item.UiGroup == "Other");


                // =================================================
                // PARAMETER MANAGER
                // =================================================

                ParameterManagerWindow window =
                    new ParameterManagerWindow(
                        document,
                        document.Title,
                        view3D.Name,
                        visibleElements,

                        uniqueTotalParameterCount,
                        uniqueInstanceParameterCount,
                        uniqueTypeParameterCount,

                        textGroupCount,
                        setGroupCount,
                        constructionGroupCount,
                        dimensionsGroupCount,
                        otherGroupCount,

                        selectionItems);


                // =================================================
                // REVIT OWNER
                // =================================================
                //
                // Revit 메인 창을 Owner로 지정합니다.
                //
                // Parameter Manager가 열려 있는 동안:
                //
                // - Revit 조작 불가
                // - Parameter Manager가 Revit 뒤로 숨는 현상 방지
                // - Revit을 클릭하려 하면 기존 UI가 전면 유지
                //
                // =================================================

                WindowInteropHelper windowHelper =
                    new WindowInteropHelper(
                        window);


                windowHelper.Owner =
                    uiApplication.MainWindowHandle;


                window.ShowDialog();


                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                // =================================================
                // UNEXPECTED ERROR
                // =================================================
                //
                // 별도 TaskDialog를 추가하지 않습니다.
                // Revit ExternalCommand에는 오류 문자열만 전달합니다.
                //
                // =================================================

                message =
                    ex.Message;


                return Result.Failed;
            }
        }
    }
}