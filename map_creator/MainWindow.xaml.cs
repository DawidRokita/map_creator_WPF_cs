using map_creator.Models;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace map_creator
{
    public partial class MainWindow : Window
    {
        private int _rows = 21;
        private int _cols = 34;
        private const int CellSize = 32;

        private int[,] _grid; // [r,c] -> tileId

        private string _mapName = "mapa";


        private readonly Stack<int[,]> _undoStack = new();
        private readonly Stack<int[,]> _redoStack = new();


        private List<Tile> _tiles = new();
        private int _selectedTileId = -1;
        private int _selectedTileVisualId = -1;

        private bool _isPainting = false;
        private bool _isRightPainting = false;

        // zoom
        private double _zoom = 1.0;

        // undo/redo stosy
        //private Stack<int[,]> _undoStack = new();
        //private Stack<int[,]> _redoStack = new();

        // kafelki które mają być "puste"
        private readonly int[] EMPTY_TILE_IDS = new int[]
        {
            4,6,9,12,15,17,21,23,26,29,32,38,40,41,42,43,44,45,46,47,
            48,49,50,51,52,53,54,55,56,57,60,63,66,72,74,77,80,83
        };

        public MainWindow()
        {
            InitializeComponent();
            this.KeyDown += MainWindow_KeyDown;

            MapNameTextBox.Text = _mapName;
            MapNameTextBox.TextChanged += (s, e) =>
            {
                _mapName = MapNameTextBox.Text.Trim();
            };



            _grid = CreateGrid(_rows, _cols);
            LoadTiles();
            RenderGrid();
            UpdateCanvasSize();
        }

        private int[,] CreateGrid(int rows, int cols)
        {
            var g = new int[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    g[r, c] = 0;
            return g;
        }

        private void LoadTiles()
        {
            int totalCols = 17;   // liczba kolumn w tilesecie
            int totalRows = 5;    // liczba wierszy
            int tileCount = 85;   // liczba kafelków

            // kolumny do pominięcia (liczone od 0)
            var skippedCols = new HashSet<int> { 5, 11 }; // pomijamy 6 i 12 kolumnę

            // definicja bloków (kolumny 1–5, 7–11, 13–17)
            var groupRanges = new List<(int Start, int End)>
    {
        (0, 4),
        (6, 10),
        (12, 16)
    };

            var allTiles = new List<Tile>();
            for (int i = 1; i <= tileCount; i++)
            {
                var t = new Tile
                {
                    VisualId = i,
                    Id = EMPTY_TILE_IDS.Contains(i) ? 0 : i,
                    Src = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tiles", $"tile_{i}.png")
                };
                allTiles.Add(t);
            }

            _tiles = allTiles;
            TilesPanel.Children.Clear();

            // dla każdej grupy (bloku 5x5)
            foreach (var (startCol, endCol) in groupRanges)
            {
                var grid = new UniformGrid
                {
                    Columns = (endCol - startCol + 1),
                    Rows = totalRows,
                    Margin = new Thickness(0, 0, 0, 12),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                for (int row = 0; row < totalRows; row++)
                {
                    for (int col = startCol; col <= endCol; col++)
                    {
                        if (skippedCols.Contains(col))
                            continue;

                        int tileIndex = row * totalCols + col + 1;
                        if (tileIndex > tileCount)
                            continue;

                        var tile = allTiles.FirstOrDefault(t => t.VisualId == tileIndex);
                        if (tile == null)
                            continue;

                        var img = new Image
                        {
                            Width = CellSize,
                            Height = CellSize,
                            Stretch = Stretch.Fill
                        };

                        if (File.Exists(tile.Src))
                            img.Source = new BitmapImage(new Uri(tile.Src, UriKind.Absolute));

                        var border = new Border
                        {
                            Width = CellSize,
                            Height = CellSize,
                            Child = img,
                            Tag = tile,
                            CornerRadius = new CornerRadius(4),
                            Margin = new Thickness(2),
                            BorderThickness = new Thickness(0)
                        };

                        border.MouseLeftButtonDown += Tile_MouseLeftButtonDown;
                        grid.Children.Add(border);
                    }
                }

                TilesPanel.Children.Add(grid);
            }
        }

        private void Tile_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border) return;
            if (border.Tag is not Tile t) return;

            _selectedTileId = t.Id;
            _selectedTileVisualId = t.VisualId;

            // wyczyść ramki we wszystkich grupach
            foreach (var grid in TilesPanel.Children.OfType<UniformGrid>())
            {
                foreach (var child in grid.Children.OfType<Border>())
                {
                    child.BorderThickness = new Thickness(0);
                    child.BorderBrush = null;
                }
            }

            // zaznacz kliknięty kafelek
            border.BorderThickness = new Thickness(2);
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(99, 157, 109));
        }


        private void RenderGrid()
        {
            MapCanvas.Children.Clear();

            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    var rect = new Border
                    {
                        Width = CellSize,
                        Height = CellSize,
                        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 65, 65, 65)),
                        BorderThickness = new Thickness(1),
                        Background = Brushes.Transparent,
                        Tag = (r, c)
                    };

                    int tileId = _grid[r, c];
                    if (tileId > 0)
                    {
                        var tile = _tiles.FirstOrDefault(x => x.Id == tileId);
                        if (tile != null && File.Exists(tile.Src))
                        {
                            rect.Background = new ImageBrush(new BitmapImage(new Uri(tile.Src, UriKind.Absolute)))
                            {
                                Stretch = Stretch.Fill
                            };
                        }
                    }

                    Canvas.SetLeft(rect, c * CellSize);
                    Canvas.SetTop(rect, r * CellSize);

                    MapCanvas.Children.Add(rect);
                }
            }
        }

        private void UpdateCanvasSize()
        {
            MapCanvas.Width = _cols * CellSize;
            MapCanvas.Height = _rows * CellSize;
        }

        // ===== MALOWANIE =====

        private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isPainting = true;
            _isRightPainting = false;
            PushUndo();
            _redoStack.Clear();

            PaintAtMouse(e.GetPosition(MapCanvas), false);
            MapCanvas.CaptureMouse();
        }

        private void MapCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isPainting = true;
            _isRightPainting = true;
            PushUndo();
            _redoStack.Clear();

            PaintAtMouse(e.GetPosition(MapCanvas), true);
            MapCanvas.CaptureMouse();
        }

        private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPainting) return;
            PaintAtMouse(e.GetPosition(MapCanvas), _isRightPainting);
        }

        private void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isPainting = false;
            MapCanvas.ReleaseMouseCapture();
        }

        private void PaintAtMouse(Point p, bool isRightClick)
        {
            int c = (int)(p.X / CellSize);
            int r = (int)(p.Y / CellSize);

            if (r < 0 || r >= _rows || c < 0 || c >= _cols) return;

            if (!isRightClick && _selectedTileId < 0) return;

            int newValue = isRightClick ? 0 : _selectedTileId;

            if (_grid[r, c] != newValue)
            {
                _grid[r, c] = newValue;
                // zaktualizuj tylko ten jeden
                var tile = _tiles.FirstOrDefault(x => x.Id == newValue);
                var index = r * _cols + c;
                if (index >= 0 && index < MapCanvas.Children.Count)
                {
                    if (MapCanvas.Children[index] is Border rect)
                    {
                        if (newValue == 0)
                        {
                            rect.Background = Brushes.Transparent;
                        }
                        else if (tile != null && File.Exists(tile.Src))
                        {
                            rect.Background = new ImageBrush(new BitmapImage(new Uri(tile.Src, UriKind.Absolute)))
                            {
                                Stretch = Stretch.Fill
                            };
                        }
                    }
                }
            }
        }

        // ===== ZOOM =====

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(Math.Min(_zoom + 0.05, 3.0));
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(Math.Max(_zoom - 0.05, 0.3));
        }

        private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // tak jak u Ciebie: z modyfikatorem
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Delta > 0)
                    SetZoom(Math.Min(_zoom + 0.05, 3.0));
                else
                    SetZoom(Math.Max(_zoom - 0.05, 0.3));

                e.Handled = true;
            }
        }

        private void SetZoom(double value)
        {
            // magnes do 1.0
            if (Math.Abs(value - 1.0) < 0.05)
                value = 1.0;

            _zoom = Math.Round(value, 2);
            MapScale.ScaleX = _zoom;
            MapScale.ScaleY = _zoom;
            ZoomLabel.Content = $"{(int)(_zoom * 100)}%";
        }

        // ===== ZMIANA ROZMIARU =====

        private void ApplySizeButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyNewSize();
        }

        private void ColsTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyNewSize();
        }

        private void RowsTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyNewSize();
        }

        private void ApplyNewSize()
        {
            if (!int.TryParse(ColsTextBox.Text, out int newCols)) newCols = 10;
            if (!int.TryParse(RowsTextBox.Text, out int newRows)) newRows = 10;

            newCols = Math.Max(10, newCols);
            newRows = Math.Max(10, newRows);

            var newGrid = new int[newRows, newCols];
            for (int r = 0; r < newRows; r++)
            {
                for (int c = 0; c < newCols; c++)
                {
                    if (r < _rows && c < _cols)
                        newGrid[r, c] = _grid[r, c];
                    else
                        newGrid[r, c] = 0;
                }
            }

            PushUndo();
            _grid = newGrid;
            _rows = newRows;
            _cols = newCols;

            RenderGrid();
            UpdateCanvasSize();
        }

        // ===== UNDO / REDO =====

        private void PushUndo()
        {
            _undoStack.Push(CloneGrid(_grid));
        }

        private int[,] CloneGrid(int[,] src)
        {
            int rows = src.GetLength(0);
            int cols = src.GetLength(1);
            var copy = new int[rows, cols];
            Array.Copy(src, copy, src.Length);
            return copy;
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count == 0) return;
            _redoStack.Push(CloneGrid(_grid));
            _grid = _undoStack.Pop();
            _rows = _grid.GetLength(0);
            _cols = _grid.GetLength(1);
            ColsTextBox.Text = _cols.ToString();
            RowsTextBox.Text = _rows.ToString();
            RenderGrid();
            UpdateCanvasSize();
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count == 0) return;
            _undoStack.Push(CloneGrid(_grid));
            _grid = _redoStack.Pop();
            _rows = _grid.GetLength(0);
            _cols = _grid.GetLength(1);
            ColsTextBox.Text = _cols.ToString();
            RowsTextBox.Text = _rows.ToString();
            RenderGrid();
            UpdateCanvasSize();
        }

        // ===== ZAPIS =====

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Zbierz dane mapy
            var flatData = new List<int>();
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    flatData.Add(_grid[r, c]);

            // Zbuduj "data" w formacie wierszy (jak w Tiled)
            var formattedData = new StringBuilder();
            formattedData.AppendLine("[");
            for (int r = 0; r < _rows; r++)
            {
                var rowValues = new List<string>();
                for (int c = 0; c < _cols; c++)
                    rowValues.Add(_grid[r, c].ToString());

                formattedData.Append("  " + string.Join(",", rowValues));

                if (r < _rows - 1)
                    formattedData.AppendLine(",");
                else
                    formattedData.AppendLine();
            }
            formattedData.Append("]");

            // Zbuduj pełny JSON mapy
            string json = $@"{{
    ""version"": 1.1,
    ""tiledversion"": ""1.11.5"",
    ""orientation"": ""orthogonal"",
    ""renderorder"": ""right-down"",
    ""width"": {_cols},
    ""height"": {_rows},
    ""tilewidth"": {CellSize},
    ""tileheight"": {CellSize},
    ""infinite"": false,
    ""nextobjectid"": 1,
    ""layers"": [
    {{
        ""name"": ""Platformy"",
        ""type"": ""tilelayer"",
        ""visible"": true,
        ""opacity"": 1,
        ""x"": 0,
        ""y"": 0,
        ""width"": {_cols},
        ""height"": {_rows},
        ""data"": {
            formattedData
        }
    }}
    ],
    ""tilesets"": [
    {{
        ""firstgid"": 1,
        ""source"": ""tileset.xml""
    }}
    ]
    }}";

            // Zapisz plik JSON
            var fileName = $"{_mapName}.json";
            var savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
            File.WriteAllText(savePath, json, Encoding.UTF8);

            MessageBox.Show($"Mapa została zapisana na pulpicie jako:\n{fileName}", "Zapis mapy", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        // ===== CLEAR =====

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Czy na pewno chcesz wyczyścić mapę?", "Potwierdź", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                PushUndo();
                _grid = CreateGrid(_rows, _cols);
                RenderGrid();
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            // Ctrl + Z → COFNIJ
            if (ctrl && e.Key == Key.Z && !shift)
            {
                e.Handled = true;
                UndoAction();
            }

            // Ctrl + Y lub Ctrl + Shift + Z → PRZYWRÓĆ
            if (ctrl && (e.Key == Key.Y || (e.Key == Key.Z && shift)))
            {
                e.Handled = true;
                RedoAction();
            }
        }

        private void SaveStateForUndo()
        {
            // skopiuj stan do cofania
            _undoStack.Push((int[,])_grid.Clone());
            _redoStack.Clear();
        }

        private void UndoAction()
        {
            if (_undoStack.Count > 0)
            {
                _redoStack.Push((int[,])_grid.Clone());
                _grid = _undoStack.Pop();
                RenderGrid();
            }
        }

        private void RedoAction()
        {
            if (_redoStack.Count > 0)
            {
                _undoStack.Push((int[,])_grid.Clone());
                _grid = _redoStack.Pop();
                RenderGrid();
            }
        }


    }
}