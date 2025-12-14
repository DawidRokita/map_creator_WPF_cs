namespace map_creator.Models
{
    public class ObjectCell
    {
        public string Key { get; set; }          // "sharkman"
        public string Type { get; set; }         // "Sharkman"
        public string Category { get; set; }     // enemies / pickables / objects / special

        public double OffsetX { get; set; }      // jak w JSX
        public int? PatrolDistance { get; set; } // Sharkman
        public string Direction { get; set; }    // Cannon LEFT / RIGHT
    }
}
