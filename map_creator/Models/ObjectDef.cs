namespace map_creator.Models
{
    public class ObjectDef
    {
        public string Key { get; set; }          // np. "player"
        public string Type { get; set; }         // np. "Player"
        public string Category { get; set; }     // "special/enemies/pickables/objects"
        public string IconSrc { get; set; }      // ścieżka do png
        public bool HasDirection { get; set; }   // cannon
        public bool HasPatrolDistance { get; set; } // sharkman
        public bool Single { get; set; }         // player/finish (tylko jeden na mapie)
        public string Icon => IconSrc;

    }
}
