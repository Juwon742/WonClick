using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using WonClick.TypeNameParameter.Updaters;

namespace WonClick.TypeNameParameter.Services
{
    /// <summary>
    /// SK 자동 동기화 Updater의 등록 / Trigger를 관리한다.
    ///
    /// 일반 Model Element:
    /// - 신규 생성
    /// - Geometry 변경
    /// - Type 변경
    ///
    /// Pipe / Duct Insulation:
    /// - 신규 생성
    /// - Type 변경
    /// - Thickness 변경
    ///
    /// Host Pipe / Duct 계열:
    /// - SK_ITEM / SK_MATE / SK_SIZE 외의
    ///   SK_* Parameter 변경
    ///   -> 연결 Insulation에 자동 상속
    ///
    /// ChangeTypeAny는 사용하지 않는다.
    /// </summary>
    public static class TypeNameParameterAutoSyncEngine
    {
        private const string ItemParameterName =
            "SK_ITEM";


        private const string MateParameterName =
            "SK_MATE";


        private const string SizeParameterName =
            "SK_SIZE";


        private static TypeNameParameterUpdater?
            _updater;


        // =========================================================
        // Initialize
        // =========================================================

        public static void Initialize(
            AddInId addInId)
        {
            if (addInId == null)
            {
                throw new ArgumentNullException(
                    nameof(addInId));
            }


            if (_updater != null)
            {
                return;
            }


            _updater =
                new TypeNameParameterUpdater(
                    addInId);
        }


        // =========================================================
        // Document 등록
        // =========================================================

        public static void RegisterDocument(
            Document doc)
        {
            if (doc == null
                ||
                _updater == null)
            {
                return;
            }


            if (doc.IsFamilyDocument
                ||
                doc.IsReadOnly)
            {
                return;
            }


            UpdaterId updaterId =
                _updater.GetUpdaterId();


            // =====================================================
            // Updater 등록 / Trigger 초기화
            // =====================================================

            try
            {
                if (UpdaterRegistry.IsUpdaterRegistered(
                        updaterId,
                        doc))
                {
                    UpdaterRegistry.RemoveDocumentTriggers(
                        updaterId,
                        doc);
                }
                else
                {
                    UpdaterRegistry.RegisterUpdater(
                        _updater,
                        doc,
                        true);
                }
            }
            catch
            {
                return;
            }


            // =====================================================
            // 일반 SK 대상 Category
            // =====================================================

            List<ElementId> targetCategoryIds =
                GetTargetCategoryIds(
                    doc);


            if (targetCategoryIds.Count > 0)
            {
                ElementMulticategoryFilter targetCategoryFilter =
                    new ElementMulticategoryFilter(
                        targetCategoryIds);


                // 신규
                AddTriggerSafe(
                    updaterId,
                    doc,
                    targetCategoryFilter,
                    Element.GetChangeTypeElementAddition());


                // Size / Angle 등 Geometry 변경
                AddTriggerSafe(
                    updaterId,
                    doc,
                    targetCategoryFilter,
                    Element.GetChangeTypeGeometry());


                // Type 변경
                AddTriggerSafe(
                    updaterId,
                    doc,
                    targetCategoryFilter,
                    Element.GetChangeTypeParameter(
                        new ElementId(
                            BuiltInParameter.ELEM_TYPE_PARAM)));
            }


            // =====================================================
            // Pipe / Duct Insulation 전용 Trigger
            // =====================================================

            ElementMulticategoryFilter insulationFilter =
                CreateInsulationCategoryFilter();


            // 신규 Insulation
            AddTriggerSafe(
                updaterId,
                doc,
                insulationFilter,
                Element.GetChangeTypeElementAddition());


            // Insulation Type 변경
            AddTriggerSafe(
                updaterId,
                doc,
                insulationFilter,
                Element.GetChangeTypeParameter(
                    new ElementId(
                        BuiltInParameter.ELEM_TYPE_PARAM)));


            // Pipe Insulation Thickness 변경
            AddTriggerSafe(
                updaterId,
                doc,
                insulationFilter,
                Element.GetChangeTypeParameter(
                    new ElementId(
                        BuiltInParameter
                            .PIPE_INSULATION_THICKNESS)));


            // Duct Insulation Thickness 변경
            AddTriggerSafe(
                updaterId,
                doc,
                insulationFilter,
                Element.GetChangeTypeParameter(
                    new ElementId(
                        BuiltInParameter
                            .DUCT_INSULATION_THICKNESS)));


            // =====================================================
            // Host의 추가 SK_* Parameter 변경 Trigger
            //
            // 예:
            // SK_UTIL
            // SK_FL
            // SK_ZONE
            //
            // SK_ITEM / SK_MATE / SK_SIZE는
            // Updater 자체가 변경하는 값이므로 제외한다.
            // =====================================================

            ElementMulticategoryFilter hostFilter =
                CreateHostCategoryFilter();


            foreach (ElementId parameterId
                     in GetAuxiliarySKParameterIds(
                         doc))
            {
                AddTriggerSafe(
                    updaterId,
                    doc,
                    hostFilter,
                    Element.GetChangeTypeParameter(
                        parameterId));
            }
        }


        // =========================================================
        // Refresh
        // =========================================================

        public static void RefreshDocument(
            Document doc)
        {
            RegisterDocument(
                doc);
        }


        // =========================================================
        // Unregister
        // =========================================================

        public static void UnregisterDocument(
            Document doc)
        {
            if (doc == null
                ||
                _updater == null)
            {
                return;
            }


            try
            {
                UpdaterId updaterId =
                    _updater.GetUpdaterId();


                if (UpdaterRegistry.IsUpdaterRegistered(
                        updaterId,
                        doc))
                {
                    UpdaterRegistry.UnregisterUpdater(
                        updaterId,
                        doc);
                }
            }
            catch
            {
            }
        }


        // =========================================================
        // SK_* Binding Category
        // =========================================================

        private static List<ElementId>
            GetTargetCategoryIds(
                Document doc)
        {
            List<ElementId> result =
                new List<ElementId>();


            HashSet<long> addedCategoryIds =
                new HashSet<long>();


            BindingMap bindingMap =
                doc.ParameterBindings;


            DefinitionBindingMapIterator iterator =
                bindingMap.ForwardIterator();


            iterator.Reset();


            while (iterator.MoveNext())
            {
                Definition? definition =
                    iterator.Key;


                if (definition == null)
                {
                    continue;
                }


                if (!IsCoreSKParameterName(
                        definition.Name))
                {
                    continue;
                }


                if (iterator.Current
                    is not InstanceBinding instanceBinding)
                {
                    continue;
                }


                foreach (Category category
                         in instanceBinding.Categories)
                {
                    if (category == null)
                    {
                        continue;
                    }


                    if (category.CategoryType !=
                        CategoryType.Model)
                    {
                        continue;
                    }


                    if (IsExcludedSystemCategory(
                            category.Id))
                    {
                        continue;
                    }


                    if (!addedCategoryIds.Add(
                            category.Id.Value))
                    {
                        continue;
                    }


                    result.Add(
                        category.Id);
                }
            }


            return result;
        }


        // =========================================================
        // 추가 SK_* Parameter의 ParameterElement Id
        //
        // SK_ITEM / SK_MATE / SK_SIZE 제외
        // =========================================================

        private static List<ElementId>
            GetAuxiliarySKParameterIds(
                Document doc)
        {
            List<ElementId> result =
                new List<ElementId>();


            FilteredElementCollector collector =
                new FilteredElementCollector(
                    doc)
                    .OfClass(
                        typeof(ParameterElement));


            foreach (ParameterElement parameterElement
                     in collector)
            {
                if (parameterElement == null)
                {
                    continue;
                }


                string name =
                    parameterElement.Name
                    ??
                    string.Empty;


                if (!name.StartsWith(
                        "SK_",
                        StringComparison.Ordinal))
                {
                    continue;
                }


                if (IsCoreSKParameterName(
                        name))
                {
                    continue;
                }


                result.Add(
                    parameterElement.Id);
            }


            return result;
        }


        // =========================================================
        // Core SK Parameter
        // =========================================================

        private static bool IsCoreSKParameterName(
            string parameterName)
        {
            return
                string.Equals(
                    parameterName,
                    ItemParameterName,
                    StringComparison.Ordinal)

                ||

                string.Equals(
                    parameterName,
                    MateParameterName,
                    StringComparison.Ordinal)

                ||

                string.Equals(
                    parameterName,
                    SizeParameterName,
                    StringComparison.Ordinal);
        }


        // =========================================================
        // Trigger Safe Add
        // =========================================================

        private static void AddTriggerSafe(
            UpdaterId updaterId,
            Document doc,
            ElementFilter filter,
            ChangeType changeType)
        {
            try
            {
                UpdaterRegistry.AddTrigger(
                    updaterId,
                    doc,
                    filter,
                    changeType);
            }
            catch
            {
                // 특정 Category / Parameter에 Trigger를
                // 등록할 수 없는 환경이어도
                // 나머지 Trigger는 유지한다.
            }
        }


        // =========================================================
        // Host Category Filter
        // =========================================================

        private static ElementMulticategoryFilter
            CreateHostCategoryFilter()
        {
            List<ElementId> categoryIds =
                new List<ElementId>
                {
                    new ElementId(
                        BuiltInCategory.OST_PipeCurves),

                    new ElementId(
                        BuiltInCategory.OST_FlexPipeCurves),

                    new ElementId(
                        BuiltInCategory.OST_PipeFitting),

                    new ElementId(
                        BuiltInCategory.OST_PipeAccessory),

                    new ElementId(
                        BuiltInCategory.OST_DuctCurves),

                    new ElementId(
                        BuiltInCategory.OST_FlexDuctCurves),

                    new ElementId(
                        BuiltInCategory.OST_DuctFitting),

                    new ElementId(
                        BuiltInCategory.OST_DuctAccessory)
                };


            return
                new ElementMulticategoryFilter(
                    categoryIds);
        }


        // =========================================================
        // Insulation Category Filter
        // =========================================================

        private static ElementMulticategoryFilter
            CreateInsulationCategoryFilter()
        {
            List<ElementId> categoryIds =
                new List<ElementId>
                {
                    new ElementId(
                        BuiltInCategory.OST_PipeInsulations),

                    new ElementId(
                        BuiltInCategory.OST_DuctInsulations)
                };


            return
                new ElementMulticategoryFilter(
                    categoryIds);
        }


        // =========================================================
        // System / Helper Category 제외
        // =========================================================

        private static bool IsExcludedSystemCategory(
            ElementId categoryId)
        {
            if (categoryId == null
                ||
                categoryId ==
                ElementId.InvalidElementId)
            {
                return true;
            }


            long id =
                categoryId.Value;


            return
                id ==
                (long)BuiltInCategory.OST_Levels

                ||

                id ==
                (long)BuiltInCategory.OST_Grids

                ||

                id ==
                (long)BuiltInCategory.OST_SectionBox

                ||

                id ==
                (long)BuiltInCategory.OST_VolumeOfInterest

                ||

                id ==
                (long)BuiltInCategory.OST_Views

                ||

                id ==
                (long)BuiltInCategory.OST_Viewers

                ||

                id ==
                (long)BuiltInCategory.OST_Sections

                ||

                id ==
                (long)BuiltInCategory.OST_ReferencePoints

                ||

                id ==
                (long)BuiltInCategory.OST_Lines;
        }
    }
}
