using map_creator.ViewModels;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace map_creator
{
    public partial class MainWindow : Window
    {
        private bool _painting;
        private bool _erase;
        private MainViewModel VM;

        public MainWindow()
        {
            InitializeComponent();
            VM = (MainViewModel)DataContext;
            VM.RequestRender += Render;
            Loaded += (_, _) => Render();
        }

        private void Render()
        {
            MapCanvas.Children.Clear();
            MapCanvas.Width = VM.Columns * MainViewModel.CellSize;
            MapCanvas.Height = VM.Rows * MainViewModel.CellSize;

            // ==== kafelki ====
            var grid = VM.GetGrid();
            var tiles = VM.AllTiles;

            for (int r = 0; r < VM.Rows; r++)
                for (int c = 0; c < VM.Columns; c++)
                {
                    var rect = new Border
                    {
                        Width = MainViewModel.CellSize,
                        Height = MainViewModel.CellSize,
                        BorderBrush = new SolidColorBrush(Color.FromRgb(65, 65, 65)),
                        BorderThickness = new Thickness(1),
                        Background = Brushes.Transparent
                    };

                    int id = grid[r, c];
                    if (id > 0)
                    {
                        var tile = tiles.First(x => x.Id == id);
                        rect.Background = new ImageBrush(new BitmapImage(new System.Uri(tile.Src)))
                        { Stretch = Stretch.Fill };
                    }

                    Canvas.SetLeft(rect, c * MainViewModel.CellSize);
                    Canvas.SetTop(rect, r * MainViewModel.CellSize);
                    MapCanvas.Children.Add(rect);
                }

            // ==== obiekty (rysowanie nad kafelkami) ====
            var objs = VM.GetObjectsGrid();
            for (int r = 0; r < VM.Rows; r++)
                for (int c = 0; c < VM.Columns; c++)
                {
                    var obj = objs[r, c];
                    if (obj == null) continue;

                    var meta = VM.GetObjectMeta(obj.Key);
                    if (meta == null) continue;

                    int w = meta.Value.w;
                    int h = meta.Value.h;

                    double tileX = c * MainViewModel.CellSize;
                    double tileY = r * MainViewModel.CellSize;

                    double anchorX = tileX + MainViewModel.CellSize / 2.0 + obj.OffsetX;
                    double anchorY = tileY + MainViewModel.CellSize;

                    double drawX = anchorX - w / 2.0;
                    double drawY = anchorY - h;

                    // jak w JSX: Player lekko wyżej (offset +8)
                    if (obj.Type == "Player")
                        drawY += 8;

                    var img = new Image
                    {
                        Width = w,
                        Height = h,
                        Source = new BitmapImage(new System.Uri(meta.Value.src)),
                        Stretch = Stretch.Uniform
                    };

                    // cannon RIGHT -> flip
                    if (obj.Type == "Cannon" && obj.Direction == "RIGHT")
                    {
                        img.RenderTransform = new ScaleTransform(-1, 1, w / 2.0, h / 2.0);
                    }

                    Canvas.SetLeft(img, drawX);
                    Canvas.SetTop(img, drawY);
                    Panel.SetZIndex(img, 10);
                    MapCanvas.Children.Add(img);

                    // ===== SHARKMAN PATROL DISTANCE (STRZAŁKI) =====
                    if (obj.Type == "Sharkman" && obj.PatrolDistance.HasValue && obj.PatrolDistance.Value > 0)
                    {
                        double pd = obj.PatrolDistance.Value;

                        double y = anchorY - h / 2.0;

                        // linia
                        var line = new Line
                        {
                            X1 = anchorX - pd,
                            X2 = anchorX + pd,
                            Y1 = y,
                            Y2 = y,
                            Stroke = Brushes.Cyan,
                            StrokeThickness = 2
                        };
                        Panel.SetZIndex(line, 5);
                        MapCanvas.Children.Add(line);

                        // strzałka lewa
                        MapCanvas.Children.Add(new Polygon
                        {
                            Fill = Brushes.Cyan,
                            Points = new PointCollection
        {
            new(anchorX - pd, y),
            new(anchorX - pd + 6, y - 4),
            new(anchorX - pd + 6, y + 4),
        }
                        });

                        // strzałka prawa
                        MapCanvas.Children.Add(new Polygon
                        {
                            Fill = Brushes.Cyan,
                            Points = new PointCollection
        {
            new(anchorX + pd, y),
            new(anchorX + pd - 6, y - 4),
            new(anchorX + pd - 6, y + 4),
        }
                        });
                    }


                }

        }

        private void MapCanvas_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            _painting = true;
            _erase = false;

            PaintOrErase(e.GetPosition(MapCanvas));
            MapCanvas.CaptureMouse();
        }

        private void MapCanvas_MouseRightButtonDown(object s, MouseButtonEventArgs e)
        {
            _painting = true;
            _erase = true;

            PaintOrErase(e.GetPosition(MapCanvas));
            MapCanvas.CaptureMouse();

            e.Handled = true;
        }

        private void MapCanvas_MouseMove(object s, MouseEventArgs e)
        {
            if (!_painting) return;
            PaintOrErase(e.GetPosition(MapCanvas));
        }

        private void MapCanvas_MouseLeftButtonUp(object s, MouseButtonEventArgs e)
        {
            _painting = false;
            _erase = false;
            MapCanvas.ReleaseMouseCapture();
        }

        private void MapCanvas_MouseRightButtonUp(object s, MouseButtonEventArgs e)
        {
            _painting = false;
            _erase = false;
            MapCanvas.ReleaseMouseCapture();

            e.Handled = true;
        }

        private void PaintOrErase(Point p)
        {
            if (VM.IsTilesMode)
            {
                int c = (int)(p.X / MainViewModel.CellSize);
                int r = (int)(p.Y / MainViewModel.CellSize);
                VM.PaintTile(r, c, _erase);
            }
            else
            {
                // obiekty: LMB stawia, RMB usuwa
                if (_erase) VM.EraseObjectAt(p.X, p.Y);
                else VM.PlaceObjectAt(p.X, p.Y);
            }
        }

        private void MapCanvas_MouseWheel(object s, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (e.Delta > 0) VM.ZoomInCommand.Execute(null);
                else VM.ZoomOutCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (e.Key == Key.Z) VM.UndoCommand.Execute(null);
                if (e.Key == Key.Y) VM.RedoCommand.Execute(null);
            }
        }
    }
}
