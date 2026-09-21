using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using WonClick.TypeNameParameter.Services;

namespace WonClick.TypeNameParameter.Commands
{
    /// <summary>
    /// SK Auto Sync ON / OFF Toggle.
    ///
    /// OFF 상태에서도:
    /// SK All / SK View / SK Select / View Fill은
    /// 수동으로 계속 사용할 수 있다.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public sealed class SKAutoToggleCommand
        : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                bool enabled =
                    SKAutoSyncSettingsService
                        .Toggle();


                // ON 전환 시 현재 Document의 Trigger를
                // 한 번 다시 구성한다.
                if (enabled)
                {
                    Document? doc =
                        commandData
                            .Application
                            .ActiveUIDocument?
                            .Document;


                    if (doc != null)
                    {
                        TypeNameParameterAutoSyncEngine
                            .RefreshDocument(
                                doc);
                    }
                }


                TaskDialog.Show(
                    "WonClick - SK Auto",
                    enabled
                        ?
                        "SK Auto Sync가 ON으로 변경되었습니다."
                        +
                        "\n\n"
                        +
                        "신규 생성 / Type 변경 / Size·Angle 변경 / "
                        +
                        "Pipe·Duct Insulation 변경을 자동 추적합니다."
                        :
                        "SK Auto Sync가 OFF로 변경되었습니다."
                        +
                        "\n\n"
                        +
                        "SK All / SK View / SK Select / View Fill은 "
                        +
                        "계속 수동으로 사용할 수 있습니다.");


                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show(
                    "WonClick - SK Auto",
                    "SK Auto 상태 변경 중 오류가 발생했습니다."
                    +
                    "\n\n"
                    +
                    ex.Message);


                return Result.Failed;
            }
        }
    }
}
