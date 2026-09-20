using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace WonClick
{
    internal static class RevitApiTest
    {
        public static string GetTestText(
            Document document)
        {
            return document.Title;
        }
    }
}