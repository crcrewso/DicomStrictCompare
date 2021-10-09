namespace ProfileBatchCompare.Model
{
    public record DataPoint
    {
        public double X { get; init; }
        public double Y {  get; init; }
        public double Z { get; init; }
        public double Dose {  get; init; }
        public double? Error {  get; init; }

        public DataPoint(double x, double y, double z, double dose, double? error)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.Dose = dose;
            this.Error = error;
        }
    }
}