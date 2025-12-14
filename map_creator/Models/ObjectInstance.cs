namespace map_creator.Models
{
    public class ObjectInstance
    {
        public string Key { get; set; }        // np. "sharkman"
        public string Type { get; set; }       // "Sharkman"
        public string Category { get; set; }   // "enemies"
        public double OffsetX { get; set; }    // snap co pół kratki
        public string Direction { get; set; }  // "LEFT"/"RIGHT" (dla cannon)
        public int? PatrolDistance { get; set; } // dla sharkman
    }
}
