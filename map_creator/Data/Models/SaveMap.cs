using System;
using System.Collections.Generic;

namespace map_creator.Data.Models;

public partial class SaveMap
{
    public int Id { get; set; }

    public string UserID { get; set; } = null!;

    public int MapID { get; set; }
}
