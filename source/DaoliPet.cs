using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

namespace DaoliPet
{
    public sealed class ChromaKey : ShaderEffect
    {
        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(ChromaKey), 0);

        public ChromaKey()
        {
            PixelShader = new PixelShader { UriSource = new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chroma.ps")) };
            UpdateShaderValue(InputProperty);
        }
    }

    public sealed class GreenScreen : ShaderEffect
    {
        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(GreenScreen), 0);
        public static readonly DependencyProperty TimeProperty = DependencyProperty.Register("Time", typeof(double), typeof(GreenScreen), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0)));
        public static readonly DependencyProperty AnimateStillProperty = DependencyProperty.Register("AnimateStill", typeof(double), typeof(GreenScreen), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(1)));
        public static readonly DependencyProperty SwayProperty = DependencyProperty.Register("Sway", typeof(double), typeof(GreenScreen), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(2)));
        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register("Stretch", typeof(double), typeof(GreenScreen), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(3)));
        public static readonly DependencyProperty TwistProperty = DependencyProperty.Register("Twist", typeof(double), typeof(GreenScreen), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(4)));
        public static readonly DependencyProperty DriftProperty = DependencyProperty.Register("Drift", typeof(double), typeof(GreenScreen), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(5)));
        public Brush Input { get { return (Brush)GetValue(InputProperty); } set { SetValue(InputProperty, value); } }
        public double Time { get { return (double)GetValue(TimeProperty); } set { SetValue(TimeProperty, value); } }
        public double AnimateStill { get { return (double)GetValue(AnimateStillProperty); } set { SetValue(AnimateStillProperty, value); } }
        public double Sway { get { return (double)GetValue(SwayProperty); } set { SetValue(SwayProperty, value); } }
        public double Stretch { get { return (double)GetValue(StretchProperty); } set { SetValue(StretchProperty, value); } }
        public double Twist { get { return (double)GetValue(TwistProperty); } set { SetValue(TwistProperty, value); } }
        public double Drift { get { return (double)GetValue(DriftProperty); } set { SetValue(DriftProperty, value); } }
        public static readonly DependencyProperty WriggleProperty = DependencyProperty.Register("Wriggle", typeof(double), typeof(GreenScreen), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(6)));
        public double Wriggle { get { return (double)GetValue(WriggleProperty); } set { SetValue(WriggleProperty, value); } }
        public static readonly DependencyProperty SideProperty = DependencyProperty.Register("Side", typeof(double), typeof(GreenScreen), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(7)));
        public double Side { get { return (double)GetValue(SideProperty); } set { SetValue(SideProperty, value); } }
        public GreenScreen()
        {
            PixelShader = new PixelShader { UriSource = new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deform.ps")) };
            UpdateShaderValue(InputProperty); UpdateShaderValue(TimeProperty); UpdateShaderValue(AnimateStillProperty);
            UpdateShaderValue(SwayProperty); UpdateShaderValue(StretchProperty); UpdateShaderValue(TwistProperty);
            UpdateShaderValue(DriftProperty);
            UpdateShaderValue(WriggleProperty); UpdateShaderValue(SideProperty);
        }
    }

    public sealed class PetWindow : Window
    {
        readonly string root = AppDomain.CurrentDomain.BaseDirectory;
        readonly Grid canvas = new Grid();
        readonly Grid viewport = new Grid();
        readonly Grid sourceLayer = new Grid();
        readonly ChromaKey chroma = new ChromaKey();
        readonly Image portrait = new Image { Stretch = Stretch.Uniform };
        readonly MediaElement video = new MediaElement { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual, Stretch = Stretch.Uniform, ScrubbingEnabled = true };
        readonly GreenScreen key = new GreenScreen();
        readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        readonly Stopwatch clock = new Stopwatch();
        readonly Stopwatch physicsClock = Stopwatch.StartNew();
        readonly DispatcherTimer physicsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        readonly ThrowBody body = new ThrowBody();
        readonly PhysicalPose pose = new PhysicalPose();
        readonly ScaleTransform physicalScale = new ScaleTransform(1, 1);
        readonly TranslateTransform physicalOffset = new TranslateTransform();
        readonly System.Collections.Generic.List<DragSample> dragTrail = new System.Collections.Generic.List<DragSample>();
        readonly MenuItem physicsItem = new MenuItem { Header = "拖拽投掷 / 重力反弹", IsCheckable = true };
        bool physicsEnabled = true, dragging = false, dragMoved = false;
        Point dragOffset, dragStart;
        double lastPhysicsTime;
        Rect flightBounds;
        readonly string settingsPath;
        bool playing = true, muted = false, isVideo = false;
        double volume = 0.35, aspect = 862.0 / 870.0;
        double speed = 1.0, motionTime = 0, lastClock = 0;
        readonly int motionSeed = new Random().Next(1, 1000000);
        readonly MenuItem speedMenu = new MenuItem();
        string sourcePath = "";
        int loopCount = 0;
        readonly MenuItem pauseItem = new MenuItem();
        readonly MenuItem muteItem = new MenuItem();
        readonly MenuItem statusItem = new MenuItem { IsEnabled = false };
        readonly MenuItem keyItem = new MenuItem { Header = "去掉绿色背景", IsCheckable = true, IsChecked = true };

        const int MaxMinis = 10;
        const double MergeDuration = 0.32;
        readonly PetWindow parentPet;
        readonly System.Collections.Generic.List<PetWindow> minis = new System.Collections.Generic.List<PetWindow>();
        readonly MenuItem splitItem = new MenuItem { Header = "分裂出小道理" };
        readonly MenuItem recallItem = new MenuItem { Header = "全部收回" };
        double birth, bulge, swallow, overlap, mergeTime;
        bool mergeArmed, merging;
        Point mergeStart;

        public PetWindow(string[] args, PetWindow parent = null)
        {
            parentPet = parent;
            Title = parent == null ? "说的道理桌宠" : "小道理";
            Icon = BitmapFrame.Create(new Uri(Path.Combine(root, "daoli.ico")));
            Width = 320; Height = 320 / aspect;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
            ShowInTaskbar = parent == null;
            ShowActivated = parent == null; WindowStartupLocation = WindowStartupLocation.Manual;
            AllowDrop = true; Content = viewport;
            viewport.Children.Add(canvas); canvas.Children.Add(sourceLayer);
            sourceLayer.Children.Add(portrait); sourceLayer.Children.Add(video);
            sourceLayer.Effect = chroma;
            canvas.Effect = key;
            TransformGroup physicalTransforms = new TransformGroup();
            physicalTransforms.Children.Add(physicalScale); physicalTransforms.Children.Add(physicalOffset);
            canvas.RenderTransformOrigin = new Point(0.5, 0.5); canvas.RenderTransform = physicalTransforms;
            video.Visibility = Visibility.Collapsed;
            settingsPath = Path.Combine(root, "settings.xml");
            if (parent == null) LoadSettings();
            else {
                Width = Math.Max(36, Math.Round(parent.Width / 3));
                Height = Width / aspect;
                speed = parent.speed;
                birth = 0.35;
            }
            BuildMenu();
            MouseLeftButtonDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseUp;
            LostMouseCapture += delegate { if (dragging) { dragging = false; dragTrail.Clear(); pose.Release(); } };
            MouseWheel += delegate(object sender, MouseWheelEventArgs e) { SetSize(Width + (e.Delta > 0 ? 24 : -24)); };
            KeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.Key == Key.Escape) Close();
                else if (e.Key == Key.Space) TogglePlay();
                else if (e.Key == Key.M) ToggleMute();
            };
            DragOver += delegate(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            Drop += delegate(object sender, DragEventArgs e) {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0) OpenMedia(files[0]);
            };
            video.MediaOpened += delegate {
                if (video.NaturalVideoWidth > 0 && video.NaturalVideoHeight > 0) {
                    aspect = (double)video.NaturalVideoWidth / video.NaturalVideoHeight;
                    Height = Width / aspect; ClampToScreen();
                }
                if (!playing) video.Pause();
                statusItem.Header = "原视频 · " + Path.GetFileName(sourcePath);
                File.WriteAllText(Path.Combine(root,"media-status.txt"), "OK: " + sourcePath);
            };
            video.MediaEnded += delegate { loopCount++; video.Position = TimeSpan.Zero; if (playing) video.Play(); };
            video.MediaFailed += delegate(object sender, ExceptionRoutedEventArgs e) {
                string failedPath = sourcePath;
                ShowPortrait();
                File.WriteAllText(Path.Combine(root,"media-status.txt"), "ERROR: " + failedPath + "\n" + e.ErrorException);
                MessageBox.Show(this, "这个视频暂时无法播放，请换成 H.264 编码的 MP4。\n\n" + e.ErrorException.Message, "说的道理桌宠");
            };
            timer.Tick += delegate { if (playing && !isVideo) AdvanceMotion(); };
            physicsTimer.Tick += delegate {
                double now = physicsClock.Elapsed.TotalSeconds, dt = now - lastPhysicsTime; lastPhysicsTime = now;
                if (UpdateFamilyMotion(dt)) return;
                if (body.Active && playing && !dragging && physicsEnabled) {
                    body.Step(dt, flightBounds); Left = body.X; Top = body.Y;
                    pose.Impact(body.FloorImpactSpeed);
                    if (!body.Active) SaveSettings();
                }
                if (playing || dragging || birth > 0 || bulge > 0 || swallow > 0) {
                    bool airborne = body.Active && (body.Y < flightBounds.Bottom - 0.5 || Math.Abs(body.VY) > 1);
                    pose.Step(dt, airborne, body.VY); ApplyPhysicalPose();
                }
            };
            lastPhysicsTime = physicsClock.Elapsed.TotalSeconds; physicsTimer.Start();
            Loaded += delegate {
                if (parentPet != null) { ShowPortrait(); return; }
                string selected = args.FirstOrDefault(p => File.Exists(p) && IsMedia(p));
                if (selected == null && File.Exists(sourcePath) && IsMedia(sourcePath)) selected = sourcePath;
                if (selected == null) selected = Directory.GetFiles(root).FirstOrDefault(IsMedia);
                if (selected != null) OpenMedia(selected); else ShowPortrait();
                ClampToScreen();
                if (args.Contains("--selftest")) {
                    DispatcherTimer verify = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    verify.Tick += delegate {
                        verify.Stop();
                        try {
                            bool initial = playing; TogglePlay(); bool paused = !playing; TogglePlay();
                            ToggleMute(); bool silence = muted; ToggleMute();
                            if (args.Contains("--check-family")) {
                                PetWindow first = Split();
                                if (first == null || !first.IsVisible || first.ShowInTaskbar ||
                                    first.Width != Math.Max(36, Math.Round(Width / 3)) || !first.body.Active)
                                    throw new Exception("Mini spawn failed.");
                                for (int i = 1; i < MaxMinis; i++) Split();
                                if (minis.Count != MaxMinis || Split() != null)
                                    throw new Exception("Mini limit failed.");
                                first.body.Stop();
                                first.Left = Left + (Width - first.Width) / 2;
                                first.Top = Top + Height * 0.58 - first.Height / 2;
                                first.mergeArmed = true;
                                for (int i = 0; i < 20 && minis.Contains(first); i++) first.UpdateFamilyMotion(0.05);
                                if (minis.Count != MaxMinis - 1) throw new Exception("Contact merge failed.");
                                recallItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                                foreach (PetWindow mini in minis.ToArray())
                                    for (int i = 0; i < 10 && minis.Contains(mini); i++) mini.UpdateFamilyMotion(0.05);
                                if (minis.Count != 0 || !splitItem.IsEnabled || recallItem.IsEnabled)
                                    throw new Exception("Recall failed.");
                            }
                            double oldWidth = Width; SetSize(400); bool resized = Width == 400; SetSize(oldWidth);
                            SetSize(80); bool minimumSize = Width == 120; SetSize(oldWidth);
                            bool hasBodyRest = false, mouthMovesDuringRest = false, combinedMotion = false;
                            bool extensionOnly = true, driftsLeft = false, driftsRight = false;
                            System.Collections.Generic.HashSet<int> combinations = new System.Collections.Generic.HashSet<int>();
                            double previousMouth = 0;
                            for (int sample = 0; sample < 8000; sample++) {
                                UpdateMotion(sample / 100.0);
                                if (key.Sway < 0 || key.Stretch < 0) extensionOnly = false;
                                if (key.Drift < -0.5) driftsLeft = true;
                                if (key.Drift > 0.5) driftsRight = true;
                                int combination = (key.Sway > 0.05 ? 1 : 0) | (key.Stretch > 0.05 ? 2 : 0) | (Math.Abs(key.Twist) > 0.05 ? 4 : 0) | (Math.Abs(key.Drift) > 0.05 ? 8 : 0);
                                combinations.Add(combination);
                                if (key.Sway > 0.05 && key.Stretch > 0.05) combinedMotion = true;
                                if (key.Sway == 0 && key.Stretch == 0 && key.Twist == 0) {
                                    hasBodyRest = true;
                                    if (sample > 0 && Math.Abs(key.Time - previousMouth) > 0.001) mouthMovesDuringRest = true;
                                }
                                previousMouth = key.Time;
                            }
                            double oldSpeed = speed; SetSpeed(0.5); bool slowSpeed = speed == 0.5 && video.SpeedRatio == 0.5;
                            SetSpeed(2.0); bool fastSpeed = speed == 2.0 && video.SpeedRatio == 2.0; SetSpeed(oldSpeed);
                            UpdateMotion(motionTime);
                            if (!extensionOnly || !driftsLeft || !driftsRight) throw new Exception("Extension/sway motion validation failed.");
                            if (!combinedMotion || combinations.Count < 5 || !slowSpeed || !fastSpeed) throw new Exception("Random combinations/speed validation failed.");
                            RenderTargetBitmap bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
                            bitmap.Render(viewport);
                            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            using (FileStream file = File.Create(Path.Combine(root,"selftest.png"))) encoder.Save(file);
                            File.WriteAllText(Path.Combine(root,"selftest.txt"), "visible=" + IsVisible + "\nsize=" + ActualWidth + "x" + ActualHeight + "\npaused=" + paused + "\nresumed=" + (playing == initial) + "\nmute=" + silence + "\nresized=" + resized + "\nminimum120=" + minimumSize + "\nrandomCombinations=" + combinations.Count + "\ncombinedMotion=" + combinedMotion + "\nslowSpeed=" + slowSpeed + "\nfastSpeed=" + fastSpeed + "\nbodyRest=" + hasBodyRest + "\nmouthMovesDuringRest=" + mouthMovesDuringRest + "\nvideo=" + isVideo + "\nloops=" + loopCount + "\nshaderTier=" + (RenderCapability.Tier >> 16));
                        } catch (Exception ex) { File.WriteAllText(Path.Combine(root,"selftest.txt"), ex.ToString()); }
                        Close();
                    };
                    verify.Start();
                }
            };
            Closed += delegate {
                timer.Stop(); physicsTimer.Stop(); clock.Stop(); video.Close(); SaveSettings();
                if (parentPet != null) {
                    parentPet.minis.Remove(this);
                    parentPet.UpdateFamilyMenu();
                }
            };
            if (parentPet == null) Closing += delegate {
                foreach (PetWindow mini in minis.ToArray()) mini.Close();
            };
        }

        static bool IsMedia(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return new[] { ".mp4", ".m4v", ".wmv", ".mov", ".avi" }.Contains(ext);
        }

        void UpdateFamilyMenu()
        {
            splitItem.IsEnabled = minis.Count < MaxMinis;
            recallItem.IsEnabled = minis.Any(mini => !mini.merging);
        }

        PetWindow Split()
        {
            if (parentPet != null || minis.Count >= MaxMinis) return null;
            PetWindow mini = new PetWindow(new string[0], this);
            mini.Owner = this;
            double direction = minis.Count % 2 == 0 ? 1 : -1;
            Rect room = GetFlightBounds();
            double centerX = Left + Width / 2;
            if (direction > 0 && room.Right - Left < Width * 0.3) direction = -1;
            else if (direction < 0 && Left - room.Left < Width * 0.3) direction = 1;
            mini.Left = centerX + direction * Width * 0.16 - mini.Width / 2;
            mini.Top = Top + Height * 0.63 - mini.Height / 2;
            minis.Add(mini);
            bulge = 0.38;
            mini.Show();
            mini.flightBounds = mini.GetFlightBounds();
            mini.Left = Math.Max(mini.flightBounds.Left, Math.Min(mini.flightBounds.Right, mini.Left));
            mini.Top = Math.Max(mini.flightBounds.Top, Math.Min(mini.flightBounds.Bottom, mini.Top));
            Vector launch = new Vector(direction * (225 + 45 * minis.Count), -220 - 20 * minis.Count);
            mini.body.Launch(mini.Left, mini.Top, launch);
            mini.lastPhysicsTime = mini.physicsClock.Elapsed.TotalSeconds;
            UpdateFamilyMenu();
            return mini;
        }

        void BeginMerge()
        {
            if (parentPet == null || merging) return;
            merging = true; mergeTime = 0;
            body.Stop(); pose.Release(); dragging = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            mergeStart = new Point(Left, Top);
            IsHitTestVisible = false;
            parentPet.swallow = 0.5;
            parentPet.UpdateFamilyMenu();
        }

        bool UpdateFamilyMotion(double elapsed)
        {
            double dt = Math.Max(0, Math.Min(0.05, elapsed));
            birth = Math.Max(0, birth - dt);
            bulge = Math.Max(0, bulge - dt);
            swallow = Math.Max(0, swallow - dt);
            if (parentPet == null) return false;

            if (merging) {
                mergeTime += dt;
                double u = Math.Min(1, mergeTime / MergeDuration);
                double eased = u * u * (3 - 2 * u);
                Left = mergeStart.X + (parentPet.Left + (parentPet.Width - Width) / 2 - mergeStart.X) * eased;
                Top = mergeStart.Y + (parentPet.Top + parentPet.Height * 0.58 - Height / 2 - mergeStart.Y) * eased;
                physicalScale.ScaleX = physicalScale.ScaleY = 1 - 0.94 * eased;
                Opacity = 1 - eased * eased;
                if (u >= 1) {
                    parentPet.pose.Impact(1050);
                    Close();
                }
                return true;
            }

            double dx = Left + Width / 2 - (parentPet.Left + parentPet.Width / 2);
            double dy = Top + Height / 2 - (parentPet.Top + parentPet.Height * 0.58);
            bool close = Math.Abs(dx) <= parentPet.Width * 0.29 && Math.Abs(dy) <= parentPet.Height * 0.32;
            if (mergeArmed && !body.Active && close) {
                overlap += dt;
                if (overlap >= 0.45) BeginMerge();
            } else overlap = 0;
            return merging;
        }

        void BuildMenu()
        {
            ContextMenu menu = new ContextMenu();
            if (parentPet == null) {
                splitItem.Click += delegate { Split(); };
                recallItem.Click += delegate { foreach (PetWindow mini in minis.ToArray()) mini.BeginMerge(); };
                menu.Items.Add(splitItem); menu.Items.Add(recallItem);
                menu.Items.Add(new Separator());
                UpdateFamilyMenu();
            } else {
                MenuItem mergeItem = new MenuItem { Header = "回到本道理" };
                mergeItem.Click += delegate { BeginMerge(); };
                menu.Items.Add(mergeItem);
            }
            statusItem.Header = "图片预览 · 尚未载入原视频";
            menu.Items.Add(statusItem); menu.Items.Add(new Separator());
            pauseItem.Click += delegate { TogglePlay(); }; menu.Items.Add(pauseItem);
            muteItem.Click += delegate { ToggleMute(); }; menu.Items.Add(muteItem);
            MenuItem volumes = new MenuItem { Header = "音量" };
            foreach (int percentage in new[] { 10, 25, 35, 50, 75, 100 }) {
                int n = percentage;
                MenuItem item = new MenuItem { Header = n + "%" };
                item.Click += delegate { volume = n / 100.0; muted = false; ApplyAudio(); UpdateMenu(); };
                volumes.Items.Add(item);
            }
            menu.Items.Add(volumes);
            foreach (double rate in new[] { 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0 }) {
                double selectedRate = rate;
                MenuItem item = new MenuItem { Header = rate.ToString("0.##", CultureInfo.InvariantCulture) + "×", Tag = rate, IsCheckable = true };
                item.Click += delegate { SetSpeed(selectedRate); }; speedMenu.Items.Add(item);
            }
            menu.Items.Add(speedMenu); UpdateSpeedMenu();
            physicsItem.IsChecked = physicsEnabled;
            physicsItem.Click += delegate { physicsEnabled = physicsItem.IsChecked; body.Stop(); SaveSettings(); };
            menu.Items.Add(physicsItem);
            MenuItem size = new MenuItem { Header = "大小（也可滚动鼠标滚轮）" };
            foreach (int width in new[] { 120, 160, 200, 280, 320, 400, 500 }) {
                int n = width; MenuItem item = new MenuItem { Header = n + " px" };
                item.Click += delegate { SetSize(n); }; size.Items.Add(item);
            }
            menu.Items.Add(size);
            keyItem.Click += delegate { sourceLayer.Effect = keyItem.IsChecked ? chroma : null; };
            menu.Items.Add(keyItem);
            menu.Items.Add(new Separator());
            MenuItem open = new MenuItem { Header = "载入原版绿幕视频…" };
            open.Click += delegate {
                Microsoft.Win32.OpenFileDialog picker = new Microsoft.Win32.OpenFileDialog {
                    Title = "选择说的道理绿幕视频", Filter = "视频文件|*.mp4;*.m4v;*.wmv;*.mov;*.avi|所有文件|*.*"
                };
                if (picker.ShowDialog(this) == true) OpenMedia(picker.FileName);
            };
            menu.Items.Add(open);
            MenuItem home = new MenuItem { Header = "移到屏幕右下角" };
            home.Click += delegate { PlaceDefault(); }; menu.Items.Add(home);
            MenuItem exit = new MenuItem { Header = "退出" }; exit.Click += delegate { Close(); }; menu.Items.Add(exit);
            ContextMenu = menu; UpdateMenu();
        }

        void ShowPortrait()
        {
            isVideo = false; video.Stop(); video.Source = null; video.Visibility = Visibility.Collapsed;
            portrait.Source = new BitmapImage(new Uri(Path.Combine(root, "original.jpg")));
            portrait.Visibility = Visibility.Visible;
            aspect = 862.0 / 870.0; Height = Width / aspect;
            key.AnimateStill = 1.0;
            statusItem.Header = "图片预览（无声）· 拖入 MP4 可播放原版";
            if (playing) { clock.Start(); timer.Start(); }
            UpdateMenu();
        }

        void OpenMedia(string path)
        {
            if (!File.Exists(path) || !IsMedia(path)) {
                MessageBox.Show(this, "请选择 MP4、M4V、WMV、MOV 或 AVI 视频。", "说的道理桌宠"); return;
            }
            sourcePath = Path.GetFullPath(path);
            isVideo = true; playing = true; timer.Stop(); clock.Stop(); key.AnimateStill = 0;
            portrait.Visibility = Visibility.Collapsed; video.Visibility = Visibility.Visible;
            video.Stop(); video.Source = new Uri(sourcePath); ApplyAudio(); video.SpeedRatio = speed;
            statusItem.Header = "正在读取原视频…"; video.Play(); UpdateMenu();
        }

        void TogglePlay()
        {
            playing = !playing;
            if (isVideo) { if (playing) video.Play(); else video.Pause(); }
            else { if (playing) { clock.Start(); timer.Start(); } else { clock.Stop(); timer.Stop(); } }
            UpdateMenu();
        }
        void AdvanceMotion()
        {
            double now = clock.Elapsed.TotalSeconds;
            motionTime += Math.Max(0, now - lastClock) * speed;
            lastClock = now;
            UpdateMotion(motionTime);
        }
        void SetSpeed(double value)
        {
            // Accumulate at the previous speed first: changing speed never jumps the pose.
            if (playing && !isVideo) AdvanceMotion();
            speed = Math.Max(0.25, Math.Min(3.0, value));
            video.SpeedRatio = speed; UpdateSpeedMenu(); SaveSettings();
        }
        void UpdateSpeedMenu()
        {
            speedMenu.Header = "动作 / 播放速度（" + speed.ToString("0.##", CultureInfo.InvariantCulture) + "×）";
            foreach (MenuItem item in speedMenu.Items) item.IsChecked = Math.Abs((double)item.Tag - speed) < 0.001;
        }
        double Noise(int channel, int cycle, int salt)
        {
            unchecked {
                uint x = (uint)(motionSeed + channel * 73856093 + cycle * 19349663 + salt * 83492791);
                x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16;
                return (x & 0xffffff) / 16777215.0;
            }
        }
        double MotionEnvelope(double t, int channel, double period)
        {
            int cycle = (int)Math.Floor(t / period);
            double local = t - cycle * period;
            double start = period * (0.04 + 0.16 * Noise(channel, cycle, 1));
            double duration = period * (0.40 + 0.36 * Noise(channel, cycle, 2));
            double u = (local - start) / duration;
            if (u <= 0 || u >= 1) return 0;
            return Math.Pow(Math.Sin(Math.PI * u), 2) * (0.65 + 0.35 * Noise(channel, cycle, 3));
        }
        void UpdateMotion(double t)
        {
            // Independent, smoothly gated tracks overlap differently each run.
            // Mouth keeps moving softly even when the other tracks rest.
            key.Time = (0.4 + 0.6 * MotionEnvelope(t, 1, 6.7)) * (0.7 * Math.Sin(t * 3.2) + 0.3 * Math.Sin(t * 5.0));
            key.Sway = MotionEnvelope(t, 2, 8.9);
            key.Stretch = MotionEnvelope(t, 3, 10.3);
            key.Twist = MotionEnvelope(t, 4, 6.1) * Math.Sin(t * 3.8 + 0.8);
            key.Drift = MotionEnvelope(t, 5, 7.7) * Math.Sin(t * 2.1);
            key.Wriggle = MotionEnvelope(t, 6, 7.1) * Math.Sin(t * 2.4);
            key.Side = MotionEnvelope(t, 7, 9.7) * Math.Sin(t * 0.85);
            // Let the physical pose read clearly during grabs, flights and landing wobble.
            if (dragging || body.Active || pose.Hang > 0.01 || Math.Abs(pose.Jelly) > 0.02) {
                key.Sway = 0; key.Stretch = 0; key.Twist = 0; key.Drift = 0; key.Wriggle = 0; key.Side = 0;
            }
        }
        void ApplyPhysicalPose()
        {
            double grow = birth / 0.35;
            double pulse = 0.10 * Math.Sin(Math.PI * bulge / 0.38);
            physicalScale.ScaleX = pose.ScaleX * (1 - 0.78 * grow + pulse);
            physicalScale.ScaleY = pose.ScaleY * (1 - 0.54 * grow + pulse);
            if (swallow > 0 && !isVideo) key.Time = Math.Sin(Math.PI * swallow / 0.5);
        }
        void ToggleMute() { muted = !muted; ApplyAudio(); UpdateMenu(); }
        void ApplyAudio() { video.Volume = volume; video.IsMuted = muted; }
        void UpdateMenu() { pauseItem.Header = playing ? "暂停（双击 / 空格）" : "继续（双击 / 空格）"; muteItem.Header = muted ? "打开声音（M）" : "静音（M）"; }
        void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { TogglePlay(); e.Handled = true; return; }
            body.Stop();
            dragOffset = e.GetPosition(this);
            pose.Grab(dragOffset.Y / Math.Max(1, Height));
            dragStart = new Point(Left + dragOffset.X, Top + dragOffset.Y);
            dragTrail.Clear(); dragTrail.Add(new DragSample(physicsClock.Elapsed.TotalSeconds, dragStart));
            dragMoved = false; dragging = CaptureMouse(); e.Handled = true;
        }
        void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (merging || !dragging || e.LeftButton != MouseButtonState.Pressed) return;
            Point local = e.GetPosition(this);
            Point cursor = new Point(Left + local.X, Top + local.Y);
            if (!dragMoved && (cursor - dragStart).Length < 3) return;
            dragMoved = true;
            if (parentPet != null) mergeArmed = true;
            Left = cursor.X - dragOffset.X; Top = cursor.Y - dragOffset.Y;
            double now = physicsClock.Elapsed.TotalSeconds;
            if (dragTrail.Count == 0 || (cursor - dragTrail[dragTrail.Count - 1].Position).Length > 0.2) {
                dragTrail.Add(new DragSample(now, cursor));
                while (dragTrail.Count > 2 && dragTrail[0].Time < now - 0.2) dragTrail.RemoveAt(0);
            }
            e.Handled = true;
        }
        void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!dragging) return;
            Vector velocity = physicsEnabled && dragMoved ? ThrowBody.ReleaseVelocity(dragTrail, physicsClock.Elapsed.TotalSeconds) : new Vector();
            dragging = false; pose.Release(); ReleaseMouseCapture(); dragTrail.Clear();
            ClampToScreen();
            if (velocity.Length > 0) {
                mergeArmed = false; overlap = 0;
                flightBounds = GetFlightBounds();
                Left = Math.Max(flightBounds.Left, Math.Min(flightBounds.Right, Left));
                Top = Math.Max(flightBounds.Top, Math.Min(flightBounds.Bottom, Top));
                body.Launch(Left, Top, velocity);
                if (!playing) TogglePlay();
                lastPhysicsTime = physicsClock.Elapsed.TotalSeconds;
            }
            SaveSettings(); e.Handled = true;
        }
        Rect GetFlightBounds()
        {
            // Bounce within the display where the pet was released, above its taskbar.
            var area = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).WorkingArea;
            Matrix fromPixels = PresentationSource.FromVisual(this).CompositionTarget.TransformFromDevice;
            Point first = fromPixels.Transform(new Point(area.Left, area.Top));
            Point last = fromPixels.Transform(new Point(area.Right, area.Bottom));
            return new Rect(first.X, first.Y, Math.Max(0, last.X - first.X - Width), Math.Max(0, last.Y - first.Y - Height));
        }
        void SetSize(double w) { body.Stop(); Width = Math.Max(parentPet == null ? 120 : 36, Math.Min(700, w)); Height = Width / aspect; ClampToScreen(); }
        void PlaceDefault() { body.Stop(); Rect area = SystemParameters.WorkArea; Left = area.Right - Width - 36; Top = area.Bottom - Height - 12; }
        void ClampToScreen()
        {
            Rect area = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            if (double.IsNaN(Left) || double.IsNaN(Top)) { PlaceDefault(); return; }
            double visibleWidth = Math.Min(80, Width);
            Left = Math.Max(area.Left - Width + visibleWidth, Math.Min(area.Right - visibleWidth, Left));
            Top = Math.Max(area.Top, Math.Min(area.Bottom - Math.Min(80, Height), Top));
        }
        void LoadSettings()
        {
            PlaceDefault();
            try {
                if (!File.Exists(settingsPath)) return;
                XElement s = XElement.Load(settingsPath);
                Width = Math.Max(120, Math.Min(700, (double?)s.Element("width") ?? 320)); Height = Width / aspect;
                Left = (double?)s.Element("left") ?? Left; Top = (double?)s.Element("top") ?? Top;
                volume = Math.Max(0, Math.Min(1, (double?)s.Element("volume") ?? 0.35));
                muted = (bool?)s.Element("muted") ?? false;
                physicsEnabled = (bool?)s.Element("physics") ?? true;
                double storedSpeed = (double?)s.Element("speed") ?? 1.0;
                speed = double.IsNaN(storedSpeed) || double.IsInfinity(storedSpeed) ? 1.0 : Math.Max(0.25, Math.Min(3.0, storedSpeed));
                sourcePath = (string)s.Element("video") ?? "";
            } catch { PlaceDefault(); }
        }
        void SaveSettings()
        {
            if (parentPet != null) return;
            try { new XElement("settings", new XElement("width", Width), new XElement("left", Left), new XElement("top", Top), new XElement("volume", volume), new XElement("muted", muted), new XElement("speed", speed), new XElement("physics", physicsEnabled), new XElement("video", sourcePath)).Save(settingsPath); } catch { }
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\DaoliPet_863f07f0", out created)) {
                if (!created) return;
                try {
                    Application app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                    app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e) {
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"), e.Exception.ToString());
                        MessageBox.Show("桌宠运行出错，详情已写入 error.log。\n" + e.Exception.Message, "说的道理桌宠");
                        e.Handled = true; app.Shutdown(1);
                    };
                    app.Run(new PetWindow(args));
                } catch (Exception e) {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"), e.ToString());
                    MessageBox.Show(e.Message, "说的道理桌宠");
                }
            }
        }
    }
}
