using Autodesk.Revit.DB;
using System;
using System.Globalization;

namespace WonClick.Parameter.Services
{
    public static class ParameterValueService
    {
        public static string GetValue(
            Document document,
            Autodesk.Revit.DB.Parameter parameter)
        {
            try
            {
                switch (parameter.StorageType)
                {
                    // =================================================
                    // STRING
                    // =================================================

                    case StorageType.String:
                        {
                            string? value =
                                parameter.AsString();


                            if (!string.IsNullOrEmpty(
                                value))
                            {
                                return value;
                            }


                            string? valueString =
                                parameter.AsValueString();


                            return
                                valueString
                                ?? string.Empty;
                        }


                    // =================================================
                    // INTEGER
                    // =================================================

                    case StorageType.Integer:
                        {
                            string? valueString =
                                parameter.AsValueString();


                            if (!string.IsNullOrEmpty(
                                valueString))
                            {
                                return valueString;
                            }


                            return parameter
                                .AsInteger()
                                .ToString(
                                    CultureInfo.InvariantCulture);
                        }


                    // =================================================
                    // DOUBLE
                    // =================================================

                    case StorageType.Double:
                        {
                            string? valueString =
                                parameter.AsValueString();


                            if (!string.IsNullOrEmpty(
                                valueString))
                            {
                                return valueString;
                            }


                            return parameter
                                .AsDouble()
                                .ToString(
                                    CultureInfo.InvariantCulture);
                        }


                    // =================================================
                    // ELEMENT ID
                    // =================================================

                    case StorageType.ElementId:
                        {
                            string? valueString =
                                parameter.AsValueString();


                            if (!string.IsNullOrEmpty(
                                valueString))
                            {
                                return valueString;
                            }


                            ElementId elementId =
                                parameter.AsElementId();


                            if (elementId
                                == ElementId.InvalidElementId)
                            {
                                return string.Empty;
                            }


                            Element? referencedElement =
                                document.GetElement(
                                    elementId);


                            if (referencedElement != null)
                            {
                                string referencedName =
                                    referencedElement.Name
                                    ?? string.Empty;


                                if (!string.IsNullOrWhiteSpace(
                                    referencedName))
                                {
                                    return referencedName;
                                }
                            }


                            return elementId.Value
                                .ToString(
                                    CultureInfo.InvariantCulture);
                        }


                    // =================================================
                    // NONE
                    // =================================================

                    case StorageType.None:
                    default:
                        {
                            string? valueString =
                                parameter.AsValueString();


                            return
                                valueString
                                ?? string.Empty;
                        }
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}