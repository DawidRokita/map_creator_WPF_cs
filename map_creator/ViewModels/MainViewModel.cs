using map_creator.Models;
using map_creator.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace map_creator.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        // ===== STAŁE =====
        public const int CellSize = 32;

        private readonly int[] EMPTY_TILE_IDS =
        {
            4,6,9,12,15,17,21,23,26,29,32,38,40,41,42,43,44,45,46,47,
            48,49,50,51,52,53,54,55,56,57,60,63,66,72,74,77,80,83
        };

        // ===== MAPA (KAFELKI) =====
        private int[,] _grid;
        private int _rows = 21;
        private int _cols = 34;

        public int Rows
        {
            get => _rows;
            set { _rows = value; OnPropertyChanged(); }
        }

        public int Columns
        {
            get => _cols;
            set { _cols = value; OnPropertyChanged(); }
        }

        // ===== NAZWA / OPIS =====
        private string _mapName = "mapa";
        public string MapName
        {
            get => _mapName;
            set { _mapName = value; OnPropertyChanged(); }
        }

        private string _mapDescription = "";
        public string MapDescription
        {
            get => _mapDescription;
            set { _mapDescription = value; OnPropertyChanged(); }
        }

        // ===== ZOOM =====
        private double _zoom = 1.0;
        public double Zoom
        {
            get => _zoom;
            set
            {
                _zoom = Math.Round(value, 2);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ZoomPercent));
            }
        }
        public string ZoomPercent => $"{(int)(Zoom * 100)}%";

        // ===== TILESET =====
        public ObservableCollection<List<Tile>> TileGroups { get; } = new();
        public List<Tile> AllTiles { get; private set; } = new();

        private Tile _selectedTile;
        public Tile SelectedTile
        {
            get => _selectedTile;
            set
            {
                _selectedTile = value;
                OnPropertyChanged();
            }
        }



        // ===== OBIEKTY =====
        private ObjectInstance[,] _objectsGrid;
        public ObjectInstance[,] GetObjectsGrid() => _objectsGrid;

        public ObservableCollection<ObjectSection> ObjectSections { get; } = new();

        private ObjectDef _selectedObject;
        public ObjectDef SelectedObject
        {
            get => _selectedObject;
            set
            {
                _selectedObject = value;

                OnPropertyChanged(); // SelectedObject
                OnPropertyChanged(nameof(SelectedObjectKey));

                // 🔥 TEGO BRAKOWAŁO
                OnPropertyChanged(nameof(IsSharkmanSelected));
                OnPropertyChanged(nameof(IsCannonSelected));

                if (_selectedObject != null)
                    SelectedTile = null;
            }
        }


        // ✅ XAML trigger porównuje Key z SelectedObjectKey
        public string SelectedObjectKey => SelectedObject?.Key;

        private int _defaultSharkmanPatrolDistance = 100;
        public int DefaultSharkmanPatrolDistance
        {
            get => _defaultSharkmanPatrolDistance;
            set { _defaultSharkmanPatrolDistance = value; OnPropertyChanged(); }
        }

        private string _cannonDirection = "LEFT";
        public string CannonDirection
        {
            get => _cannonDirection;
            set { _cannonDirection = value; OnPropertyChanged(); }
        }

        public bool IsSharkmanSelected =>
            SelectedObject?.Key == "sharkman";

        public bool IsCannonSelected =>
            SelectedObject?.Key == "cannon";


        // meta obrazków obiektów: key -> (src,w,h)
        private readonly Dictionary<string, (string src, int w, int h)> _objectMeta = new();
        public (string src, int w, int h)? GetObjectMeta(string key)
        {
            if (key == null) return null;
            return _objectMeta.TryGetValue(key, out var m) ? m : null;
        }

        // ===== ZAKŁADKI (Kafelki/Obiekty) =====
        private bool _isObjectsMode;
        public bool IsObjectsMode
        {
            get => _isObjectsMode;
            set
            {
                if (_isObjectsMode == value) return;
                _isObjectsMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsTilesMode));
            }
        }
        public bool IsTilesMode => !IsObjectsMode;

        // ===== UNDO / REDO (dla kafelków + obiektów) =====
        private class Snapshot
        {
            public int[,] Tiles;
            public ObjectInstance[,] Objects;
            public int Rows;
            public int Cols;
        }

        private readonly Stack<Snapshot> _undoStack = new();
        private readonly Stack<Snapshot> _redoStack = new();

        // ===== EVENT DO RENDERU =====
        public event Action RequestRender;

        // ===== KOMENDY =====
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand ApplySizeCommand { get; }
        public ICommand SelectTileCommand { get; }

        public ICommand SelectObjectCommand { get; }   // ✅ POTRZEBNE W XAML
        public ICommand ShowTilesCommand { get; }
        public ICommand ShowObjectsCommand { get; }

        // ===== KONSTRUKTOR =====
        public MainViewModel()
        {
            _grid = new int[_rows, _cols];
            _objectsGrid = new ObjectInstance[_rows, _cols];

            LoadTiles();
            LoadObjectDefsAndMeta();

            ZoomInCommand = new RelayCommand(_ => Zoom = Math.Min(Zoom + 0.05, 3));
            ZoomOutCommand = new RelayCommand(_ => Zoom = Math.Max(Zoom - 0.05, 0.3));

            UndoCommand = new RelayCommand(_ => Undo(), _ => _undoStack.Count > 0);
            RedoCommand = new RelayCommand(_ => Redo(), _ => _redoStack.Count > 0);

            SaveCommand = new RelayCommand(_ => SaveAll());   // dwa pliki
            ClearCommand = new RelayCommand(_ => ClearMap());
            ApplySizeCommand = new RelayCommand(_ => ResizeMap());

            SelectTileCommand = new RelayCommand(t =>
            {
                SelectedTile = (Tile)t;
                SelectedObject = null;
            });

            // ✅ wybór obiektu z menu
            SelectObjectCommand = new RelayCommand(o =>
            {
                SelectedObject = (ObjectDef)o;
            });

            ShowTilesCommand = new RelayCommand(_ => IsObjectsMode = false);
            ShowObjectsCommand = new RelayCommand(_ => IsObjectsMode = true);
        }

        // ===== LOGIKA MAPY =====
        public int[,] GetGrid() => _grid;

        public void StartPainting()
        {
            _undoStack.Push(new Snapshot
            {
                Tiles = (int[,])_grid.Clone(),
                Objects = CloneObjects(_objectsGrid),
                Rows = Rows,
                Cols = Columns
            });
            _redoStack.Clear();
        }

        public void PaintTile(int r, int c, bool erase)
        {
            if (r < 0 || r >= Rows || c < 0 || c >= Columns) return;
            if (!erase && SelectedTile == null) return;

            int val = erase ? 0 : SelectedTile.Id;
            if (_grid[r, c] != val)
            {
                _grid[r, c] = val;
                RequestRender?.Invoke();
            }
        }

        // ===== OBIEKTY =====
        private const double SNAP = CellSize / 2.0;

        private (int c, double offsetX) WorldToTileAndOffsetX(double worldX)
        {
            double minX = SNAP;
            double maxX = Columns * CellSize - SNAP;

            double snappedX = Math.Round(worldX / SNAP) * SNAP;
            if (snappedX < minX) snappedX = minX;
            if (snappedX > maxX) snappedX = maxX;

            int c = (int)Math.Floor(snappedX / CellSize);
            c = Math.Max(0, Math.Min(Columns - 1, c));

            double tileCenterX = c * CellSize + CellSize / 2.0;
            double offsetX = snappedX - tileCenterX;

            return (c, offsetX);
        }



        public void PlaceObjectAt(double worldX, double worldY)
        {
            if (SelectedObject == null) return;

            int r = (int)Math.Floor(worldY / CellSize);
            if (r < 0 || r >= Rows) return;

            var (c, offsetX) = WorldToTileAndOffsetX(worldX);

            // single (player/finish)
            if (SelectedObject.Single)
            {
                for (int rr = 0; rr < Rows; rr++)
                    for (int cc = 0; cc < Columns; cc++)
                        if (_objectsGrid[rr, cc]?.Key == SelectedObject.Key)
                            _objectsGrid[rr, cc] = null;
            }

            var inst = new ObjectInstance
            {
                Key = SelectedObject.Key,
                Type = SelectedObject.Type,
                Category = SelectedObject.Category,
                OffsetX = offsetX,
                Direction = SelectedObject.HasDirection ? CannonDirection : null,
                PatrolDistance = SelectedObject.HasPatrolDistance ? DefaultSharkmanPatrolDistance : null
            };

            _objectsGrid[r, c] = inst;
            RequestRender?.Invoke();
        }

        public void EraseObjectAt(double worldX, double worldY)
        {
            var hit = FindObjectAtPosition(worldX, worldY);
            if (hit == null) return;

            _objectsGrid[hit.Value.r, hit.Value.c] = null;
            RequestRender?.Invoke();
        }

        public (ObjectInstance obj, int r, int c)? FindObjectAtPosition(double x, double y)
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    var obj = _objectsGrid[r, c];
                    if (obj == null) continue;

                    var meta = GetObjectMeta(obj.Key);
                    int w = meta?.w ?? CellSize;
                    int h = meta?.h ?? CellSize;

                    double tileX = c * CellSize;
                    double tileY = r * CellSize;

                    double anchorX = tileX + CellSize / 2.0 + obj.OffsetX;
                    double anchorY = tileY + CellSize;

                    double left = anchorX - w / 2.0;
                    double right = anchorX + w / 2.0;
                    double top = anchorY - h;
                    double bottom = anchorY;

                    if (x >= left && x <= right && y >= top && y <= bottom)
                        return (obj, r, c);
                }
            }
            return null;
        }

        // ===== TILESET =====
        private void LoadTiles()
        {
            int totalCols = 17;
            int totalRows = 5;
            int tileCount = 85;
            var skipped = new HashSet<int> { 5, 11 };
            var groups = new[] { (0, 4), (6, 10), (12, 16) };

            var list = new List<Tile>();
            for (int i = 1; i <= tileCount; i++)
            {
                list.Add(new Tile
                {
                    VisualId = i,
                    Id = EMPTY_TILE_IDS.Contains(i) ? 0 : i,
                    Src = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tiles", $"tile_{i}.png")
                });
            }

            AllTiles = list;
            TileGroups.Clear();

            foreach (var g in groups)
            {
                var grp = new List<Tile>();
                for (int r = 0; r < totalRows; r++)
                    for (int c = g.Item1; c <= g.Item2; c++)
                    {
                        if (skipped.Contains(c)) continue;
                        int id = r * totalCols + c + 1;
                        if (id <= tileCount)
                            grp.Add(list.First(x => x.VisualId == id));
                    }
                TileGroups.Add(grp);
            }
        }

        private void LoadObjectDefsAndMeta()
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tiles", "icons");

            ObjectSections.Clear();

            ObjectSections.Add(new ObjectSection
            {
                Id = "special",
                Label = "Player & Finish",
                Items = new ObservableCollection<ObjectDef>
                {
                    new ObjectDef{ Key="player", Type="Player", Category="special", Single=true, IconSrc=Path.Combine(baseDir,"special","Player.png") },
                    new ObjectDef{ Key="finish", Type="Finish", Category="special", Single=true, IconSrc=Path.Combine(baseDir,"special","Finish.png") },
                }
            });

            ObjectSections.Add(new ObjectSection
            {
                Id = "enemies",
                Label = "Enemies",
                Items = new ObservableCollection<ObjectDef>
                {
                    new ObjectDef{ Key="sharkman", Type="Sharkman", Category="enemies", HasPatrolDistance=true, IconSrc=Path.Combine(baseDir,"enemies","Sharkman.png") },
                    new ObjectDef{ Key="crabby", Type="Crabby", Category="enemies", IconSrc=Path.Combine(baseDir,"enemies","Crabby.png") },
                }
            });

            ObjectSections.Add(new ObjectSection
            {
                Id = "pickables",
                Label = "Pickables",
                Items = new ObservableCollection<ObjectDef>
                {
                    new ObjectDef{ Key="goldenSkull", Type="GoldenSkull", Category="pickables", IconSrc=Path.Combine(baseDir,"pickables","GoldenSkull.png") },
                    new ObjectDef{ Key="healthPotion", Type="HealthPotion", Category="pickables", IconSrc=Path.Combine(baseDir,"pickables","HealthPotion.png") },
                    new ObjectDef{ Key="diamond", Type="Diamond", Category="pickables", IconSrc=Path.Combine(baseDir,"pickables","Diamond.png") },
                    new ObjectDef{ Key="goldCoin", Type="GoldCoin", Category="pickables", IconSrc=Path.Combine(baseDir,"pickables","GoldCoin.png") },
                    new ObjectDef{ Key="silverCoin", Type="SilverCoin", Category="pickables", IconSrc=Path.Combine(baseDir,"pickables","SilverCoin.png") },
                }
            });

            ObjectSections.Add(new ObjectSection
            {
                Id = "objects",
                Label = "Objects",
                Items = new ObservableCollection<ObjectDef>
                {
                    new ObjectDef{ Key="cannon", Type="Cannon", Category="objects", HasDirection=true, IconSrc=Path.Combine(baseDir,"objects","Cannon.png") },
                    new ObjectDef{ Key="spikes", Type="Spikes", Category="objects", IconSrc=Path.Combine(baseDir,"objects","Spikes.png") },
                }
            });

            _objectMeta.Clear();
            foreach (var def in ObjectSections.SelectMany(s => s.Items))
            {
                try
                {
                    if (!File.Exists(def.IconSrc)) continue;

                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(def.IconSrc, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();

                    _objectMeta[def.Key] = (def.IconSrc, bmp.PixelWidth, bmp.PixelHeight);
                }
                catch { }
            }
        }

        // ===== UNDO / REDO =====
        private void Undo()
        {
            var cur = new Snapshot
            {
                Tiles = (int[,])_grid.Clone(),
                Objects = CloneObjects(_objectsGrid),
                Rows = Rows,
                Cols = Columns
            };
            _redoStack.Push(cur);

            var snap = _undoStack.Pop();
            _grid = snap.Tiles;
            _objectsGrid = snap.Objects;
            Rows = snap.Rows;
            Columns = snap.Cols;
            RequestRender?.Invoke();
        }

        private void Redo()
        {
            var cur = new Snapshot
            {
                Tiles = (int[,])_grid.Clone(),
                Objects = CloneObjects(_objectsGrid),
                Rows = Rows,
                Cols = Columns
            };
            _undoStack.Push(cur);

            var snap = _redoStack.Pop();
            _grid = snap.Tiles;
            _objectsGrid = snap.Objects;
            Rows = snap.Rows;
            Columns = snap.Cols;
            RequestRender?.Invoke();
        }

        private static ObjectInstance[,] CloneObjects(ObjectInstance[,] src)
        {
            int r = src.GetLength(0);
            int c = src.GetLength(1);
            var copy = new ObjectInstance[r, c];

            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    if (src[i, j] != null)
                    {
                        var o = src[i, j];
                        copy[i, j] = new ObjectInstance
                        {
                            Key = o.Key,
                            Type = o.Type,
                            Category = o.Category,
                            OffsetX = o.OffsetX,
                            Direction = o.Direction,
                            PatrolDistance = o.PatrolDistance
                        };
                    }
            return copy;
        }

        // ===== RESIZE =====
        private void ResizeMap()
        {
            Rows = Math.Max(10, Rows);
            Columns = Math.Max(10, Columns);

            var nTiles = new int[Rows, Columns];
            var nObj = new ObjectInstance[Rows, Columns];

            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                {
                    if (r < _grid.GetLength(0) && c < _grid.GetLength(1))
                        nTiles[r, c] = _grid[r, c];

                    if (r < _objectsGrid.GetLength(0) && c < _objectsGrid.GetLength(1))
                        nObj[r, c] = _objectsGrid[r, c];
                }

            _undoStack.Push(new Snapshot
            {
                Tiles = (int[,])_grid.Clone(),
                Objects = CloneObjects(_objectsGrid),
                Rows = _grid.GetLength(0),
                Cols = _grid.GetLength(1)
            });

            _grid = nTiles;
            _objectsGrid = nObj;

            RequestRender?.Invoke();
        }

        // ===== ZAPIS =====
        private void SaveAll()
        {
            SaveMapJson();
            SaveObjectsJson();
        }

        private void SaveMapJson()
        {
            var sb = new StringBuilder("[\n");
            for (int r = 0; r < Rows; r++)
            {
                sb.Append("  ");
                sb.Append(string.Join(",", Enumerable.Range(0, Columns).Select(c => _grid[r, c])));
                sb.AppendLine(r < Rows - 1 ? "," : "");
            }
            sb.Append("]");

            string json = $@"{{ ""width"":{Columns},""height"":{Rows},""tilewidth"":32,""tileheight"":32,""layers"":[{{""data"":{sb}}}] }}";
            File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"{MapName}.json"), json);
        }



        private void SaveObjectsJson()
        {
            var dto = new ObjectsDto();
            int mapHeight = Rows * CellSize;

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    var obj = _objectsGrid[r, c];
                    if (obj == null) continue;

                    var meta = GetObjectMeta(obj.Key);
                    int h = meta?.h ?? CellSize;

                    double tileX = c * CellSize;
                    double tileY = r * CellSize;

                    double anchorX = tileX + CellSize / 2.0 + obj.OffsetX;
                    double anchorY = tileY + CellSize;

                    double centerX = anchorX;
                    double centerYTop = anchorY - h / 2.0;
                    double centerY = mapHeight - centerYTop;

                    if (obj.Category == "special")
                    {
                        if (obj.Type == "Player")
                            dto.player = new XY { x = centerX, y = centerY };
                        else if (obj.Type == "Finish")
                        {
                            int hh = meta?.h ?? CellSize;
                            double y = centerY;
                            if (hh > CellSize) y -= (hh - CellSize) / 2.0;
                            dto.finish = new XY { x = centerX, y = y };
                        }
                    }
                    else if (obj.Category == "enemies")
                    {
                        var e = new EnemyObj { type = obj.Type, x = centerX, y = centerY };
                        if (obj.PatrolDistance != null) e.patrol_distance = obj.PatrolDistance.Value;
                        dto.enemies.Add(e);
                    }
                    else if (obj.Category == "pickables")
                    {
                        dto.pickables.Add(new SimpleObj { type = obj.Type, x = centerX, y = centerY });
                    }
                    else if (obj.Category == "objects")
                    {
                        var o = new ObjWithDir { type = obj.Type, x = centerX, y = centerY };
                        if (!string.IsNullOrEmpty(obj.Direction)) o.direction = obj.Direction;
                        dto.objects.Add(o);
                    }
                }
            }

            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"{MapName}_objects.json"), json);
        }

        private class ObjectsDto
        {
            public XY player { get; set; } = null;
            public List<EnemyObj> enemies { get; set; } = new();
            public List<SimpleObj> pickables { get; set; } = new();
            public List<ObjWithDir> objects { get; set; } = new();
            public XY finish { get; set; } = null;
        }
        private class XY { public double x { get; set; } public double y { get; set; } }
        private class SimpleObj { public string type { get; set; } public double x { get; set; } public double y { get; set; } }
        private class EnemyObj : SimpleObj { public int patrol_distance { get; set; } = 0; }
        private class ObjWithDir : SimpleObj { public string direction { get; set; } = null; }

        private void ClearMap()
        {
            if (MessageBox.Show("Wyczyścić?", "Potwierdź", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _undoStack.Push(new Snapshot
                {
                    Tiles = (int[,])_grid.Clone(),
                    Objects = CloneObjects(_objectsGrid),
                    Rows = Rows,
                    Cols = Columns
                });

                _grid = new int[Rows, Columns];
                _objectsGrid = new ObjectInstance[Rows, Columns];
                RequestRender?.Invoke();
            }
        }
    }
}
