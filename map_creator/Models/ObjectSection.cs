using System.Collections.ObjectModel;

namespace map_creator.Models
{
    public class ObjectSection
    {
        public string Id { get; set; }      // np. "enemies"
        public string Label { get; set; }   // np. "Enemies"
        public ObservableCollection<ObjectDef> Items { get; set; } = new();
    }
}
