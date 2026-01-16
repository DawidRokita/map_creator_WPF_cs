using map_creator.Models;
using map_creator.Services;
using map_creator.Session;
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
        public ICommand OpenMapInEditorCommand { get; }


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

                OnPropertyChanged(nameof(IsSharkmanSelected));
                OnPropertyChanged(nameof(IsCannonSelected));

                if (_selectedObject != null)
                    SelectedTile = null;
            }
        }

        private ObjectInstance _selectedPlacedObject;
        public ObjectInstance SelectedPlacedObject
        {
            get => _selectedPlacedObject;
            set
            {
                _selectedPlacedObject = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSharkmanPlacedSelected));
                OnPropertyChanged(nameof(IsCannonPlacedSelected));
            }
        }

        public bool IsSharkmanPlacedSelected =>
            SelectedPlacedObject?.Key == "sharkman";

        public bool IsCannonPlacedSelected =>
            SelectedPlacedObject?.Key == "cannon";

        public void SelectObjectAt(double x, double y)
        {
            var hit = FindObjectAtPosition(x, y);
            if (hit == null)
            {
                SelectedPlacedObject = null;
                return;
            }

            SelectedPlacedObject = hit.Value.obj;

            // synchronizacja UI
            if (SelectedPlacedObject.PatrolDistance != null)
                DefaultSharkmanPatrolDistance = SelectedPlacedObject.PatrolDistance.Value;

            if (!string.IsNullOrEmpty(SelectedPlacedObject.Direction))
                CannonDirection = SelectedPlacedObject.Direction;
        }


        // XAML trigger porównuje Key z SelectedObjectKey
        public string SelectedObjectKey => SelectedObject?.Key;

        private int _defaultSharkmanPatrolDistance = 100;
        public int DefaultSharkmanPatrolDistance
        {
            get => _defaultSharkmanPatrolDistance;
            set
            {
                _defaultSharkmanPatrolDistance = value;
                OnPropertyChanged();

                if (SelectedPlacedObject?.Key == "sharkman")
                {
                    SelectedPlacedObject.PatrolDistance = value;
                    RequestRender?.Invoke();
                }
            }
        }

        public void InvalidateRender()
        {
            RequestRender?.Invoke();
        }


        private string _cannonDirection = "LEFT";
        public string CannonDirection
        {
            get => _cannonDirection;
            set
            {
                _cannonDirection = value;
                OnPropertyChanged();

                if (SelectedPlacedObject?.Key == "cannon")
                {
                    SelectedPlacedObject.Direction = value;
                    RequestRender?.Invoke();
                }
            }
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

            // wybór obiektu z menu
            SelectObjectCommand = new RelayCommand(o =>
            {
                SelectedObject = (ObjectDef)o;
            });

            ShowTilesCommand = new RelayCommand(_ => IsObjectsMode = false);
            ShowObjectsCommand = new RelayCommand(_ => IsObjectsMode = true);

            ShowEditorTabCommand = new RelayCommand(_ => TopTab = "editor");
            ShowBrowseTabCommand = new RelayCommand(_ =>
            {
                TopTab = "browse";
                RefreshBrowseMaps();
            });

            RefreshBrowseMapsCommand = new RelayCommand(_ => RefreshBrowseMaps());
            DownloadMapCommand = new RelayCommand(m => DownloadMap((MapCardVM)m));
            ToggleSaveMapCommand = new RelayCommand(m => ToggleSaveMap((MapCardVM)m));

            OpenMapInEditorCommand = new RelayCommand(m => OpenMapInEditor((MapCardVM)m));


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




    private void SaveToDatabase(string mapsJson, string objectsJson)
    {
        if (!UserSession.IsLoggedIn)
        {
            MessageBox.Show("Musisz być zalogowany, żeby zapisać mapę do bazy.");
            return;
        }

        var dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BazaDanych.db");
        var mapService = new MapService(dbPath);

        var record = new MapRecord
        {
            // Id = 0 => insert (jeśli chcesz update, trzymaj Id mapy w VM)
            Id = 0,
            NameMap = MapName,
            UserId = UserSession.CurrentUser.Id,  // TEXT
            Date = DateTime.UtcNow.ToString("o"),
            Plus = 0,
            Minus = 0,
            MapsJson = mapsJson,
            ObjectJson = objectsJson,
            Desc = MapDescription
        };

        int newId = mapService.Save(record);
        MessageBox.Show($"Zapisano mapę");
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
            string mapsJson = SaveMapJson();
            string objectsJson = SaveObjectsJson();

            SaveToDatabase(mapsJson, objectsJson);
        }

        private string SaveMapJson()
        {
            var sb = new StringBuilder();

            sb.AppendLine("{");
            sb.AppendLine("  \"version\": 1.1,");
            sb.AppendLine("  \"tiledversion\": \"1.11.5\",");
            sb.AppendLine("  \"orientation\": \"orthogonal\",");
            sb.AppendLine("  \"renderorder\": \"right-down\",");
            sb.AppendLine($"  \"width\": {Columns},");
            sb.AppendLine($"  \"height\": {Rows},");
            sb.AppendLine("  \"tilewidth\": 32,");
            sb.AppendLine("  \"tileheight\": 32,");
            sb.AppendLine("  \"infinite\": false,");
            sb.AppendLine("  \"nextobjectid\": 1,");
            sb.AppendLine("  \"layers\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"name\": \"Walls\",");
            sb.AppendLine("      \"type\": \"tilelayer\",");
            sb.AppendLine("      \"visible\": true,");
            sb.AppendLine("      \"opacity\": 1,");
            sb.AppendLine("      \"x\": 0,");
            sb.AppendLine("      \"y\": 0,");
            sb.AppendLine($"      \"width\": {Columns},");
            sb.AppendLine($"      \"height\": {Rows},");
            sb.AppendLine("      \"data\": [");

            // 🔹 DATA – formatowana w wierszach jak siatka
            for (int r = 0; r < Rows; r++)
            {
                sb.Append("        ");

                for (int c = 0; c < Columns; c++)
                {
                    sb.Append(_grid[r, c]);

                    if (!(r == Rows - 1 && c == Columns - 1))
                        sb.Append(",");
                }

                sb.AppendLine();
            }

            sb.AppendLine("      ]");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"tilesets\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"firstgid\": 1,");
            sb.AppendLine("      \"source\": \"../../tileset.xml\"");
            sb.AppendLine("    }");
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"{MapName}.json"
                ),
                sb.ToString()
            );

            return sb.ToString();
        }





        private string SaveObjectsJson()
        {
            var sb = new StringBuilder();
            int mapHeight = Rows * CellSize;

            sb.AppendLine("{");

            // ========= PLAYER =========
            var player = FindSingle("player");
            if (player != null)
            {
                sb.AppendLine("  \"player\": {");
                sb.AppendLine($"    \"x\": {player.Value.x},");
                sb.AppendLine($"    \"y\": {player.Value.y}");
                sb.AppendLine("  },");
            }
            else
            {
                sb.AppendLine("  \"player\": null,");
            }

            // ========= ENEMIES =========
            sb.AppendLine("  \"enemies\": [");
            WriteObjects(sb, "enemies", mapHeight);
            sb.AppendLine("  ],");

            // ========= PICKABLES =========
            sb.AppendLine("  \"pickables\": [");
            WriteObjects(sb, "pickables", mapHeight);
            sb.AppendLine("  ],");

            // ========= OBJECTS =========
            sb.AppendLine("  \"objects\": [");
            WriteObjects(sb, "objects", mapHeight);
            sb.AppendLine("  ],");

            // ========= FINISH =========
            var finish = FindSingle("finish");
            if (finish != null)
            {
                sb.AppendLine("  \"finish\": {");
                sb.AppendLine($"    \"x\": {finish.Value.x},");
                sb.AppendLine($"    \"y\": {finish.Value.y}");
                sb.AppendLine("  }");
            }
            else
            {
                sb.AppendLine("  \"finish\": null");
            }

            sb.AppendLine("}");

            File.WriteAllText(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"{MapName}_objects.json"
                ),
                sb.ToString()
            );

            return sb.ToString();
        }

        private void WriteObjects(StringBuilder sb, string category, int mapHeight)
        {
            bool first = true;

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    var obj = _objectsGrid[r, c];
                    if (obj == null || obj.Category != category)
                        continue;

                    var meta = GetObjectMeta(obj.Key);
                    int h = meta?.h ?? CellSize;

                    double tileX = c * CellSize;
                    double tileY = r * CellSize;

                    double anchorX = tileX + CellSize / 2.0 + obj.OffsetX;
                    double anchorY = tileY + CellSize;

                    double centerX = anchorX;
                    double centerYTop = anchorY - h / 2.0;
                    double centerY = mapHeight - centerYTop;

                    if (!first)
                        sb.AppendLine(",");

                    first = false;

                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"type\": \"{obj.Type}\",");
                    sb.AppendLine($"      \"x\": {centerX},");
                    sb.AppendLine($"      \"y\": {centerY}");

                    // ✅ OPCJONALNE POLA NA KOŃCU
                    if (obj.PatrolDistance != null)
                        sb.AppendLine($",      \"patrol_distance\": {obj.PatrolDistance}");

                    if (!string.IsNullOrEmpty(obj.Direction))
                        sb.AppendLine($",      \"direction\": \"{obj.Direction}\"");

                    sb.Append("    }");
                }
            }

            sb.AppendLine();
        }


        private (double x, double y)? FindSingle(string key)
        {
            int mapHeight = Rows * CellSize;

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    var obj = _objectsGrid[r, c];
                    if (obj == null || obj.Key != key) continue;

                    var meta = GetObjectMeta(obj.Key);
                    int h = meta?.h ?? CellSize;

                    double tileX = c * CellSize;
                    double tileY = r * CellSize;

                    double anchorX = tileX + CellSize / 2.0 + obj.OffsetX;
                    double anchorY = tileY + CellSize;

                    double centerYTop = anchorY - h / 2.0;
                    double centerY = mapHeight - centerYTop;

                    if (key == "finish")
                    {
                        centerY = Math.Round(centerY);
                    }

                    return (anchorX, centerY);
                }
            }
            return null;
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
        // ===== TOP TABS: Kreator / Przeglądaj =====
        private string _topTab = "editor"; // "editor" | "browse"
        public string TopTab
        {
            get => _topTab;
            set
            {
                if (_topTab == value) return;
                _topTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEditorTab));
                OnPropertyChanged(nameof(IsBrowseTab));
            }
        }

        public bool IsEditorTab => TopTab == "editor";
        public bool IsBrowseTab => TopTab == "browse";

        public ICommand ShowEditorTabCommand { get; }
        public ICommand ShowBrowseTabCommand { get; }

        // ===== PRZEGLĄD MAP =====
        public ObservableCollection<MapCardVM> BrowseMaps { get; } = new();

        public ICommand RefreshBrowseMapsCommand { get; }
        public ICommand DownloadMapCommand { get; }
        public ICommand ToggleSaveMapCommand { get; }

        private string DbPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BazaDanych.db");

        private void RefreshBrowseMaps()
        {
            BrowseMaps.Clear();

            var svc = new MapService(DbPath);
            var rows = svc.GetAllMaps();

            foreach (var row in rows)
            {
                var card = new MapCardVM(row);

                if (UserSession.IsLoggedIn)
                    card.IsSaved = svc.IsSaved(UserSession.CurrentUser.Id, card.Id);
                else
                    card.IsSaved = false;

                BrowseMaps.Add(card);
            }
        }

        private void DownloadMap(MapCardVM card)
        {
            if (card == null) return;

            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            File.WriteAllText(Path.Combine(desktop, $"{card.NameMap}.json"), card.MapsJson ?? "");
            File.WriteAllText(Path.Combine(desktop, $"{card.NameMap}_objects.json"), card.ObjectJson ?? "");

            MessageBox.Show("Pobrano pliki na pulpit.");
        }

        private void ToggleSaveMap(MapCardVM card)
        {
            if (card == null) return;

            if (!UserSession.IsLoggedIn)
            {
                MessageBox.Show("Musisz być zalogowany, żeby zapisywać mapy.");
                return;
            }

            var svc = new MapService(DbPath);

            if (!card.IsSaved)
            {
                svc.SaveToUser(UserSession.CurrentUser.Id, card.Id);
                card.IsSaved = true;
            }
            else
            {
                svc.UnsaveFromUser(UserSession.CurrentUser.Id, card.Id);
                card.IsSaved = false;
            }
        }

        private void OpenMapInEditor(MapCardVM card)
        {
            if (card == null) return;

            try
            {
                LoadFromJsonStrings(card.MapsJson, card.ObjectJson);

                // nazwa/desc z bazy (jeśli chcesz)
                MapName = card.NameMap;
                MapDescription = card.Desc ?? "";

                // przełącz widok na edytor
                TopTab = "editor";

                // odśwież canvas
                RequestRender?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się wczytać mapy: " + ex.Message);
            }
        }

        private void LoadFromJsonStrings(string mapsJson, string objectsJson)
        {
            if (string.IsNullOrWhiteSpace(mapsJson))
                throw new Exception("MapsJson jest pusty.");

            // ==========================
            // 1) TILE LAYER (Tiled JSON)
            // ==========================
            using (var doc = JsonDocument.Parse(mapsJson))
            {
                var root = doc.RootElement;

                int w = root.GetProperty("width").GetInt32();
                int h = root.GetProperty("height").GetInt32();

                Rows = h;
                Columns = w;

                _grid = new int[Rows, Columns];
                _objectsGrid = new ObjectInstance[Rows, Columns];

                // layers[0].data
                var layers = root.GetProperty("layers");
                if (layers.GetArrayLength() == 0)
                    throw new Exception("Brak layers w MapsJson.");

                var data = layers[0].GetProperty("data");
                if (data.ValueKind != JsonValueKind.Array)
                    throw new Exception("layers[0].data nie jest tablicą.");

                int i = 0;
                for (int r = 0; r < Rows; r++)
                {
                    for (int c = 0; c < Columns; c++)
                    {
                        if (i >= data.GetArrayLength())
                            _grid[r, c] = 0;
                        else
                            _grid[r, c] = data[i].GetInt32();
                        i++;
                    }
                }
            }

            // ==========================
            // 2) OBJECTS JSON
            // ==========================
            if (string.IsNullOrWhiteSpace(objectsJson))
            {
                // brak obiektów – tylko kafelki
                return;
            }

            using (var docObj = JsonDocument.Parse(objectsJson))
            {
                var root = docObj.RootElement;

                int mapHeight = Rows * CellSize;

                // helper: world (x,y) -> (r,c,offsetX)
                (int r, int c, double offsetX) WorldToCell(double x, double y)
                {
                    // w Twoim formacie y rośnie do góry (jak w JSX)
                    // r liczymy od góry w WPF
                    double yTop = mapHeight - y;              // odwrócenie osi
                    int r = (int)Math.Floor(yTop / CellSize);
                    int c = (int)Math.Floor(x / CellSize);

                    r = Math.Max(0, Math.Min(Rows - 1, r));
                    c = Math.Max(0, Math.Min(Columns - 1, c));

                    double tileCenterX = c * CellSize + CellSize / 2.0;
                    double offsetX = x - tileCenterX;

                    // opcjonalnie przytnij do sensownego zakresu
                    if (offsetX < -CellSize / 2.0) offsetX = -CellSize / 2.0;
                    if (offsetX > CellSize / 2.0) offsetX = CellSize / 2.0;

                    return (r, c, offsetX);
                }

                string TypeToKey(string type)
                {
                    return type switch
                    {
                        "Player" => "player",
                        "Finish" => "finish",
                        "Sharkman" => "sharkman",
                        "Crabby" => "crabby",
                        "GoldenSkull" => "goldenSkull",
                        "HealthPotion" => "healthPotion",
                        "Diamond" => "diamond",
                        "GoldCoin" => "goldCoin",
                        "SilverCoin" => "silverCoin",
                        "Cannon" => "cannon",
                        "Spikes" => "spikes",
                        _ => type.Length > 0 ? char.ToLower(type[0]) + type.Substring(1) : type
                    };
                }

                string CategoryFromType(string type)
                {
                    return type switch
                    {
                        "Player" or "Finish" => "special",
                        "Cannon" or "Spikes" => "objects",
                        "Sharkman" or "Crabby" => "enemies",
                        _ => "pickables",
                    };
                }

                // ---- player
                if (root.TryGetProperty("player", out var playerEl) && playerEl.ValueKind == JsonValueKind.Object)
                {
                    double x = playerEl.GetProperty("x").GetDouble();
                    double y = playerEl.GetProperty("y").GetDouble();
                    var (r, c, offX) = seeCell(x, y);
                    _objectsGrid[r, c] = new ObjectInstance
                    {
                        Key = "player",
                        Type = "Player",
                        Category = "special",
                        OffsetX = offX
                    };
                }

                // ---- finish
                if (root.TryGetProperty("finish", out var finishEl) && finishEl.ValueKind == JsonValueKind.Object)
                {
                    double x = finishEl.GetProperty("x").GetDouble();
                    double y = finishEl.GetProperty("y").GetDouble();
                    var (r, c, offX) = seeCell(x, y);
                    _objectsGrid[r, c] = new ObjectInstance
                    {
                        Key = "finish",
                        Type = "Finish",
                        Category = "special",
                        OffsetX = offX
                    };
                }

                // helper local func (C# requires defined before use if you want; easiest: use a lambda)
                (int r, int c, double offsetX) seeCell(double x, double y) => WorldToCell(x, y);

                // ---- enemies[]
                if (root.TryGetProperty("enemies", out var enemiesEl) && enemiesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in enemiesEl.EnumerateArray())
                    {
                        if (e.ValueKind != JsonValueKind.Object) continue;

                        string type = e.GetProperty("type").GetString() ?? "";
                        double x = e.GetProperty("x").GetDouble();
                        double y = e.GetProperty("y").GetDouble();

                        var (r, c, offX) = seeCell(x, y);

                        int? patrol = null;
                        if (e.TryGetProperty("patrol_distance", out var pdEl) && pdEl.ValueKind == JsonValueKind.Number)
                            patrol = pdEl.GetInt32();

                        _objectsGrid[r, c] = new ObjectInstance
                        {
                            Key = TypeToKey(type),
                            Type = type,
                            Category = CategoryFromType(type),
                            OffsetX = offX,
                            PatrolDistance = patrol
                        };
                    }
                }

                // ---- pickables[]
                if (root.TryGetProperty("pickables", out var pickEl) && pickEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in pickEl.EnumerateArray())
                    {
                        if (p.ValueKind != JsonValueKind.Object) continue;

                        string type = p.GetProperty("type").GetString() ?? "";
                        double x = p.GetProperty("x").GetDouble();
                        double y = p.GetProperty("y").GetDouble();

                        var (r, c, offX) = seeCell(x, y);

                        _objectsGrid[r, c] = new ObjectInstance
                        {
                            Key = TypeToKey(type),
                            Type = type,
                            Category = "pickables",
                            OffsetX = offX
                        };
                    }
                }

                // ---- objects[]
                if (root.TryGetProperty("objects", out var objsEl) && objsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var o in objsEl.EnumerateArray())
                    {
                        if (o.ValueKind != JsonValueKind.Object) continue;

                        string type = o.GetProperty("type").GetString() ?? "";
                        double x = o.GetProperty("x").GetDouble();
                        double y = o.GetProperty("y").GetDouble();

                        var (r, c, offX) = seeCell(x, y);

                        string dir = null;
                        if (o.TryGetProperty("direction", out var dirEl) && dirEl.ValueKind == JsonValueKind.String)
                            dir = dirEl.GetString();

                        _objectsGrid[r, c] = new ObjectInstance
                        {
                            Key = TypeToKey(type),
                            Type = type,
                            Category = "objects",
                            OffsetX = offX,
                            Direction = dir
                        };
                    }
                }
            }

            // po wczytaniu odśwież UI (XAML bindingi)
            OnPropertyChanged(nameof(Rows));
            OnPropertyChanged(nameof(Columns));
        }

    }
}
