using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Windows.Interop;
using WonClick.TypeNameParameter.ExternalEvents;
using WonClick.TypeNameParameter.UI;

namespace WonClick.TypeNameParameter.Commands
{
    /// <summary>
    /// SK Manager Modeless Window를 연다.
    ///
    /// Ribbon에서는 이 Command 하나만 노출한다.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public sealed class SKManagerCommand
        : IExternalCommand
    {
        private static SKManagerWindow?
            _window;


        private static ExternalEvent?
            _externalEvent;


        private static SKManagerExternalEventHandler?
            _handler;


        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                // =================================================
                // 이미 열려 있으면 기존 Window 활성화
                // =================================================

                if (_window != null)
                {
                    if (_window.IsVisible)
                    {
                        _window.Activate();


                        _window.RequestRefresh();


                        return Result.Succeeded;
                    }


                    Cleanup();
                }


                // =================================================
                // External Event
                // =================================================

                _handler =
                    new SKManagerExternalEventHandler();


                _externalEvent =
                    ExternalEvent.Create(
                        _handler);


                _window =
                    new SKManagerWindow(
                        _handler,
                        _externalEvent);


                _handler.AttachWindow(
                    _window);


                // =================================================
                // Revit Main Window Owner
                // =================================================

                try
                {
                    WindowInteropHelper helper =
                        new WindowInteropHelper(
                            _window);


                    helper.Owner =
                        commandData
                            .Application
                            .MainWindowHandle;
                }
                catch
                {
                }


                _window.Closed +=
                    Window_Closed;


                _window.Show();


                _window.RequestRefresh();


                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Cleanup();


                TaskDialog.Show(
                    "WonClick - SK Manager",
                    "SK Manager를 여는 중 오류가 발생했습니다."
                    +
                    "\n\n"
                    +
                    ex.Message);


                return Result.Failed;
            }
        }


        private static void Window_Closed(
            object? sender,
            EventArgs e)
        {
            Cleanup();
        }


        private static void Cleanup()
        {
            try
            {
                _externalEvent?.Dispose();
            }
            catch
            {
            }


            _window =
                null;


            _externalEvent =
                null;


            _handler =
                null;
        }
    }
}
