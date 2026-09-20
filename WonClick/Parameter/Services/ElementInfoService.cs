using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace WonClick.Parameter.Services
{
    public static class ElementInfoService
    {
        public static List<Element> GetVisibleModelElements(
            Document document,
            View3D view3D)
        {
            FilteredElementCollector collector =
                new FilteredElementCollector(
                    document,
                    view3D.Id);


            List<Element> elements =
                collector
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .Where(
                        element =>
                            !ElementExclusionPolicy
                                .ShouldExclude(element))
                    .ToList();


            return elements;
        }
    }
}