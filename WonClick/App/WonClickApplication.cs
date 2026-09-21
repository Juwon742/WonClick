using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using WonClick.AutoParameter.Services;
using WonClick.TypeNameParameter.Services;

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
                // SK Auto Sync Engine 초기화
                // =================================================

                TypeNameParameterAutoSyncEngine.Initialize(
                    application.ActiveAddInId);


                // =================================================
                // Document Events
                // =================================================

                application
                    .ControlledApplication
                    .DocumentOpened +=
                        ControlledApplication_DocumentOpened;


                application
                    .ControlledApplication
                    .DocumentCreated +=
                        ControlledApplication_DocumentCreated;


                // =================================================
                // Auto Parameter Catalog Cache 감시
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


                try
                {
                    application.CreateRibbonTab(
                        tabName);
                }
                catch
                {
                    // 이미 존재
                }


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


                string assemblyPath =
                    Assembly
                        .GetExecutingAssembly()
                        .Location;


                // =================================================
                // Parameter
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


                    parameterButton.Image =
                        LoadEmbeddedImage(
                            "WonClick.Resources.Icons.Parameter16.png");


                    parameterButton.LargeImage =
                        LoadEmbeddedImage(
                            "WonClick.Resources.Icons.Parameter32.png");
                }


                // =================================================
                // Auto Parameter
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


                    autoParameterButton.Image =
                        LoadEmbeddedImage(
                            "WonClick.Resources.Icons.AutoParameter16.png");


                    autoParameterButton.LargeImage =
                        LoadEmbeddedImage(
                            "WonClick.Resources.Icons.AutoParameter32.png");
                }


                // =================================================
                // SK Manager
                //
                // 기존 SK All / View / Select / Auto / View Fill을
                // Ribbon에서 직접 노출하지 않고
                // 안전한 단일 Manager UI 안으로 통합한다.
                // =================================================

                modelPanel.AddSeparator();


                PushButtonData skManagerButtonData =
                    new PushButtonData(
                        "WonClick.SKManager",
                        "SK\nManager",
                        assemblyPath,
                        "WonClick.TypeNameParameter.Commands.SKManagerCommand");


                PushButton? skManagerButton =
                    modelPanel.AddItem(
                        skManagerButtonData)
                    as PushButton;


                if (skManagerButton != null)
                {
                    skManagerButton.ToolTip =
                        "SK Auto Sync, Selected/View/All Sync, "
                        +
                        "View Fill을 하나의 안전한 UI에서 관리합니다.";


                    skManagerButton.Image =
                        LoadEmbeddedImage(
                            "WonClick.Resources.Icons.AutoFill16.png");


                    skManagerButton.LargeImage =
                        LoadEmbeddedImage(
                            "WonClick.Resources.Icons.AutoFill32.png");
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
        // Document Opened
        // =========================================================

        private static void ControlledApplication_DocumentOpened(
            object? sender,
            DocumentOpenedEventArgs e)
        {
            try
            {
                TypeNameParameterAutoSyncEngine
                    .RegisterDocument(
                        e.Document);
            }
            catch
            {
            }
        }


        // =========================================================
        // Document Created
        // =========================================================

        private static void ControlledApplication_DocumentCreated(
            object? sender,
            DocumentCreatedEventArgs e)
        {
            try
            {
                TypeNameParameterAutoSyncEngine
                    .RegisterDocument(
                        e.Document);
            }
            catch
            {
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
        // AutoParameter Catalog
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
                    .DocumentOpened -=
                        ControlledApplication_DocumentOpened;
            }
            catch
            {
            }


            try
            {
                application
                    .ControlledApplication
                    .DocumentCreated -=
                        ControlledApplication_DocumentCreated;
            }
            catch
            {
            }


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
