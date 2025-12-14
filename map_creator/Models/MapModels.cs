namespace map_creator.Models
{
        public class Tile
        {
            public int Id { get; set; }        // ID logiczne (do zapisu)
            public int VisualId { get; set; }  // ID z nazwy pliku (do ładowania)
            public string Src { get; set; }    // Ścieżka do obrazka
        }

    //public class TiledMap
    //{
    //    public double version { get; set; } = 1.1;
    //    public string tiledversion { get; set; } = "1.11.5";
    //    public string orientation { get; set; } = "orthogonal";
    //    public string renderorder { get; set; } = "right-down";
    //    public int width { get; set; }
    //    public int height { get; set; }
    //    public int tilewidth { get; set; }
    //    public int tileheight { get; set; }
    //    public bool infinite { get; set; } = false;
    //    public int nextobjectid { get; set; } = 1;
    //    public List<TiledLayer>? layers { get; set; }
    //    public List<TiledTileset>? tilesets { get; set; }
    //}

    //public class TiledLayer
    //{
    //    public string? name { get; set; }
    //    public string type { get; set; } = "tilelayer";
    //    public bool visible { get; set; } = true;
    //    public double opacity { get; set; } = 1;
    //    public int x { get; set; } = 0;
    //    public int y { get; set; } = 0;
    //    public int width { get; set; }
    //    public int height { get; set; }
    //    public List<int>? data { get; set; }
    //}

    //public class TiledTileset
    //{
    //    public int firstgid { get; set; } = 1;
    //    public string? source { get; set; }
    //}
}
