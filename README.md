# DicomStrictCompare
This C# program will compare folders of Dicom Dose files. This relies on open-source EvilDICOM (http://rexcardan.github.io/Evil-DICOM/) and is distributed under the MIT licence. 

While this was written for a specific use case please don't hesitate to suggest improvements that you would like to see. 

Purpose: To batch the comparisons of Dose files calculated from two sources. A Source/Reference/Known good set, and a Target/Test/Unknown status set. 

Requirements - Folders containing the Dose and Plan files of each data set, Dose files should be exported per field if the intent is to analyze each field independently. 

Dose - The dicom dose file representing the 3d volume of energy deposited within a phantom, structure, or patient of interest. To parse the dose files properly the associated plan from a planning system such as Eclipse or Oncentra is required, again in the associated Dicom file standard. This code assumes the volume is a phantom used in the commissioning of such a planning system but I see no reason based on the assumptions made why this comparison would not also work on patient datasets. (I need to link to the DICOM file standards here)

Comparison - numerical representation of the aggregate difference between two dose files, with 0% representing identical distributions and 100% representing no numerical similarities.

Note: The comparison is using the simplest dose difference and distance to agreement algorithms, not the more advanced gamma metrics which should be an option in future versions (I need the citations here)

Assumptions - Dose files are calculated as Head First Supine, and are calculated as absolute dose. I have not tested using relative dose distributions

