namespace map_creator.Models
{
    public class MapRecord
    {
        public int Id { get; set; }                 // 0 => INSERT, >0 => UPDATE
        public string NameMap { get; set; }         // TEXT NOT NULL
        public string UserId { get; set; }          // TEXT NOT NULL
        public string Date { get; set; }            // TEXT NOT NULL (ISO)
        public int Plus { get; set; }               // INTEGER NOT NULL
        public int Minus { get; set; }              // INTEGER NOT NULL
        public string ObjectJson { get; set; }      // TEXT NOT NULL
        public string MapsJson { get; set; }        // TEXT NOT NULL
        public string Desc { get; set; }            // TEXT NOT NULL
    }
}
