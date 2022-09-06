using DCS_WPF.Models;
using DCS_WPF.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCS_WPF.ModelViews
{
    public class DTAsettings
    {
        Dictionary<string, DtaSet> _dtaSets;
        public Dictionary<string, DtaSet> DtaSets
        {
            get { return _dtaSets; }
            private set { _dtaSets = value; } 
        }

        public DTAsettings()
        {
            _dtaSets = new Dictionary<string, DtaSet>();
        }

        public void AddDta(DtaSet dtaSet)
        {
            if (!_dtaSets.ContainsKey(dtaSet.Name))
                _dtaSets.Add(dtaSet.Name, dtaSet);
            else
                throw new ArgumentException(nameof(dtaSet) + " This dataset already contains a dtaSet of that name");
        }
    
    }


}
