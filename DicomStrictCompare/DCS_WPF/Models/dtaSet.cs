using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCS_WPF.Models
{
    public record DtaSet
    {
        public int Tolerance;
        public int DTA;
        public enum DTAUnit { mm, Voxels}
        public DTAUnit Unit;
        public float Threshhold;
        public int Trim;
        public bool Global;
        public bool Gamma;
        const string percent = " (%)";
        const string space = " ";
        const string tab = "\t";
        public string Name;


        public override string ToString()
        {
            string ret = "";
          
            ret += Tolerance.ToString() + percent + tab;
            ret += DTA.ToString() + space + Unit.ToString() + tab;
            ret += Threshhold.ToString() + percent + tab;
            ret += Trim.ToString() + " voxels" + tab;
            ret += Global ? " Global\t" : " Local\t";
            ret += Gamma ? " Gamma\t" : " Local\t";
            




            return ret;
        }


    }
}
