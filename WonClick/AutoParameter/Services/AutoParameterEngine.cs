using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using WonClick.AutoParameter.Models;
using WonClick.AutoParameter.Updaters;

namespace WonClick.AutoParameter.Services
{
    public static class AutoParameterEngine
    {
        // =========================================================
        // Nullable로 선언
        //
        // Revit OnStartup에서 Initialize()가 호출되기 전까지는
        // Updater가 존재하지 않는 상태가 정상이다.
        // =========================================================

        private static AutoParameterUpdater? _updater;


        // =========================================================
        // Document별 Rule
        // =========================================================

        private static readonly Dictionary<
            Document,
            List<AutoParameterRule>>
            RulesByDocument =
                new Dictionary<
                    Document,
                    List<AutoParameterRule>>();


        // =========================================================
        // 초기화
        //
        // 이 단계에서는 Updater를 등록하지 않는다.
        // 즉 Export / Import에는 아무 영향이 없다.
        // =========================================================

        public static void Initialize(
            AddInId addInId)
        {
            if (addInId == null)
            {
                throw new ArgumentNullException(
                    nameof(addInId));
            }


            // 중복 초기화 방지
            if (_updater != null)
            {
                return;
            }


            _updater =
                new AutoParameterUpdater(
                    addInId);
        }


        // =========================================================
        // Auto Parameter ON
        // =========================================================

        public static void Enable(
            Document doc,
            IEnumerable<AutoParameterRule> rules)
        {
            if (doc == null)
            {
                throw new ArgumentNullException(
                    nameof(doc));
            }


            if (_updater == null)
            {
                throw new InvalidOperationException(
                    "AutoParameterEngine.Initialize()가 "
                    +
                    "먼저 호출되어야 합니다.");
            }


            List<AutoParameterRule> ruleList =
                rules
                    .Where(
                        rule =>
                            rule != null
                            &&
                            rule.Enabled)
                    .ToList();


            if (ruleList.Count == 0)
            {
                Disable(
                    doc);

                return;
            }


            RulesByDocument[doc] =
                ruleList;


            RegisterUpdater(
                doc);
        }


        // =========================================================
        // Auto Parameter OFF
        // =========================================================

        public static void Disable(
            Document doc)
        {
            if (doc == null)
            {
                return;
            }


            RulesByDocument.Remove(
                doc);


            AutoParameterUpdater? updater =
                _updater;


            if (updater == null)
            {
                return;
            }


            UpdaterId updaterId =
                updater.GetUpdaterId();


            try
            {
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
                // 이미 제거되었거나
                // Document 상태가 종료 중이면 무시
            }
        }


        // =========================================================
        // 현재 활성 상태
        // =========================================================

        public static bool IsEnabled(
            Document doc)
        {
            if (doc == null)
            {
                return false;
            }


            return
                RulesByDocument.TryGetValue(
                    doc,
                    out List<AutoParameterRule>? rules)
                &&
                rules != null
                &&
                rules.Count > 0;
        }


        // =========================================================
        // Updater에서 Rule 조회
        // =========================================================

        internal static IReadOnlyList<AutoParameterRule>
            GetRules(
                Document doc)
        {
            if (doc == null)
            {
                return
                    Array.Empty<
                        AutoParameterRule>();
            }


            if (RulesByDocument.TryGetValue(
                    doc,
                    out List<AutoParameterRule>? rules)
                &&
                rules != null)
            {
                return rules;
            }


            return
                Array.Empty<
                    AutoParameterRule>();
        }


        // =========================================================
        // Updater 등록
        // =========================================================

        private static void RegisterUpdater(
            Document doc)
        {
            AutoParameterUpdater? updater =
                _updater;


            if (updater == null)
            {
                return;
            }


            UpdaterId updaterId =
                updater.GetUpdaterId();


            // =====================================================
            // 이미 현재 Document에 등록되어 있으면
            // 중복 등록하지 않는다.
            // =====================================================

            if (UpdaterRegistry.IsUpdaterRegistered(
                    updaterId,
                    doc))
            {
                return;
            }


            // =====================================================
            // 현재 Document 전용 Updater 등록
            // =====================================================

            UpdaterRegistry.RegisterUpdater(
                updater,
                doc,
                true);


            // =====================================================
            // Model Category 수집
            // =====================================================

            List<ElementId> categoryIds =
                new List<ElementId>();


            foreach (Category category
                     in doc.Settings.Categories)
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


                try
                {
                    // Filter에 사용할 수 없는 Category가
                    // 존재할 수 있으므로 미리 검사한다.
                    ElementCategoryFilter testFilter =
                        new ElementCategoryFilter(
                            category.Id);


                    categoryIds.Add(
                        category.Id);
                }
                catch
                {
                    // 필터 사용 불가 Category 제외
                }
            }


            if (categoryIds.Count == 0)
            {
                return;
            }


            // =====================================================
            // 신규 Model Element 생성 감지
            // =====================================================

            ElementMulticategoryFilter filter =
                new ElementMulticategoryFilter(
                    categoryIds);


            UpdaterRegistry.AddTrigger(
                updaterId,
                doc,
                filter,
                Element.GetChangeTypeElementAddition());
        }
    }
}