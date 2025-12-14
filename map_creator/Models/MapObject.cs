namespace map_creator.Models
{
    public class MapObject
    {
        public string Key { get; set; }      // "sharkman"
        public string Type { get; set; }     // "Sharkman"
        public string Category { get; set; } // enemies / objects / pickables
        public int OffsetX { get; set; }
        public int PatrolDistance { get; set; }
        public string Direction { get; set; } // LEFT / RIGHT
    }
}
