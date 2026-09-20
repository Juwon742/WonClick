using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using WonClick.AutoParameter.Models;

// WonClick.Parameter 충돌 방지
using RevitParameter = Autodesk.Revit.DB.Parameter;

namespace WonClick.AutoParameter.Services
{
    /// <summary>
    /// Auto Parameter UI에서 사용할
    /// Instance Parameter 목록을 관리한다.
    ///
    /// 핵심:
    /// 1. 동일 Category + Type의 대표 Instance만 검사
    /// 2. Document별 Cache
    /// 3. Family / Type / Parameter 구조 변경 시 Cache 무효화
    /// </summary>
    public static class AutoParameterCatalogService
    {
        // =========================================================
        // Document를 강제로 계속 잡고 있지 않도록
        // ConditionalWeakTable 사용
        // =========================================================

        private static readonly ConditionalWeakTable<
            Document,
            CatalogCacheEntry>
            Cache =
                new ConditionalWeakTable<
                    Document,
                    CatalogCacheEntry>();


        // =========================================================
        // Catalog 가져오기
        // =========================================================

        public static AutoParameterCatalogResult GetCatalog(
            Document doc)
        {
            if (doc == null)
            {
                throw new ArgumentNullException(
                    nameof(doc));
            }


            // =====================================================
            // 기존 Cache 확인
            // =====================================================

            if (Cache.TryGetValue(
                    doc,
                    out CatalogCacheEntry? cachedEntry)
                &&
                cachedEntry != null
                &&
                !cachedEntry.IsDirty)
            {
                return
                    new AutoParameterCatalogResult(
                        new List<InstanceParameterItem>(
                            cachedEntry.Items),
                        fromCache: true,
                        elapsedMilliseconds: 0);
            }


            // =====================================================
            // 새 Catalog 작성
            // =====================================================

            Stopwatch stopwatch =
                Stopwatch.StartNew();


            CatalogCacheEntry newEntry =
                BuildCatalog(
                    doc);


            stopwatch.Stop();


            // =====================================================
            // 기존 Cache 교체
            // =====================================================

            Cache.Remove(
                doc);


            Cache.Add(
                doc,
                newEntry);


            return
                new AutoParameterCatalogResult(
                    new List<InstanceParameterItem>(
                        newEntry.Items),
                    fromCache: false,
                    elapsedMilliseconds:
                        stopwatch.ElapsedMilliseconds);
        }


        // =========================================================
        // Cache 강제 Dirty
        // =========================================================

        public static void MarkDirty(
            Document doc)
        {
            if (doc == null)
            {
                return;
            }


            if (Cache.TryGetValue(
                    doc,
                    out CatalogCacheEntry? entry)
                &&
                entry != null)
            {
                entry.IsDirty =
                    true;
            }
        }


        // =========================================================
        // Revit DocumentChanged 감시
        //
        // 모든 모델링 변경에서 Cache를 버리지 않는다.
        //
        // Parameter 목록 구조에 영향을 줄 가능성이 있는 경우만
        // Dirty 처리한다.
        // =========================================================

        public static void NotifyDocumentChanged(
            DocumentChangedEventArgs e)
        {
            if (e == null)
            {
                return;
            }


            Document doc;


            try
            {
                doc =
                    e.GetDocument();
            }
            catch
            {
                return;
            }


            if (doc == null)
            {
                return;
            }


            // Cache가 아직 없다면
            // DocumentChanged에서 검사할 이유도 없음
            if (!Cache.TryGetValue(
                    doc,
                    out CatalogCacheEntry? entry)
                ||
                entry == null)
            {
                return;
            }


            if (entry.IsDirty)
            {
                return;
            }


            // =====================================================
            // Added
            // =====================================================

            ICollection<ElementId> addedIds;


            try
            {
                addedIds =
                    e.GetAddedElementIds();
            }
            catch
            {
                addedIds =
                    Array.Empty<ElementId>();
            }


            foreach (ElementId id
                     in addedIds)
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


                // =================================================
                // Project / Shared Parameter 구조 변화
                // =================================================

                if (element is ParameterElement)
                {
                    entry.IsDirty =
                        true;

                    return;
                }


                // =================================================
                // FamilySymbol / WallType / DuctType 등
                // 새로운 Type 생성
                // =================================================

                if (element is ElementType)
                {
                    entry.IsDirty =
                        true;

                    return;
                }


                // =================================================
                // 새 Instance인데 기존 Catalog 작성 당시
                // 존재하지 않았던 Category + Type 조합이라면
                // 새 Parameter 구조가 등장했을 가능성이 있으므로
                // Dirty 처리
                //
                // 평범한 동일 Type Instance 추가는 Dirty 안 됨.
                // =================================================

                if (IsModelInstance(
                        element))
                {
                    string signature =
                        BuildElementSignature(
                            element);


                    if (!entry.KnownSignatures.Contains(
                            signature))
                    {
                        entry.IsDirty =
                            true;

                        return;
                    }
                }
            }


            // =====================================================
            // Modified
            // =====================================================

            ICollection<ElementId> modifiedIds;


            try
            {
                modifiedIds =
                    e.GetModifiedElementIds();
            }
            catch
            {
                modifiedIds =
                    Array.Empty<ElementId>();
            }


            foreach (ElementId id
                     in modifiedIds)
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


                // Parameter Binding / Parameter 자체 변경
                if (element is ParameterElement)
                {
                    entry.IsDirty =
                        true;

                    return;
                }


                // Family Reload 등으로 Type 정의 변경
                if (element is ElementType)
                {
                    entry.IsDirty =
                        true;

                    return;
                }
            }


            // =====================================================
            // Deleted
            //
            // 삭제된 Element 자체는 이미 조회할 수 없으므로
            // Catalog 작성 당시 기억해둔 ID와 비교
            // =====================================================

            ICollection<ElementId> deletedIds;


            try
            {
                deletedIds =
                    e.GetDeletedElementIds();
            }
            catch
            {
                deletedIds =
                    Array.Empty<ElementId>();
            }


            foreach (ElementId id
                     in deletedIds)
            {
                long value =
                    id.Value;


                // 대표 Instance 삭제
                if (entry.RepresentativeElementIds.Contains(
                        value))
                {
                    entry.IsDirty =
                        true;

                    return;
                }


                // Type 삭제
                if (entry.KnownTypeIds.Contains(
                        value))
                {
                    entry.IsDirty =
                        true;

                    return;
                }


                // Project / Shared Parameter 삭제 가능성
                if (entry.KnownParameterElementIds.Contains(
                        value))
                {
                    entry.IsDirty =
                        true;

                    return;
                }
            }
        }


        // =========================================================
        // 실제 Catalog 작성
        // =========================================================

        private static CatalogCacheEntry BuildCatalog(
            Document doc)
        {
            CatalogCacheEntry entry =
                new CatalogCacheEntry();


            // =====================================================
            // Parameter 중복 제거
            // =====================================================

            HashSet<string> parameterKeys =
                new HashSet<string>(
                    StringComparer.Ordinal);


            // =====================================================
            // 같은 Category + Type 대표 Instance만 검사
            // =====================================================

            HashSet<string> scannedSignatures =
                new HashSet<string>(
                    StringComparer.Ordinal);


            // =====================================================
            // Parameter Group 판정 Cache
            //
            // 같은 Group마다 LabelUtils를 반복 호출하지 않음
            // =====================================================

            Dictionary<string, bool> groupResultCache =
                new Dictionary<string, bool>(
                    StringComparer.Ordinal);


            // =====================================================
            // ToElements() 사용하지 않고 직접 순회
            //
            // 전체 Element Collection을 메모리에 만들지 않음
            // =====================================================

            FilteredElementCollector collector =
                new FilteredElementCollector(
                    doc)
                    .WhereElementIsNotElementType();


            foreach (Element element
                     in collector)
            {
                if (element == null)
                {
                    continue;
                }


                if (!IsModelInstance(
                        element))
                {
                    continue;
                }


                string signature =
                    BuildElementSignature(
                        element);


                // =================================================
                // 이미 동일 구조의 Instance를 검사했다면 Skip
                // =================================================

                if (!scannedSignatures.Add(
                        signature))
                {
                    continue;
                }


                entry.KnownSignatures.Add(
                    signature);


                entry.RepresentativeElementIds.Add(
                    element.Id.Value);


                ElementId typeId;


                try
                {
                    typeId =
                        element.GetTypeId();
                }
                catch
                {
                    typeId =
                        ElementId.InvalidElementId;
                }


                if (typeId != null &&
                    typeId != ElementId.InvalidElementId)
                {
                    entry.KnownTypeIds.Add(
                        typeId.Value);
                }


                // =================================================
                // 대표 Instance의 Parameter만 검사
                // =================================================

                foreach (RevitParameter parameter
                         in element.Parameters)
                {
                    if (parameter == null)
                    {
                        continue;
                    }


                    // =============================================
                    // ReadOnly 제외
                    // =============================================

                    if (parameter.IsReadOnly)
                    {
                        continue;
                    }


                    // =============================================
                    // Text / Set Group만
                    // =============================================

                    if (!IsTargetParameterGroup(
                            parameter,
                            groupResultCache))
                    {
                        continue;
                    }


                    string parameterName =
                        parameter.Definition?.Name
                        ?? string.Empty;


                    if (string.IsNullOrWhiteSpace(
                            parameterName))
                    {
                        continue;
                    }


                    Guid? sharedGuid =
                        null;


                    if (parameter.IsShared)
                    {
                        try
                        {
                            sharedGuid =
                                parameter.GUID;
                        }
                        catch
                        {
                            sharedGuid =
                                null;
                        }
                    }


                    // =============================================
                    // Parameter 중복 Key
                    // =============================================

                    string parameterKey;


                    if (sharedGuid.HasValue)
                    {
                        parameterKey =
                            "GUID:"
                            +
                            sharedGuid.Value;
                    }
                    else
                    {
                        parameterKey =
                            "ID:"
                            +
                            parameter.Id.Value
                            +
                            "|NAME:"
                            +
                            parameterName;
                    }


                    if (!parameterKeys.Add(
                            parameterKey))
                    {
                        continue;
                    }


                    // =============================================
                    // 삭제 감지를 위한 ParameterElement ID 기억
                    // =============================================

                    if (parameter.Id.Value > 0)
                    {
                        entry.KnownParameterElementIds.Add(
                            parameter.Id.Value);
                    }


                    entry.Items.Add(
                        new InstanceParameterItem
                        {
                            ParameterId =
                                parameter.Id.Value,

                            Name =
                                parameterName,

                            SharedParameterGuid =
                                sharedGuid,

                            StorageTypeName =
                                parameter
                                    .StorageType
                                    .ToString()
                        });
                }
            }


            // =====================================================
            // 이름순 정렬
            // =====================================================

            entry.Items.Sort(
                (a, b) =>
                    StringComparer
                        .CurrentCultureIgnoreCase
                        .Compare(
                            a.Name,
                            b.Name));


            entry.IsDirty =
                false;


            return
                entry;
        }


        // =========================================================
        // Model Instance 확인
        // =========================================================

        private static bool IsModelInstance(
            Element element)
        {
            if (element == null)
            {
                return false;
            }


            if (element is ElementType)
            {
                return false;
            }


            Category? category =
                element.Category;


            if (category == null)
            {
                return false;
            }


            return
                category.CategoryType ==
                CategoryType.Model;
        }


        // =========================================================
        // 대표 Instance Signature
        //
        // Category
        // +
        // TypeId
        // +
        // CLR Type
        //
        // Invalid TypeId 객체끼리도 지나치게 합쳐지지 않도록
        // CLR Type 포함
        // =========================================================

        private static string BuildElementSignature(
            Element element)
        {
            long categoryId =
                element.Category?.Id.Value
                ?? long.MinValue;


            long typeId =
                ElementId.InvalidElementId.Value;


            try
            {
                ElementId currentTypeId =
                    element.GetTypeId();


                if (currentTypeId != null)
                {
                    typeId =
                        currentTypeId.Value;
                }
            }
            catch
            {
            }


            string runtimeType =
                element
                    .GetType()
                    .FullName
                ??
                element
                    .GetType()
                    .Name;


            return
                categoryId
                +
                "|"
                +
                typeId
                +
                "|"
                +
                runtimeType;
        }


        // =========================================================
        // Parameter Group
        //
        // Text
        // Set
        //
        // Group 판정 결과 Cache 적용
        // =========================================================

        private static bool IsTargetParameterGroup(
            RevitParameter parameter,
            Dictionary<string, bool> groupResultCache)
        {
            try
            {
                Definition? definition =
                    parameter.Definition;


                if (definition == null)
                {
                    return false;
                }


                ForgeTypeId groupTypeId =
                    definition.GetGroupTypeId();


                string groupKey =
                    groupTypeId.TypeId
                    ?? string.Empty;


                // =================================================
                // 이전 판정값
                // =================================================

                if (groupResultCache.TryGetValue(
                        groupKey,
                        out bool cachedResult))
                {
                    return
                        cachedResult;
                }


                bool result =
                    false;


                // =================================================
                // Text
                // =================================================

                if (groupTypeId ==
                    GroupTypeId.Text)
                {
                    result =
                        true;
                }
                else
                {
                    // =============================================
                    // Set
                    // =============================================

                    try
                    {
                        string groupName =
                            LabelUtils.GetLabelForGroup(
                                groupTypeId);


                        result =
                            string.Equals(
                                groupName,
                                "Set",
                                StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        result =
                            false;
                    }
                }


                groupResultCache[groupKey] =
                    result;


                return
                    result;
            }
            catch
            {
                return false;
            }
        }


        // =========================================================
        // 내부 Cache
        // =========================================================

        private sealed class CatalogCacheEntry
        {
            public bool IsDirty
            {
                get;
                set;
            }


            public List<InstanceParameterItem> Items
            {
                get;
            } =
                new List<InstanceParameterItem>();


            public HashSet<string> KnownSignatures
            {
                get;
            } =
                new HashSet<string>(
                    StringComparer.Ordinal);


            public HashSet<long> RepresentativeElementIds
            {
                get;
            } =
                new HashSet<long>();


            public HashSet<long> KnownTypeIds
            {
                get;
            } =
                new HashSet<long>();


            public HashSet<long> KnownParameterElementIds
            {
                get;
            } =
                new HashSet<long>();
        }
    }


    // =============================================================
    // UI / ExternalEvent 반환용
    // =============================================================

    public sealed class AutoParameterCatalogResult
    {
        public IReadOnlyList<InstanceParameterItem> Items
        {
            get;
        }


        public bool FromCache
        {
            get;
        }


        public long ElapsedMilliseconds
        {
            get;
        }


        public AutoParameterCatalogResult(
            IReadOnlyList<InstanceParameterItem> items,
            bool fromCache,
            long elapsedMilliseconds)
        {
            Items =
                items
                ?? Array.Empty<InstanceParameterItem>();


            FromCache =
                fromCache;


            ElapsedMilliseconds =
                elapsedMilliseconds;
        }
    }
}