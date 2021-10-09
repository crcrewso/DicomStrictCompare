using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfileBatchCompare.Model
{
    internal record Profile
    {
        public Dictionaries.direction direction { get; init; }
        public Dictionaries.doseType doseType {  get; init; }
        public List<DataPoint> dataPoints {  get; init; }


    }
}
