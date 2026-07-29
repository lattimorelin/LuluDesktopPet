using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LuluDesktopPet
{
    public sealed class DayStats
    {
        public Dictionary<string, long> Keyboard { get; set; }
        public Dictionary<string, long> Mouse { get; set; }

        public DayStats()
        {
            Keyboard = new Dictionary<string, long>();
            Mouse = new Dictionary<string, long>();
        }
    }

    public sealed class StatsFile
    {
        public Dictionary<string, DayStats> Days { get; set; }

        public StatsFile()
        {
            Days = new Dictionary<string, DayStats>();
        }
    }

    public sealed class StatRow
    {
        public string Name { get; set; }
        public long Count { get; set; }
        public string Group { get; set; }
    }

    public sealed class StatsStore
    {
        private readonly object sync = new object();
        private readonly string filePath;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private StatsFile data;
        private bool dirty;

        public event Action Changed;

        public StatsStore()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LuluDesktopPet");
            Directory.CreateDirectory(folder);
            filePath = Path.Combine(folder, "stats.json");
            data = Load();
        }

        private StatsFile Load()
        {
            try
            {
                if (!File.Exists(filePath)) return new StatsFile();
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                StatsFile loaded = serializer.Deserialize<StatsFile>(json);
                return loaded ?? new StatsFile();
            }
            catch
            {
                return new StatsFile();
            }
        }

        private DayStats TodayUnsafe()
        {
            string key = DateTime.Now.ToString("yyyy-MM-dd");
            DayStats day;
            if (!data.Days.TryGetValue(key, out day))
            {
                day = new DayStats();
                data.Days[key] = day;
            }
            return day;
        }

        public void AddKeyboard(string keyName)
        {
            lock (sync)
            {
                DayStats day = TodayUnsafe();
                long value;
                day.Keyboard.TryGetValue(keyName, out value);
                day.Keyboard[keyName] = value + 1;
                dirty = true;
            }
            FireChanged();
        }

        public void AddMouse(string buttonName)
        {
            lock (sync)
            {
                DayStats day = TodayUnsafe();
                long value;
                day.Mouse.TryGetValue(buttonName, out value);
                day.Mouse[buttonName] = value + 1;
                dirty = true;
            }
            FireChanged();
        }

        private void FireChanged()
        {
            Action handler = Changed;
            if (handler != null) handler();
        }

        public long TodayKeyboardTotal()
        {
            lock (sync) return TodayUnsafe().Keyboard.Values.Sum();
        }

        public long TodayMouseClickTotal()
        {
            lock (sync)
            {
                return TodayUnsafe().Mouse
                    .Where(x => !x.Key.StartsWith("滚轮"))
                    .Sum(x => x.Value);
            }
        }

        public List<StatRow> GetKeyboardRows(string dateKey)
        {
            lock (sync)
            {
                DayStats day;
                if (!data.Days.TryGetValue(dateKey, out day)) return new List<StatRow>();
                return day.Keyboard
                    .Select(x => new StatRow { Name = x.Key, Count = x.Value, Group = KeyNames.GroupOf(x.Key) })
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Name)
                    .ToList();
            }
        }

        public List<StatRow> GetMouseRows(string dateKey)
        {
            lock (sync)
            {
                DayStats day;
                if (!data.Days.TryGetValue(dateKey, out day)) return new List<StatRow>();
                return day.Mouse
                    .Select(x => new StatRow { Name = x.Key, Count = x.Value, Group = "鼠标" })
                    .OrderByDescending(x => x.Count)
                    .ToList();
            }
        }

        public void Save()
        {
            string json;
            lock (sync)
            {
                if (!dirty) return;
                json = serializer.Serialize(data);
                dirty = false;
            }

            string temp = filePath + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            if (File.Exists(filePath))
            {
                File.Replace(temp, filePath, null);
            }
            else
            {
                File.Move(temp, filePath);
            }
        }

        public string StoragePath { get { return filePath; } }
    }

    public static class KeyNames
    {
        private const int MAPVK_VSC_TO_VK_EX = 3;

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetKeyNameText(int lParam, StringBuilder text, int size);

        public static string FromRaw(ushort virtualKey, ushort scanCode, ushort flags)
        {
            bool extended = (flags & 0x02) != 0;
            int vk = virtualKey;

            if (vk == 0x10)
            {
                uint mapped = MapVirtualKey(scanCode, MAPVK_VSC_TO_VK_EX);
                vk = mapped == 0xA1 ? 0xA1 : 0xA0;
            }
            else if (vk == 0x11) vk = extended ? 0xA3 : 0xA2;
            else if (vk == 0x12) vk = extended ? 0xA5 : 0xA4;

            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
            if (vk >= 0x60 && vk <= 0x69) return "数字键盘 " + (vk - 0x60);

            Dictionary<int, string> names = FriendlyNames;
            string value;
            if (vk == 0x0D && extended) return "数字键盘 Enter";
            if (names.TryGetValue(vk, out value)) return value;

            int keyData = (scanCode << 16) | (extended ? (1 << 24) : 0);
            StringBuilder sb = new StringBuilder(64);
            if (GetKeyNameText(keyData, sb, sb.Capacity) > 0 && sb.Length > 0)
                return sb.ToString();
            return "按键 0x" + vk.ToString("X2") + " (扫描码 " + scanCode.ToString("X2") + ")";
        }

        public static string PhysicalId(ushort virtualKey, ushort scanCode, ushort flags)
        {
            return virtualKey.ToString("X4") + "-" + scanCode.ToString("X4") + "-" + (flags & 0x06).ToString("X2");
        }

        public static string GroupOf(string name)
        {
            if (name.Length == 1 && name[0] >= 'A' && name[0] <= 'Z') return "字母";
            if (name.Length == 1 && name[0] >= '0' && name[0] <= '9') return "数字";
            if (name.StartsWith("F") && name.Length <= 3) return "功能键";
            if (name.StartsWith("数字键盘")) return "数字键盘";
            if (name.Contains("Shift") || name.Contains("Ctrl") || name.Contains("Alt") || name.Contains("Windows"))
                return "修饰键";
            return "其他按键";
        }

        private static readonly Dictionary<int, string> FriendlyNames = new Dictionary<int, string>
        {
            { 0x08, "Backspace（退格）" }, { 0x09, "Tab" }, { 0x0D, "Enter（回车）" },
            { 0x13, "Pause" }, { 0x14, "Caps Lock" }, { 0x1B, "Esc" },
            { 0x20, "Space（空格）" }, { 0x21, "Page Up" }, { 0x22, "Page Down" },
            { 0x23, "End" }, { 0x24, "Home" }, { 0x25, "←" }, { 0x26, "↑" },
            { 0x27, "→" }, { 0x28, "↓" }, { 0x2C, "Print Screen" }, { 0x2D, "Insert" },
            { 0x2E, "Delete（删除）" }, { 0x5B, "左 Windows" }, { 0x5C, "右 Windows" },
            { 0x5D, "菜单键" }, { 0x6A, "数字键盘 *" }, { 0x6B, "数字键盘 +" },
            { 0x6D, "数字键盘 -" }, { 0x6E, "数字键盘 ." }, { 0x6F, "数字键盘 /" },
            { 0x90, "Num Lock" }, { 0x91, "Scroll Lock" },
            { 0xA0, "左 Shift" }, { 0xA1, "右 Shift" }, { 0xA2, "左 Ctrl" },
            { 0xA3, "右 Ctrl" }, { 0xA4, "左 Alt" }, { 0xA5, "右 Alt" },
            { 0xAD, "静音" }, { 0xAE, "音量减" }, { 0xAF, "音量加" },
            { 0xB0, "下一曲" }, { 0xB1, "上一曲" }, { 0xB2, "停止" }, { 0xB3, "播放/暂停" },
            { 0xBA, ";" }, { 0xBB, "=" }, { 0xBC, "," }, { 0xBD, "-" },
            { 0xBE, "." }, { 0xBF, "/" }, { 0xC0, "`" }, { 0xDB, "[" },
            { 0xDC, "\\" }, { 0xDD, "]" }, { 0xDE, "'" }
        };
    }

    public sealed class GlobalInputTracker : IDisposable
    {
        private const int WM_INPUT = 0x00FF;
        private const int RID_INPUT = 0x10000003;
        private const int RIM_TYPEMOUSE = 0;
        private const int RIM_TYPEKEYBOARD = 1;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private readonly HwndSource source;
        private readonly StatsStore store;
        private readonly HashSet<string> keysDown = new HashSet<string>();

        public bool Paused { get; set; }
        public event Action KeyboardActivity;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct RAWMOUSE
        {
            [FieldOffset(0)] public ushort usFlags;
            [FieldOffset(4)] public uint ulButtons;
            [FieldOffset(4)] public ushort usButtonFlags;
            [FieldOffset(6)] public ushort usButtonData;
            [FieldOffset(8)] public uint ulRawButtons;
            [FieldOffset(12)] public int lLastX;
            [FieldOffset(16)] public int lLastY;
            [FieldOffset(20)] public uint ulExtraInformation;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            RAWINPUTDEVICE[] devices, uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(
            IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

        public GlobalInputTracker(Window window, StatsStore store)
        {
            this.store = store;
            IntPtr handle = new WindowInteropHelper(window).Handle;
            source = HwndSource.FromHwnd(handle);
            source.AddHook(WndProc);

            RAWINPUTDEVICE[] devices = new RAWINPUTDEVICE[]
            {
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x06, dwFlags = RIDEV_INPUTSINK, hwndTarget = handle },
                new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x02, dwFlags = RIDEV_INPUTSINK, hwndTarget = handle }
            };
            if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
                throw new InvalidOperationException("无法注册全局键鼠输入，错误代码：" + Marshal.GetLastWin32Error());
        }

        private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == WM_INPUT) ReadInput(lParam);
            return IntPtr.Zero;
        }

        private void ReadInput(IntPtr rawHandle)
        {
            uint size = 0;
            uint headerSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));
            if (GetRawInputData(rawHandle, RID_INPUT, IntPtr.Zero, ref size, headerSize) != 0 || size == 0)
                return;

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(rawHandle, RID_INPUT, buffer, ref size, headerSize) != size) return;
                RAWINPUTHEADER header = (RAWINPUTHEADER)Marshal.PtrToStructure(buffer, typeof(RAWINPUTHEADER));
                IntPtr payload = IntPtr.Add(buffer, (int)headerSize);

                if (header.dwType == RIM_TYPEKEYBOARD)
                {
                    RAWKEYBOARD keyboard = (RAWKEYBOARD)Marshal.PtrToStructure(payload, typeof(RAWKEYBOARD));
                    string physical = KeyNames.PhysicalId(keyboard.VKey, keyboard.MakeCode, keyboard.Flags);
                    bool released = (keyboard.Flags & 0x01) != 0;
                    if (released)
                    {
                        keysDown.Remove(physical);
                    }
                    else if (keysDown.Add(physical) && !Paused)
                    {
                        store.AddKeyboard(KeyNames.FromRaw(keyboard.VKey, keyboard.MakeCode, keyboard.Flags));
                        FireKeyboardActivity();
                    }
                }
                else if (header.dwType == RIM_TYPEMOUSE)
                {
                    RAWMOUSE mouse = (RAWMOUSE)Marshal.PtrToStructure(payload, typeof(RAWMOUSE));
                    if (!Paused) ProcessMouse(mouse);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private void ProcessMouse(RAWMOUSE mouse)
        {
            ushort f = mouse.usButtonFlags;
            if ((f & 0x0001) != 0) store.AddMouse("左键");
            if ((f & 0x0004) != 0) store.AddMouse("右键");
            if ((f & 0x0010) != 0) store.AddMouse("中键");
            if ((f & 0x0040) != 0) store.AddMouse("侧键 1");
            if ((f & 0x0100) != 0) store.AddMouse("侧键 2");
            if ((f & 0x0400) != 0)
            {
                short delta = unchecked((short)mouse.usButtonData);
                store.AddMouse(delta > 0 ? "滚轮向上" : "滚轮向下");
            }
            if ((f & 0x0800) != 0)
            {
                short delta = unchecked((short)mouse.usButtonData);
                store.AddMouse(delta > 0 ? "横向滚轮向右" : "横向滚轮向左");
            }
        }

        private void FireKeyboardActivity()
        {
            Action handler = KeyboardActivity;
            if (handler != null) handler();
        }

        public void Dispose()
        {
            if (source != null) source.RemoveHook(WndProc);
        }
    }

    public sealed class StatsWindow : Window
    {
        private readonly StatsStore store;
        private readonly DataGrid keyboardGrid;
        private readonly DataGrid mouseGrid;
        private readonly TextBlock summary;
        private DateTime selectedDate = DateTime.Today;

        public StatsWindow(StatsStore store)
        {
            this.store = store;
            Title = "噜噜 · 按键统计";
            Width = 760;
            Height = 620;
            MinWidth = 620;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(250, 247, 239));
            FontFamily = new FontFamily("Microsoft YaHei UI");

            DockPanel root = new DockPanel { Margin = new Thickness(18) };
            Content = root;

            StackPanel header = new StackPanel { Orientation = Orientation.Vertical };
            DockPanel.SetDock(header, Dock.Top);
            TextBlock title = new TextBlock
            {
                Text = "噜噜的键鼠统计",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(104, 67, 30))
            };
            header.Children.Add(title);

            StackPanel toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 8) };
            Button previous = MakeButton("‹ 前一天");
            Button today = MakeButton("今天");
            Button next = MakeButton("后一天 ›");
            previous.Click += delegate { selectedDate = selectedDate.AddDays(-1); Refresh(); };
            today.Click += delegate { selectedDate = DateTime.Today; Refresh(); };
            next.Click += delegate { if (selectedDate < DateTime.Today) selectedDate = selectedDate.AddDays(1); Refresh(); };
            toolbar.Children.Add(previous);
            toolbar.Children.Add(today);
            toolbar.Children.Add(next);
            header.Children.Add(toolbar);

            summary = new TextBlock { FontSize = 15, Margin = new Thickness(2, 2, 0, 12) };
            header.Children.Add(summary);
            root.Children.Add(header);

            TabControl tabs = new TabControl();
            keyboardGrid = CreateGrid();
            mouseGrid = CreateGrid();
            tabs.Items.Add(new TabItem { Header = "全部键盘按键", Content = keyboardGrid });
            tabs.Items.Add(new TabItem { Header = "鼠标", Content = mouseGrid });
            root.Children.Add(tabs);

            store.Changed += OnStoreChanged;
            Closed += delegate { store.Changed -= OnStoreChanged; };
            Refresh();
        }

        private Button MakeButton(string text)
        {
            return new Button
            {
                Content = text,
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(255, 220, 139))
            };
        }

        private DataGrid CreateGrid()
        {
            DataGrid grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                RowBackground = Brushes.White,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(255, 249, 232))
            };
            grid.Columns.Add(new DataGridTextColumn { Header = "按键", Binding = new Binding("Name"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "分类", Binding = new Binding("Group"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "次数", Binding = new Binding("Count") { StringFormat = "N0" }, Width = 140 });
            return grid;
        }

        private void OnStoreChanged()
        {
            Dispatcher.BeginInvoke(new Action(Refresh), DispatcherPriority.Background);
        }

        private void Refresh()
        {
            string key = selectedDate.ToString("yyyy-MM-dd");
            List<StatRow> keyboard = store.GetKeyboardRows(key);
            List<StatRow> mouse = store.GetMouseRows(key);
            keyboardGrid.ItemsSource = keyboard;
            mouseGrid.ItemsSource = mouse;
            long mouseClicks = mouse.Where(x => !x.Name.StartsWith("滚轮") && !x.Name.StartsWith("横向")).Sum(x => x.Count);
            summary.Text = selectedDate.ToString("yyyy年M月d日") +
                "    键盘 " + keyboard.Sum(x => x.Count).ToString("N0") +
                " 次    鼠标点击 " + mouseClicks.ToString("N0") + " 次";
        }
    }

    public sealed class PetWindow : Window
    {
        private readonly StatsStore store = new StatsStore();
        private readonly TextBlock badge;
        private readonly Image petImage;
        private readonly DispatcherTimer saveTimer;
        private readonly DispatcherTimer typingTimer;
        private readonly BitmapImage typingFrameA;
        private readonly BitmapImage typingFrameB;
        private GlobalInputTracker tracker;
        private StatsWindow statsWindow;
        private bool allowClose;
        private bool alternateTypingFrame;

        public PetWindow()
        {
            Title = "噜噜桌宠";
            Width = 170;
            Height = 190;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Left = SystemParameters.WorkArea.Right - Width - 20;
            Top = SystemParameters.WorkArea.Bottom - Height - 20;

            Grid root = new Grid();
            Content = root;

            petImage = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(4, 24, 4, 2),
                Cursor = Cursors.Hand
            };
            typingFrameA = LoadImage("lulu-typing.png");
            typingFrameB = LoadImage("lulu-typing-press.png");
            petImage.Source = typingFrameA;
            root.Children.Add(petImage);

            Border badgeBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(235, 92, 63, 34)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 4, 0),
                Cursor = Cursors.Hand
            };
            badge = new TextBlock
            {
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 10,
                TextAlignment = TextAlignment.Right
            };
            badgeBox.Child = badge;
            badgeBox.MouseLeftButtonUp += delegate { ShowStats(); };
            root.Children.Add(badgeBox);

            ContextMenu menu = new ContextMenu();
            MenuItem stats = new MenuItem { Header = "查看详细统计" };
            MenuItem pause = new MenuItem { Header = "暂停统计", IsCheckable = true };
            MenuItem top = new MenuItem { Header = "始终置顶", IsCheckable = true, IsChecked = true };
            MenuItem size = new MenuItem { Header = "桌宠大小" };
            MenuItem sizeSmall = new MenuItem { Header = "迷你（135 px）", IsCheckable = true };
            MenuItem sizeMedium = new MenuItem { Header = "小（170 px）", IsCheckable = true, IsChecked = true };
            MenuItem sizeLarge = new MenuItem { Header = "中（220 px）", IsCheckable = true };
            MenuItem exit = new MenuItem { Header = "退出噜噜" };
            stats.Click += delegate { ShowStats(); };
            pause.Click += delegate { if (tracker != null) tracker.Paused = pause.IsChecked; UpdateBadge(); };
            top.Click += delegate { Topmost = top.IsChecked; };
            sizeSmall.Click += delegate { SetPetSize(135, 152, sizeSmall, sizeMedium, sizeLarge); };
            sizeMedium.Click += delegate { SetPetSize(170, 190, sizeMedium, sizeSmall, sizeLarge); };
            sizeLarge.Click += delegate { SetPetSize(220, 240, sizeLarge, sizeSmall, sizeMedium); };
            exit.Click += delegate { allowClose = true; Close(); };
            size.Items.Add(sizeSmall);
            size.Items.Add(sizeMedium);
            size.Items.Add(sizeLarge);
            menu.Items.Add(stats);
            menu.Items.Add(pause);
            menu.Items.Add(top);
            menu.Items.Add(size);
            menu.Items.Add(new Separator());
            menu.Items.Add(exit);
            ContextMenu = menu;

            petImage.MouseLeftButtonDown += OnDrag;
            MouseRightButtonUp += delegate { ContextMenu.IsOpen = true; };

            SourceInitialized += OnSourceInitialized;
            Closing += OnClosing;
            store.Changed += delegate { Dispatcher.BeginInvoke(new Action(UpdateBadge), DispatcherPriority.Background); };

            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            saveTimer.Tick += delegate { TrySave(); };
            saveTimer.Start();

            typingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
            typingTimer.Tick += delegate
            {
                petImage.Source = typingFrameA;
                alternateTypingFrame = false;
                typingTimer.Stop();
            };

            DispatcherTimer dateTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            dateTimer.Tick += delegate { UpdateBadge(); };
            dateTimer.Start();
            UpdateBadge();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            try
            {
                tracker = new GlobalInputTracker(this, store);
                tracker.KeyboardActivity += OnKeyboardActivity;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "噜噜无法启动统计", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnKeyboardActivity()
        {
            if (typingFrameB != null)
            {
                alternateTypingFrame = !alternateTypingFrame;
                petImage.Source = alternateTypingFrame ? typingFrameB : typingFrameA;
            }
            typingTimer.Stop();
            typingTimer.Start();
        }

        private BitmapImage LoadImage(string fileName)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", fileName);
            if (!File.Exists(path)) return null;
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private void SetPetSize(double width, double height, MenuItem selected, MenuItem otherA, MenuItem otherB)
        {
            selected.IsChecked = true;
            otherA.IsChecked = false;
            otherB.IsChecked = false;
            Width = width;
            Height = height;
            Left = Math.Min(Left, SystemParameters.WorkArea.Right - Width);
            Top = Math.Min(Top, SystemParameters.WorkArea.Bottom - Height);
        }

        private void OnDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void ShowStats()
        {
            if (statsWindow == null || !statsWindow.IsLoaded)
            {
                statsWindow = new StatsWindow(store);
                statsWindow.Show();
            }
            else
            {
                statsWindow.Activate();
            }
        }

        private void UpdateBadge()
        {
            string prefix = tracker != null && tracker.Paused ? "已暂停\n" : "";
            badge.Text = prefix +
                "⌨ " + store.TodayKeyboardTotal().ToString("N0") +
                "\n🖱 " + store.TodayMouseClickTotal().ToString("N0");
        }

        private void TrySave()
        {
            try { store.Save(); } catch { }
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!allowClose)
            {
                e.Cancel = true;
                return;
            }
            if (tracker != null) tracker.Dispose();
            TrySave();
            Application.Current.Shutdown();
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "LuluDesktopPet.SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("噜噜已经在运行啦。", "噜噜桌宠");
                    return;
                }

                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.Run(new PetWindow());
            }
        }
    }
}
