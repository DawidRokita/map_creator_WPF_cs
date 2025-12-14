public class ObjectInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Key { get; set; }
    public string Type { get; set; }
    public string Category { get; set; }

    public double OffsetX { get; set; }

    public string Direction { get; set; }
    public int? PatrolDistance { get; set; }
}
