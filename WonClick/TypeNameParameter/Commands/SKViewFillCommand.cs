using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace WonClick.TypeNameParameter.Commands
{
    [Transaction(TransactionMode.Manual)]
    public sealed class SKViewFillCommand
        : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            SKManagerCommand managerCommand =
                new SKManagerCommand();


            return
                managerCommand.Execute(
                    commandData,
                    ref message,
                    elements);
        }
    }
}