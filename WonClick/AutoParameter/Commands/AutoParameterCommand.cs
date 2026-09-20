using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Windows.Interop;
using WonClick.AutoParameter.UI;

namespace WonClick.AutoParameter.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoParameterCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIApplication uiApp =
                    commandData.Application;


                UIDocument? uiDoc =
                    uiApp.ActiveUIDocument;


                if (uiDoc == null)
                {
                    TaskDialog.Show(
                        "WonClick",
                        "활성화된 Revit 문서가 없습니다.");

                    return Result.Cancelled;
                }


                Document doc =
                    uiDoc.Document;


                // =================================================
                // 이미 Auto Parameter 창이 열려 있는 경우
                // =================================================

                AutoParameterWindow? existingWindow =
                    AutoParameterWindow.Current;


                if (existingWindow != null)
                {
                    // 같은 Document면 기존 창 활성화
                    if (existingWindow.Document == doc)
                    {
                        if (existingWindow.WindowState ==
                            System.Windows.WindowState.Minimized)
                        {
                            existingWindow.WindowState =
                                System.Windows.WindowState.Normal;
                        }


                        existingWindow.Activate();

                        return Result.Succeeded;
                    }


                    // 다른 Document용 창이면 기존 창 종료
                    existingWindow.Close();
                }


                // =================================================
                // 새 Modeless Window 생성
                // =================================================

                AutoParameterWindow window =
                    new AutoParameterWindow(
                        doc);


                // =================================================
                // Revit Main Window를 Owner로 설정
                //
                // Auto Parameter 창을 띄워둔 상태에서도
                // Revit 화면을 자유롭게 조작할 수 있다.
                // =================================================

                WindowInteropHelper helper =
                    new WindowInteropHelper(
                        window);


                helper.Owner =
                    uiApp.MainWindowHandle;


                // =================================================
                // ShowDialog()가 아니라 Show()
                // =================================================

                window.Show();


                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "WonClick - Auto Parameter",
                    "Auto Parameter 실행 중 오류가 발생했습니다."
                    +
                    "\n\n"
                    +
                    ex.Message);


                return Result.Failed;
            }
        }
    }
}