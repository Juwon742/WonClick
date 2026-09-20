using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using WonClick.AutoParameter.Services;

namespace WonClick.App
{
    public class WonClickApplication : IExternalApplication
    {
        public Result OnStartup(
            UIControlledApplication application)
        {
            try
            {
                // =================================================
                // Auto Parameter Engine 초기화
                // =================================================

                AutoParameterEngine.Initialize(
                    application.ActiveAddInId);


                // =================================================
                // Auto Parameter Catalog Cache 감시
                //
                // DocumentChanged는 읽기 전용으로만 사용.
                //
                // Parameter Catalog 구조에 변화가 있는지를
                // 판단해서 Cache Dirty 여부만 관리한다.
                // =================================================

                application
                    .ControlledApplication
                    .DocumentChanged +=
                        ControlledApplication_DocumentChanged;


                // =================================================
                // Ribbon
                // =================================================

                const string tabName =
                    "WonClick";


                const string panelName =
                    "Model";


                // =================================================
                // Ribbon Tab
                // =================================================

                try
                {
                    application.CreateRibbonTab(
                        tabName);
                }
                catch
                {
                    // 이미 존재하면 그대로 사용
                }


                // =================================================
                // Model Panel
                // =================================================

                RibbonPanel? modelPanel =
                    null;


                foreach (RibbonPanel panel
                         in application.GetRibbonPanels(
                             tabName))
                {
                    if (panel.Name ==
                        panelName)
                    {
                        modelPanel =
                            panel;

                        break;
                    }
                }


                if (modelPanel == null)
                {
                    modelPanel =
                        application
                            .CreateRibbonPanel(
                                tabName,
                                panelName);
                }


                // =================================================
                // Assembly
                // =================================================

                string assemblyPath =
                    Assembly
                        .GetExecutingAssembly()
                        .Location;


                // =================================================
                // Parameter
                //
                // Excel Export / Import
                // =================================================

                PushButtonData parameterButtonData =
                    new PushButtonData(
                        "WonClick.Parameter",
                        "Parameter",
                        assemblyPath,
                        "WonClick.Commands.ParameterCommand");


                PushButton? parameterButton =
                    modelPanel.AddItem(
                        parameterButtonData)
                    as PushButton;


                if (parameterButton != null)
                {
                    parameterButton.ToolTip =
                        "현재 3D View의 Parameter를 관리합니다.";


                    // =============================================
                    // Parameter 16 x 16 Icon
                    // =============================================

                    parameterButton.Image =
                        LoadEmbeddedImage(
                            "WonClick.Resources.Icons.Parameter16.png");


                    // =============================================
                    // Parameter 32 x 32 Icon
                    // =============================================

                    parameterButton.LargeImage =
                        LoadEmbeddedImage(
                            "WonClick.Resources.Icons.Parameter32.png");
                }


                // =================================================
                // Auto Parameter
                //
                // 새로 생성되는 객체의 Parameter에
                // 지정된 Value 자동 입력
                // =================================================

                PushButtonData autoParameterButtonData =
                    new PushButtonData(
                        "WonClick.AutoParameter",
                        "Auto\nParameter",
                        assemblyPath,
                        "WonClick.AutoParameter.Commands.AutoParameterCommand");


                PushButton? autoParameterButton =
                    modelPanel.AddItem(
                        autoParameterButtonData)
                    as PushButton;


                if (autoParameterButton != null)
                {
                    autoParameterButton.ToolTip =
                        "새로 생성되는 객체의 Instance Parameter에 "
                        +
                        "지정한 Value를 자동으로 입력합니다.";


                    // =============================================
                    // Auto Parameter 16 x 16 Icon
                    // =============================================

                    autoParameterButton.Image =
                        LoadEmbeddedImage(
                            "WonClick.Resources.Icons.AutoParameter16.png");


                    // =============================================
                    // Auto Parameter 32 x 32 Icon
                    // =============================================

                    autoParameterButton.LargeImage =
                        LoadEmbeddedImage(
                            "WonClick.Resources.Icons.AutoParameter32.png");
                }


                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "WonClick",
                    "WonClick 시작 중 오류가 발생했습니다."
                    +
                    "\n\n"
                    +
                    ex.Message);


                return Result.Failed;
            }
        }


        // =========================================================
        // Embedded Resource Image Loader
        // =========================================================

        private static BitmapImage LoadEmbeddedImage(
            string resourceName)
        {
            Assembly assembly =
                Assembly.GetExecutingAssembly();


            using Stream? stream =
                assembly.GetManifestResourceStream(
                    resourceName);


            if (stream == null)
            {
                throw new InvalidOperationException(
                    "아이콘 리소스를 찾을 수 없습니다."
                    +
                    "\n\n"
                    +
                    resourceName);
            }


            BitmapImage bitmapImage =
                new BitmapImage();


            bitmapImage.BeginInit();


            bitmapImage.CacheOption =
                BitmapCacheOption.OnLoad;


            bitmapImage.StreamSource =
                stream;


            bitmapImage.EndInit();


            bitmapImage.Freeze();


            return bitmapImage;
        }


        // =========================================================
        // Document Changed
        //
        // EventHandler<DocumentChangedEventArgs>의 sender는
        // nullable object이므로 object? 사용
        // =========================================================

        private static void ControlledApplication_DocumentChanged(
            object? sender,
            DocumentChangedEventArgs e)
        {
            try
            {
                AutoParameterCatalogService
                    .NotifyDocumentChanged(
                        e);
            }
            catch
            {
                // Catalog Cache 관련 오류가
                // 기존 WonClick / Revit 기능에 영향을 주지 않도록
                // 무시
            }
        }


        // =========================================================
        // Shutdown
        // =========================================================

        public Result OnShutdown(
            UIControlledApplication application)
        {
            try
            {
                application
                    .ControlledApplication
                    .DocumentChanged -=
                        ControlledApplication_DocumentChanged;
            }
            catch
            {
            }


            return Result.Succeeded;
        }
    }
}