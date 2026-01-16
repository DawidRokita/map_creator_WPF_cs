using System;
using System.Collections.Generic;

namespace map_creator.Data.Models;

public partial class MapVote
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public int MapId { get; set; }

    public int IsLike { get; set; }
}
