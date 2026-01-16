using System;
using System.Collections.Generic;

namespace map_creator.Data.Models;

public partial class Map
{
    public int Id { get; set; }

    public string NameMap { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public DateTime Date { get; set; }

    public int Plus { get; set; }

    public int Minus { get; set; }

    public string ObjectJson { get; set; } = null!;

    public string MapsJson { get; set; } = null!;

    public string Desc { get; set; } = null!;
}
