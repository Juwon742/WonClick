using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Globalization;
using WonClick.AutoParameter.Models;
using WonClick.AutoParameter.Services;

// WonClick.Parameter 네임스페이스 충돌 방지
using RevitParameter = Autodesk.Revit.DB.Parameter;

namespace WonClick.AutoParameter.Updaters
{
    public class AutoParameterUpdater : IUpdater
    {
        // =========================================================
        // WonClick Auto Parameter Updater GUID
        // =========================================================

        private static readonly Guid UpdaterGuid =
            new Guid(
                "A8184323-E81A-4D68-84C6-BDBEC8ED2F42");


        private readonly UpdaterId
            _updaterId;


        // =========================================================
        // Constructor
        // =========================================================

        public AutoParameterUpdater(
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
            return _updaterId;
        }


        public string GetUpdaterName()
        {
            return
                "WonClick Auto Parameter";
        }


        public string GetAdditionalInformation()
        {
            return
                "Automatically fills writable Instance Parameters "
                +
                "in the Text and Set parameter groups.";
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
            Document doc =
                data.GetDocument();


            IReadOnlyList<AutoParameterRule> rules =
                AutoParameterEngine.GetRules(
                    doc);


            if (rules.Count == 0)
            {
                return;
            }


            ICollection<ElementId> addedElementIds =
                data.GetAddedElementIds();


            foreach (ElementId elementId
                     in addedElementIds)
            {
                Element? element;


                try
                {
                    element =
                        doc.GetElement(
                            elementId);
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
                // Type Element 제외
                // =================================================

                if (element is ElementType)
                {
                    continue;
                }


                foreach (AutoParameterRule rule
                         in rules)
                {
                    if (rule == null)
                    {
                        continue;
                    }


                    if (!rule.Enabled)
                    {
                        continue;
                    }


                    try
                    {
                        RevitParameter? parameter =
                            FindInstanceParameter(
                                element,
                                rule);


                        if (parameter == null)
                        {
                            continue;
                        }


                        // =========================================
                        // ReadOnly 제외
                        // =========================================

                        if (parameter.IsReadOnly)
                        {
                            continue;
                        }


                        // =========================================
                        // Text / Set Group만 허용
                        // =========================================

                        if (!IsTargetParameterGroup(
                                parameter))
                        {
                            continue;
                        }


                        SetParameterValue(
                            parameter,
                            rule.Value);
                    }
                    catch
                    {
                        // 개별 Parameter 실패가
                        // Element 생성 전체를 중단시키지 않음
                    }
                }
            }
        }


        // =========================================================
        // Instance Parameter 검색
        // =========================================================

        private static RevitParameter?
            FindInstanceParameter(
                Element element,
                AutoParameterRule rule)
        {
            RevitParameter? idMatchedParameter =
                null;


            RevitParameter? nameMatchedParameter =
                null;


            int nameMatchCount =
                0;


            // =====================================================
            // Instance Element의 Parameter만 조사
            //
            // Type Element 접근 없음
            // =====================================================

            foreach (RevitParameter parameter
                     in element.Parameters)
            {
                if (parameter == null)
                {
                    continue;
                }


                // =================================================
                // 1순위
                // Shared Parameter GUID
                // =================================================

                if (rule.SharedParameterGuid.HasValue &&
                    parameter.IsShared)
                {
                    try
                    {
                        if (parameter.GUID ==
                            rule.SharedParameterGuid.Value)
                        {
                            return parameter;
                        }
                    }
                    catch
                    {
                    }
                }


                // =================================================
                // 2순위
                // Parameter ID
                // =================================================

                try
                {
                    if (parameter.Id.Value ==
                        rule.ParameterId)
                    {
                        idMatchedParameter =
                            parameter;
                    }
                }
                catch
                {
                }


                // =================================================
                // 3순위
                // Parameter Name
                // =================================================

                try
                {
                    string parameterName =
                        parameter.Definition?.Name
                        ?? string.Empty;


                    if (!string.IsNullOrWhiteSpace(
                            parameterName)
                        &&
                        string.Equals(
                            parameterName,
                            rule.ParameterName,
                            StringComparison.Ordinal))
                    {
                        nameMatchedParameter =
                            parameter;


                        nameMatchCount++;
                    }
                }
                catch
                {
                }
            }


            if (idMatchedParameter != null)
            {
                return idMatchedParameter;
            }


            if (nameMatchCount == 1)
            {
                return nameMatchedParameter;
            }


            return null;
        }


        // =========================================================
        // Parameter Group 확인
        //
        // Text / Set
        // =========================================================

        private static bool IsTargetParameterGroup(
            RevitParameter parameter)
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


                // =================================================
                // Text
                // =================================================

                if (groupTypeId ==
                    GroupTypeId.Text)
                {
                    return true;
                }


                // =================================================
                // Set
                // =================================================

                string groupName;


                try
                {
                    groupName =
                        LabelUtils.GetLabelForGroup(
                            groupTypeId);
                }
                catch
                {
                    return false;
                }


                if (string.Equals(
                        groupName,
                        "Set",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }


                return false;
            }
            catch
            {
                return false;
            }
        }


        // =========================================================
        // Parameter Value 입력
        // =========================================================

        private static bool SetParameterValue(
            RevitParameter parameter,
            string value)
        {
            if (parameter.IsReadOnly)
            {
                return false;
            }


            value ??=
                string.Empty;


            try
            {
                switch (parameter.StorageType)
                {
                    // =================================================
                    // String
                    // =================================================

                    case StorageType.String:
                        {
                            string currentValue =
                                parameter.AsString()
                                ?? string.Empty;


                            if (currentValue ==
                                value)
                            {
                                return true;
                            }


                            return
                                parameter.Set(
                                    value);
                        }


                    // =================================================
                    // Integer
                    // =================================================

                    case StorageType.Integer:
                        {
                            if (!int.TryParse(
                                    value,
                                    NumberStyles.Integer,
                                    CultureInfo.InvariantCulture,
                                    out int integerValue))
                            {
                                return false;
                            }


                            if (parameter.AsInteger() ==
                                integerValue)
                            {
                                return true;
                            }


                            return
                                parameter.Set(
                                    integerValue);
                        }


                    // =================================================
                    // Double
                    //
                    // Revit 표시값 기준
                    // =================================================

                    case StorageType.Double:
                        {
                            try
                            {
                                return
                                    parameter.SetValueString(
                                        value);
                            }
                            catch
                            {
                                return false;
                            }
                        }


                    // =================================================
                    // ElementId
                    //
                    // 현재 Auto Parameter에서는 제외
                    // =================================================

                    case StorageType.ElementId:
                        {
                            return false;
                        }


                    default:
                        {
                            return false;
                        }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}