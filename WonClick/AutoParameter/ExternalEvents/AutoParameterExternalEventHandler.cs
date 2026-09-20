using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using WonClick.AutoParameter.Models;
using WonClick.AutoParameter.Services;

namespace WonClick.AutoParameter.ExternalEvents
{
    public enum AutoParameterRequest
    {
        None,

        LoadCatalog,

        EnableOrUpdate,

        Disable
    }


    public class AutoParameterExternalEventHandler
        : IExternalEventHandler
    {
        // =========================================================
        // 요청
        // =========================================================

        public AutoParameterRequest Request
        {
            get;
            set;
        } =
            AutoParameterRequest.None;


        // =========================================================
        // 대상 Document
        // =========================================================

        public Document? Document
        {
            get;
            set;
        }


        // =========================================================
        // Apply / Update Rules
        // =========================================================

        public List<AutoParameterRule> Rules
        {
            get;
            set;
        } =
            new List<AutoParameterRule>();


        // =========================================================
        // Enable / Disable 완료 Callback
        // =========================================================

        public Action<bool, string>? Completed
        {
            get;
            set;
        }


        // =========================================================
        // Parameter Catalog 완료 Callback
        // =========================================================

        public Action<
            bool,
            string,
            AutoParameterCatalogResult?>?
            CatalogLoaded
        {
            get;
            set;
        }


        // =========================================================
        // Execute
        // =========================================================

        public void Execute(
            UIApplication app)
        {
            try
            {
                Document? doc =
                    Document;


                if (doc == null ||
                    !doc.IsValidObject)
                {
                    if (Request ==
                        AutoParameterRequest.LoadCatalog)
                    {
                        CatalogLoaded?.Invoke(
                            false,
                            "Revit 문서를 사용할 수 없습니다.",
                            null);
                    }
                    else
                    {
                        Completed?.Invoke(
                            false,
                            "Revit 문서를 사용할 수 없습니다.");
                    }


                    return;
                }


                switch (Request)
                {
                    // =================================================
                    // Parameter Catalog
                    // =================================================

                    case AutoParameterRequest.LoadCatalog:
                        {
                            AutoParameterCatalogResult result =
                                AutoParameterCatalogService.GetCatalog(
                                    doc);


                            CatalogLoaded?.Invoke(
                                true,
                                string.Empty,
                                result);


                            break;
                        }


                    // =================================================
                    // Enable / Update
                    // =================================================

                    case AutoParameterRequest.EnableOrUpdate:
                        {
                            if (Rules.Count == 0)
                            {
                                Completed?.Invoke(
                                    false,
                                    "적용할 Auto Parameter Rule이 없습니다.");

                                break;
                            }


                            AutoParameterEngine.Enable(
                                doc,
                                Rules);


                            Completed?.Invoke(
                                true,
                                "Auto Parameter가 적용되었습니다.");


                            break;
                        }


                    // =================================================
                    // Disable
                    // =================================================

                    case AutoParameterRequest.Disable:
                        {
                            AutoParameterEngine.Disable(
                                doc);


                            Completed?.Invoke(
                                true,
                                "Auto Parameter가 비활성화되었습니다.");


                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                if (Request ==
                    AutoParameterRequest.LoadCatalog)
                {
                    CatalogLoaded?.Invoke(
                        false,
                        ex.Message,
                        null);
                }
                else
                {
                    Completed?.Invoke(
                        false,
                        ex.Message);
                }
            }
            finally
            {
                ResetRequest();
            }
        }


        // =========================================================
        // Name
        // =========================================================

        public string GetName()
        {
            return
                "WonClick Auto Parameter External Event";
        }


        // =========================================================
        // Reset
        // =========================================================

        private void ResetRequest()
        {
            Request =
                AutoParameterRequest.None;
        }
    }
}