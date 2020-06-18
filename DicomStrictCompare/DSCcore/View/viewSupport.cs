using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace DSCcore.View
{
    internal static class viewSupport
    {
        internal static DicomStrictCompare.Model.Dta listViewToDta(ListViewItem listViewItem)
        {
            bool UseMM;
            bool Relative;
            bool Gamma;
            double Threshhold;
            double Tolerance;
            double Distance;
            int trim;

            UseMM = listViewItem.SubItems[1].Text.Contains("mm");
            Relative = listViewItem.SubItems[4].Text.Contains('y');
            Gamma = listViewItem.SubItems[5].Text.Contains('y');
            var distanceText = listViewItem.SubItems[1].Text;
            Threshhold = double.Parse(listViewItem.SubItems[2].Text);
            Tolerance = double.Parse(listViewItem.SubItems[0].Text);
            Distance = double.Parse(distanceText.Substring(0, distanceText.IndexOf(' ')));
            trim = int.Parse(listViewItem.SubItems[3].Text);



            var temp = new DicomStrictCompare.Model.Dta(UseMM, Threshhold, Tolerance, Distance, Relative, Gamma, trim);
            return temp;
        }
    }
}
