using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using WonClick.TypeNameParameter.Services;

namespace WonClick.TypeNameParameter.Updaters
{
    /// <summary>
    /// WonClick SK Auto Sync Updater.
    ///
    /// 일반 Model Element:
    /// -> TypeNameParameterService
    ///
    /// Pipe / Duct Insulation:
    /// -> InsulationParameterService
    ///
    /// Host Pipe / Duct 계열 변경:
    /// -> 연결 Insulation까지 갱신
    /// </summary>
    public sealed class TypeNameParameterUpdater
        : IUpdater
    {
        private static readonly Guid UpdaterGuid =
            new Guid(
                "D6D0A621-1F3D-4C88-9A3D-1A8E5D6B7442");


        private readonly UpdaterId
            _updaterId;


        // =========================================================
        // Constructor
        // =========================================================

        public TypeNameParameterUpdater(
            AddInId addInId)
        {
            if (addInId == null)
            {
                throw new ArgumentNullException(
                    nameof(addInId));
            }


            _updaterId =
                new UpdaterId(
                    addInId,
                    UpdaterGuid);
        }


        // =========================================================
        // IUpdater
        // =========================================================

        public UpdaterId GetUpdaterId()
        {
            return
                _updaterId;
        }


        public string GetUpdaterName()
        {
            return
                "WonClick SK Auto Sync";
        }


        public string GetAdditionalInformation()
        {
            return
                "Automatically synchronizes SK parameters for "
                +
                "model elements and pipe/duct insulation.";
        }


        public ChangePriority GetChangePriority()
        {
            return
                ChangePriority.FreeStandingComponents;
        }


        // =========================================================
        // Execute
        // =========================================================

        public void Execute(
            UpdaterData data)
        {
            // =====================================================
            // SK Auto OFF
            //
            // Trigger 자체는 등록되어 있어도
            // 어떤 Parameter도 변경하지 않는다.
            // =====================================================

            if (!SKAutoSyncSettingsService.IsEnabled)
            {
                return;
            }


            Document doc =
                data.GetDocument();


            if (doc == null)
            {
                return;
            }


            Dictionary<long, ElementId> targetIds =
                new Dictionary<long, ElementId>();


            // =====================================================
            // Added
            // =====================================================

            ICollection<ElementId> addedIds;


            try
            {
                addedIds =
                    data.GetAddedElementIds();
            }
            catch
            {
                addedIds =
                    Array.Empty<ElementId>();
            }


            AddIds(
                targetIds,
                addedIds);


            // =====================================================
            // Modified
            // =====================================================

            ICollection<ElementId> modifiedIds;


            try
            {
                modifiedIds =
                    data.GetModifiedElementIds();
            }
            catch
            {
                modifiedIds =
                    Array.Empty<ElementId>();
            }


            AddIds(
                targetIds,
                modifiedIds);


            if (targetIds.Count == 0)
            {
                return;
            }


            // =====================================================
            // 일반 Element / Insulation 분리
            // =====================================================

            List<ElementId> normalElementIds =
                new List<ElementId>();


            List<ElementId> insulationElementIds =
                new List<ElementId>();


            foreach (ElementId id
                     in targetIds.Values)
            {
                Element? element;


                try
                {
                    element =
                        doc.GetElement(
                            id);
                }
                catch
                {
                    continue;
                }


                if (element == null)
                {
                    continue;
                }


                if (InsulationParameterService
                    .IsSupportedInsulation(
                        element))
                {
                    insulationElementIds.Add(
                        id);
                }
                else
                {
                    normalElementIds.Add(
                        id);
                }
            }


            // =====================================================
            // 1. 일반 Model Element Sync
            // =====================================================

            if (normalElementIds.Count > 0)
            {
                try
                {
                    TypeNameParameterService
                        .SyncElements(
                            doc,
                            normalElementIds);
                }
                catch
                {
                    // 사용자의 Revit 작업을 중단시키지 않음
                }
            }


            // =====================================================
            // 2. 변경된 Host에 연결된 Insulation 갱신
            //
            // 일반 Sync가 먼저 실행되므로
            // Host의 최신 SK_*를 기준으로 상속한다.
            // =====================================================

            foreach (ElementId id
                     in normalElementIds)
            {
                Element? element =
                    doc.GetElement(
                        id);


                if (element == null)
                {
                    continue;
                }


                try
                {
                    InsulationParameterService
                        .SyncHostInsulations(
                            doc,
                            element);
                }
                catch
                {
                }
            }


            // =====================================================
            // 3. Insulation 자신의 값 갱신
            //
            // ITEM / MATE = 자기 Type Name
            // SIZE = 자기 Thickness
            // 기타 SK_* = Host
            // =====================================================

            foreach (ElementId id
                     in insulationElementIds)
            {
                Element? insulation =
                    doc.GetElement(
                        id);


                if (insulation == null)
                {
                    continue;
                }


                try
                {
                    InsulationParameterService
                        .SyncInsulation(
                            doc,
                            insulation);
                }
                catch
                {
                }
            }
        }


        // =========================================================
        // Id Merge
        // =========================================================

        private static void AddIds(
            Dictionary<long, ElementId> targetIds,
            ICollection<ElementId> sourceIds)
        {
            foreach (ElementId id
                     in sourceIds)
            {
                if (id == null
                    ||
                    id ==
                    ElementId.InvalidElementId)
                {
                    continue;
                }


                targetIds[id.Value] =
                    id;
            }
        }
    }
}
