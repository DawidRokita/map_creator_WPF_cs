using map_creator.Services;
using map_creator.Utilities;
using static map_creator.Services.MapService;

namespace map_creator.ViewModels
{
    public class MapCardVM : ViewModelBase
    {
        public int Id { get; }
        public string NameMap { get; }
        public string Desc { get; }
        public string UserId { get; }
        public string Date { get; }

        public string MapsJson { get; }
        public string ObjectJson { get; }

        private bool _isSaved;
        public bool IsSaved
        {
            get => _isSaved;
            set { _isSaved = value; OnPropertyChanged(); OnPropertyChanged(nameof(SaveButtonText)); }
        }

        public string SaveButtonText => IsSaved ? "Usuń zapis" : "Zapisz";

        public MapCardVM(MapRow row)
        {
            Id = row.Id;
            NameMap = row.NameMap;
            Desc = row.Desc;
            UserId = row.UserId;
            Date = row.Date;

            MapsJson = row.MapsJson;
            ObjectJson = row.ObjectJson;
        }


    }
}
