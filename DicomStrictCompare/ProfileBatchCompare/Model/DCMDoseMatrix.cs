using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DCSCore.Model;
using EvilDICOM.RT;

namespace ProfileBatchCompare.Model
{
    internal class DCMDoseMatrix : DoseMatrixOptimal
    {
        public DCMDoseMatrix(RTDose doseMatrix) : base(doseMatrix)
        {

        }
    }
}
