using map_creator.ViewModels;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using map_creator.Session;

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

        private ObjectInstance _draggedObject;
        private int _dragStartRow;
        private int _dragStartCol;


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

                    // ===== SHARKMAN PATROL DISTANCE (NIEBIESKIE STRZAŁKI) =====
                    if (obj.Type == "Sharkman" && obj.PatrolDistance.HasValue)
                    {
                        double dist = obj.PatrolDistance.Value;

                        double centerY = anchorY - h / 2.0;
                        double leftX = anchorX - dist;
                        double rightX = anchorX + dist;

                        Brush arrowBrush = new SolidColorBrush(Color.FromRgb(80, 170, 255));
                        double arrowSize = 6;

                        // linia lewa
                        var leftLine = new Line
                        {
                            X1 = anchorX,
                            Y1 = centerY,
                            X2 = leftX,
                            Y2 = centerY,
                            Stroke = arrowBrush,
                            StrokeThickness = 2
                        };

                        // grot ←
                        var leftArrow1 = new Line
                        {
                            X1 = leftX,
                            Y1 = centerY,
                            X2 = leftX + arrowSize,
                            Y2 = centerY - arrowSize,
                            Stroke = arrowBrush,
                            StrokeThickness = 2
                        };

                        var leftArrow2 = new Line
                        {
                            X1 = leftX,
                            Y1 = centerY,
                            X2 = leftX + arrowSize,
                            Y2 = centerY + arrowSize,
                            Stroke = arrowBrush,
                            StrokeThickness = 2
                        };

                        // linia prawa
                        var rightLine = new Line
                        {
                            X1 = anchorX,
                            Y1 = centerY,
                            X2 = rightX,
                            Y2 = centerY,
                            Stroke = arrowBrush,
                            StrokeThickness = 2
                        };

                        // grot →
                        var rightArrow1 = new Line
                        {
                            X1 = rightX,
                            Y1 = centerY,
                            X2 = rightX - arrowSize,
                            Y2 = centerY - arrowSize,
                            Stroke = arrowBrush,
                            StrokeThickness = 2
                        };

                        var rightArrow2 = new Line
                        {
                            X1 = rightX,
                            Y1 = centerY,
                            X2 = rightX - arrowSize,
                            Y2 = centerY + arrowSize,
                            Stroke = arrowBrush,
                            StrokeThickness = 2
                        };

                        Panel.SetZIndex(leftLine, 5);
                        Panel.SetZIndex(rightLine, 5);

                        MapCanvas.Children.Add(leftLine);
                        MapCanvas.Children.Add(leftArrow1);
                        MapCanvas.Children.Add(leftArrow2);

                        MapCanvas.Children.Add(rightLine);
                        MapCanvas.Children.Add(rightArrow1);
                        MapCanvas.Children.Add(rightArrow2);
                    }




                }

        }



        private void LogoutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // wyczyść sesję
            UserSession.Logout();

            // otwórz okno logowania
            var login = new LoginWindow();
            login.Show();

            // zamknij główne okno
            this.Close();
        }

        private void UserButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement =
                    System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }



        private void MapCanvas_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            var p = e.GetPosition(MapCanvas);

            if (VM.IsObjectsMode)
            {
                var hit = VM.FindObjectAtPosition(p.X, p.Y);
                if (hit != null)
                {
                    _draggedObject = hit.Value.obj;
                    _dragStartRow = hit.Value.r;
                    _dragStartCol = hit.Value.c;

                    VM.SelectObjectAt(p.X, p.Y);
                    MapCanvas.CaptureMouse();
                    return;
                }
            }

            _painting = true;
            _erase = false;
            PaintOrErase(p);
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
            var p = e.GetPosition(MapCanvas);

            if (_draggedObject != null)
            {
                int r = (int)(p.Y / MainViewModel.CellSize);
                int c = (int)(p.X / MainViewModel.CellSize);

                if (r < 0 || c < 0 || r >= VM.Rows || c >= VM.Columns)
                    return;

                var grid = VM.GetObjectsGrid();

                if (grid[r, c] == null)
                {
                    grid[_dragStartRow, _dragStartCol] = null;
                    grid[r, c] = _draggedObject;

                    _dragStartRow = r;
                    _dragStartCol = c;

                    VM.InvalidateRender();

                }
                return;
            }

            if (_painting)
                PaintOrErase(p);
        }


        private void MapCanvas_MouseLeftButtonUp(object s, MouseButtonEventArgs e)
        {
            _painting = false;
            _draggedObject = null;
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
