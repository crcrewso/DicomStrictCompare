namespace DSClibrary
{
    /// <summary>A position on a lattice: voxel or sample indices, or a size in voxels. X fastest, as in RT Dose.</summary>
    public readonly record struct Index3(int X, int Y, int Z)
    {
        public long Count => (long)X * Y * Z;
    }

    /// <summary>
    /// A position or step in millimetres, DICOM patient coordinates. A value type, unlike EvilDICOM's
    /// <c>Vector3</c>, so the comparers can pass it per point without allocating.
    /// </summary>
    public readonly record struct Position3(double X, double Y, double Z);
}
