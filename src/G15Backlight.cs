using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

[assembly: AssemblyTitle("G15 Backlight")]
[assembly: AssemblyDescription("Lightweight four-zone keyboard backlight control for the tested Dell G15 5515 controller")]
[assembly: AssemblyCompany("Community project")]
[assembly: AssemblyProduct("G15 Backlight")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

internal sealed class BacklightSettings
{
    public int Brightness = 255;
    public int DimBrightness = 128;
    public Color[] Zones = { Color.White, Color.White, Color.White, Color.White };

    public BacklightSettings Clone()
    {
        BacklightSettings copy = new BacklightSettings();
        copy.Brightness = Brightness;
        copy.DimBrightness = DimBrightness;
        copy.Zones = (Color[])Zones.Clone();
        return copy;
    }

    public static BacklightSettings Load(string path)
    {
        BacklightSettings value = new BacklightSettings();
        if (!File.Exists(path)) return value;
        try
        {
            foreach (string raw in File.ReadAllLines(path))
            {
                string[] parts = raw.Split(new[] { '=' }, 2);
                if (parts.Length != 2) continue;
                if (parts[0] == "brightness")
                {
                    int parsed;
                    if (Int32.TryParse(parts[1], out parsed))
                        value.Brightness = Math.Max(0, Math.Min(255, parsed));
                }
                else if (parts[0] == "dimBrightness")
                {
                    int parsed;
                    if (Int32.TryParse(parts[1], out parsed))
                        value.DimBrightness = Math.Max(1, Math.Min(254, parsed));
                }
                else if (parts[0].StartsWith("zone", StringComparison.Ordinal))
                {
                    int index;
                    if (Int32.TryParse(parts[0].Substring(4), out index) && index >= 0 && index < 4)
                    {
                        int rgb;
                        if (Int32.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
                            value.Zones[index] = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
                    }
                }
            }
        }
        catch { }
        return value;
    }

    public void Save(string path)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("brightness=" + Brightness.ToString(CultureInfo.InvariantCulture));
        text.AppendLine("dimBrightness=" + DimBrightness.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < Zones.Length; i++)
            text.AppendLine("zone" + i + "=" + ColorHex(Zones[i]));
        File.WriteAllText(path, text.ToString(), Encoding.ASCII);
    }

    public static string ColorHex(Color color)
    {
        return color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
    }
}

internal sealed class AlienFxHidDevice : IDisposable
{
    private const uint DigcfPresent = 0x02;
    private const uint DigcfDeviceInterface = 0x10;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x01;
    private const uint FileShareWrite = 0x02;
    private const uint OpenExisting = 3;
    private const uint FileFlagSequentialScan = 0x08000000;
    private const int ReportLength = 34;
    private SafeFileHandle handle;

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInterfaceData
    {
        public int Size;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidAttributes
    {
        public int Size;
        public ushort VendorId;
        public ushort ProductId;
        public ushort VersionNumber;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid guid);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetAttributes(SafeFileHandle device, ref HidAttributes attributes);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_SetOutputReport(SafeFileHandle device, byte[] report, int reportLength);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator,
        IntPtr parent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr info, IntPtr deviceInfo,
        ref Guid classGuid, uint index, ref DeviceInterfaceData data);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr info,
        ref DeviceInterfaceData data, IntPtr detail, uint detailSize,
        out uint requiredSize, IntPtr deviceInfo);

    [DllImport("setupapi.dll")]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr info);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share,
        IntPtr security, uint creation, uint flags, IntPtr template);

    public bool IsOpen { get { return handle != null && !handle.IsInvalid && !handle.IsClosed; } }

    public AlienFxHidDevice()
    {
        Open();
        if (IsOpen)
        {
            // Enter APIv4 edit mode once per application lifetime.
            Send(new byte[] { 0x03, 0x21, 0x00, 0x04, 0xff, 0xff });
            Send(new byte[] { 0x03, 0x21, 0x00, 0x01, 0xff, 0xff });
        }
    }

    private void Open()
    {
        Guid hidGuid;
        HidD_GetHidGuid(out hidGuid);
        IntPtr info = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
            DigcfPresent | DigcfDeviceInterface);
        if (info == new IntPtr(-1)) return;
        try
        {
            for (uint index = 0; ; index++)
            {
                DeviceInterfaceData data = new DeviceInterfaceData();
                data.Size = Marshal.SizeOf(typeof(DeviceInterfaceData));
                if (!SetupDiEnumDeviceInterfaces(info, IntPtr.Zero, ref hidGuid, index, ref data)) break;
                uint required;
                SetupDiGetDeviceInterfaceDetail(info, ref data, IntPtr.Zero, 0, out required, IntPtr.Zero);
                if (required == 0) continue;
                IntPtr detail = Marshal.AllocHGlobal((int)required);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(info, ref data, detail, required,
                        out required, IntPtr.Zero)) continue;
                    string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                    SafeFileHandle candidate = CreateFile(path, GenericRead | GenericWrite,
                        FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting,
                        FileFlagSequentialScan, IntPtr.Zero);
                    if (candidate.IsInvalid) { candidate.Dispose(); continue; }
                    HidAttributes attributes = new HidAttributes();
                    attributes.Size = Marshal.SizeOf(typeof(HidAttributes));
                    if (HidD_GetAttributes(candidate, ref attributes) &&
                        attributes.VendorId == 0x187c && attributes.ProductId == 0x0550)
                    {
                        handle = candidate;
                        return;
                    }
                    candidate.Dispose();
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(info); }
    }

    private bool Send(byte[] command)
    {
        if (!IsOpen) return false;
        byte[] report = new byte[ReportLength];
        // Report ID is zero for AlienFX APIv4; command begins at report byte 1.
        Array.Copy(command, 0, report, 1, Math.Min(command.Length, ReportLength - 1));
        return HidD_SetOutputReport(handle, report, report.Length);
    }

    public bool Apply(BacklightSettings settings)
    {
        if (!IsOpen) return false;
        int[] ids = { 8, 9, 10, 11 };
        for (int i = 0; i < 4; i++)
        {
            Color color = settings.Brightness == 0 ? Color.Black : settings.Zones[i];
            if (!Send(new byte[] { 0x03, 0x27, color.R, color.G, color.B, 0, 1, (byte)ids[i] }))
                return false;
        }
        if (settings.Brightness > 0)
        {
            int scaled = settings.Brightness * 100 / 255;
            if (!Send(new byte[] { 0x03, 0x26, (byte)(100 - scaled), 0, 4, 8, 9, 10, 11 }))
                return false;
        }
        return true;
    }

    public void Dispose()
    {
        if (handle != null) handle.Dispose();
        handle = null;
    }
}

internal sealed class LightController : IDisposable
{
    private AlienFxHidDevice device;
    private readonly object gate = new object();

    public LightController()
    {
        device = new AlienFxHidDevice();
    }

    public void Apply(BacklightSettings settings, Action<string> completed)
    {
        BacklightSettings snapshot = settings.Clone();
        ThreadPool.QueueUserWorkItem(delegate
        {
            string result;
            lock (gate)
            {
                try
                {
                    bool applied = device.Apply(snapshot);
                    if (!applied)
                    {
                        device.Dispose();
                        device = new AlienFxHidDevice();
                        applied = device.Apply(snapshot);
                    }
                    result = applied ? "OK" : "HID write failed";
                }
                catch (Exception ex)
                {
                    result = ex.GetType().Name;
                }
            }
            if (completed != null) completed(result);
        });
    }

    public void Dispose()
    {
        device.Dispose();
    }
}

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmAppHotkey = 0x8001;
    private const uint VkF18 = 0x81;
    private const uint VkF5 = 0x74;
    private const uint ModShift = 0x0004;
    private const uint ModControl = 0x0002;
    private const int CleanHotkeyId = 0x5516;
    private IntPtr hook = IntPtr.Zero;
    private readonly HookProc hookProc;
    public event Action Pressed;
    public bool Registered { get; private set; }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, HookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr handle, int id);

    public HotkeyWindow()
    {
        CreateParams parameters = new CreateParams();
        parameters.Caption = "G15BacklightHotkey";
        parameters.Parent = new IntPtr(-3);
        CreateHandle(parameters);
        hookProc = KeyboardHook;
        hook = SetWindowsHookEx(WhKeyboardLl, hookProc, IntPtr.Zero, 0);
        bool cleanRegistered = RegisterHotKey(Handle, CleanHotkeyId, ModControl | ModShift, VkF5);
        Registered = hook != IntPtr.Zero && cleanRegistered;
    }

    private IntPtr KeyboardHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && (wParam.ToInt32() == WmKeyDown || wParam.ToInt32() == WmSysKeyDown))
        {
            int virtualKey = Marshal.ReadInt32(lParam);
            if (virtualKey == VkF18)
            {
                PostMessage(Handle, WmAppHotkey, IntPtr.Zero, IntPtr.Zero);
                return new IntPtr(1); // Suppress the original F18 so it cannot toggle lighting twice.
            }
        }
        return CallNextHookEx(hook, code, wParam, lParam);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmAppHotkey ||
            (message.Msg == 0x0312 && message.WParam.ToInt32() == CleanHotkeyId))
        {
            Action handler = Pressed;
            if (handler != null) handler();
        }
        base.WndProc(ref message);
    }

    public void Dispose()
    {
        if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
        UnregisterHotKey(Handle, CleanHotkeyId);
        hook = IntPtr.Zero;
        Registered = false;
        DestroyHandle();
    }
}

internal sealed class SettingsForm : Form
{
    private readonly BacklightSettings editing;
    private readonly Button[] zoneButtons = new Button[4];
    private readonly TrackBar brightness = new TrackBar();
    private readonly Label brightnessLabel = new Label();
    private readonly TrackBar dimBrightness = new TrackBar();
    private readonly Label dimBrightnessLabel = new Label();
    private readonly Label statusLabel = new Label();
    private readonly Action<BacklightSettings, SettingsForm> apply;

    public SettingsForm(BacklightSettings current, Action<BacklightSettings, SettingsForm> applyAction)
    {
        editing = current.Clone();
        apply = applyAction;
        Text = "G15 Backlight";
        ClientSize = new Size(470, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        Icon = SystemIcons.Application;

        Label title = new Label();
        title.Text = "Four-zone keyboard backlight";
        title.Font = new Font(Font.FontFamily, 13F, FontStyle.Bold);
        title.Location = new Point(22, 18);
        title.AutoSize = true;
        Controls.Add(title);

        Label hint = new Label();
        hint.Text = "Click a zone to choose its color.";
        hint.Location = new Point(24, 51);
        hint.AutoSize = true;
        hint.ForeColor = Color.DimGray;
        Controls.Add(hint);

        string[] names = { "Left", "Center-left", "Center-right", "Right" };
        for (int i = 0; i < 4; i++)
        {
            Button button = new Button();
            button.Text = names[i];
            button.Tag = i;
            button.Location = new Point(22 + i * 108, 79);
            button.Size = new Size(98, 58);
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = editing.Zones[i];
            button.ForeColor = TextColor(button.BackColor);
            button.Click += ZoneClick;
            zoneButtons[i] = button;
            Controls.Add(button);
        }

        Label brightnessTitle = new Label();
        brightnessTitle.Text = "Brightness";
        brightnessTitle.Location = new Point(22, 155);
        brightnessTitle.AutoSize = true;
        Controls.Add(brightnessTitle);

        brightness.Minimum = 0;
        brightness.Maximum = 255;
        brightness.TickFrequency = 32;
        brightness.Value = editing.Brightness;
        brightness.Location = new Point(99, 145);
        brightness.Size = new Size(292, 45);
        brightness.ValueChanged += delegate { UpdateBrightnessLabel(); };
        Controls.Add(brightness);

        brightnessLabel.Location = new Point(397, 155);
        brightnessLabel.Size = new Size(54, 22);
        brightnessLabel.TextAlign = ContentAlignment.MiddleRight;
        Controls.Add(brightnessLabel);
        UpdateBrightnessLabel();

        Label dimTitle = new Label();
        dimTitle.Text = "Fn+F5 dim level";
        dimTitle.Location = new Point(22, 202);
        dimTitle.AutoSize = true;
        Controls.Add(dimTitle);

        dimBrightness.Minimum = 1;
        dimBrightness.Maximum = 254;
        dimBrightness.TickFrequency = 32;
        dimBrightness.Value = editing.DimBrightness;
        dimBrightness.Location = new Point(132, 192);
        dimBrightness.Size = new Size(259, 45);
        dimBrightness.ValueChanged += delegate { UpdateDimBrightnessLabel(); };
        Controls.Add(dimBrightness);

        dimBrightnessLabel.Location = new Point(397, 202);
        dimBrightnessLabel.Size = new Size(54, 22);
        dimBrightnessLabel.TextAlign = ContentAlignment.MiddleRight;
        Controls.Add(dimBrightnessLabel);
        UpdateDimBrightnessLabel();

        Button sameColor = MakeButton("Same color", 22, 250, 98);
        sameColor.Click += SameColorClick;
        Controls.Add(sameColor);

        Button white = MakeButton("All white", 130, 250, 88);
        white.Click += delegate { SetAll(Color.White); brightness.Value = 255; };
        Controls.Add(white);

        Button off = MakeButton("Lights off", 228, 250, 88);
        off.Click += delegate { brightness.Value = 0; ApplyNow(); };
        Controls.Add(off);

        Button applyButton = MakeButton("Apply", 342, 250, 104);
        applyButton.Click += delegate { ApplyNow(); };
        Controls.Add(applyButton);

        statusLabel.Text = "Ctrl+Shift+F5: clean bright / dim / off";
        statusLabel.Location = new Point(22, 315);
        statusLabel.Size = new Size(424, 22);
        statusLabel.ForeColor = Color.DimGray;
        Controls.Add(statusLabel);
    }

    private static Button MakeButton(string text, int x, int y, int width)
    {
        Button button = new Button();
        button.Text = text;
        button.Location = new Point(x, y);
        button.Size = new Size(width, 34);
        return button;
    }

    private void ZoneClick(object sender, EventArgs e)
    {
        Button button = (Button)sender;
        int index = (int)button.Tag;
        using (ColorDialog dialog = new ColorDialog())
        {
            dialog.Color = editing.Zones[index];
            dialog.FullOpen = true;
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                editing.Zones[index] = dialog.Color;
                button.BackColor = dialog.Color;
                button.ForeColor = TextColor(dialog.Color);
            }
        }
    }

    private void SameColorClick(object sender, EventArgs e)
    {
        using (ColorDialog dialog = new ColorDialog())
        {
            dialog.Color = editing.Zones[0];
            dialog.FullOpen = true;
            if (dialog.ShowDialog(this) == DialogResult.OK) SetAll(dialog.Color);
        }
    }

    private void SetAll(Color color)
    {
        for (int i = 0; i < 4; i++)
        {
            editing.Zones[i] = color;
            zoneButtons[i].BackColor = color;
            zoneButtons[i].ForeColor = TextColor(color);
        }
    }

    private static Color TextColor(Color background)
    {
        int light = background.R * 299 + background.G * 587 + background.B * 114;
        return light > 150000 ? Color.Black : Color.White;
    }

    private void UpdateBrightnessLabel()
    {
        brightnessLabel.Text = ((brightness.Value * 100 + 127) / 255).ToString() + "%";
    }

    private void UpdateDimBrightnessLabel()
    {
        dimBrightnessLabel.Text = ((dimBrightness.Value * 100 + 127) / 255).ToString() + "%";
    }

    private void ApplyNow()
    {
        editing.Brightness = brightness.Value;
        editing.DimBrightness = dimBrightness.Value;
        statusLabel.Text = "Applying...";
        statusLabel.ForeColor = Color.DimGray;
        apply(editing.Clone(), this);
    }

    public void SetStatus(string value, bool success)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string, bool>(SetStatus), value, success);
            return;
        }
        statusLabel.Text = success ? "Applied and saved" : "Failed: " + value;
        statusLabel.ForeColor = success ? Color.DarkGreen : Color.Firebrick;
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private readonly string settingsPath;
    private readonly BacklightSettings settings;
    private readonly LightController controller;
    private readonly NotifyIcon tray;
    private readonly HotkeyWindow hotkey;
    private readonly System.Windows.Forms.Timer startupTimer;
    private readonly Control activationControl;
    private readonly RegisteredWaitHandle activationWait;
    private int cycleIndex;

    public TrayContext(string baseDir, bool showSettings, EventWaitHandle activationEvent)
    {
        settingsPath = Path.Combine(baseDir, "settings.ini");
        settings = BacklightSettings.Load(settingsPath);
        controller = new LightController();
        cycleIndex = ClosestCycle(settings.Brightness);

        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Open settings", null, delegate { OpenSettings(); });
        menu.Items.Add("All white", null, delegate { SetWhite(); });
        menu.Items.Add("Lights off", null, delegate { SetOff(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, delegate { ExitApp(); });

        tray = new NotifyIcon();
        tray.Text = "G15 Backlight";
        tray.Icon = SystemIcons.Application;
        tray.Visible = true;
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += delegate { OpenSettings(); };

        activationControl = new Control();
        activationControl.CreateControl();
        activationWait = ThreadPool.RegisterWaitForSingleObject(
            activationEvent,
            delegate
            {
                if (!activationControl.IsDisposed)
                    activationControl.BeginInvoke(new Action(OpenSettings));
            },
            null,
            Timeout.Infinite,
            false);

        hotkey = new HotkeyWindow();
        hotkey.Pressed += CycleBrightness;
        if (!hotkey.Registered)
            tray.ShowBalloonTip(4000, "G15 Backlight", "Fn+F5 hotkey could not be registered.", ToolTipIcon.Warning);

        startupTimer = new System.Windows.Forms.Timer();
        startupTimer.Interval = 12000;
        startupTimer.Tick += delegate
        {
            startupTimer.Stop();
            controller.Apply(settings, null);
        };
        startupTimer.Start();
        controller.Apply(settings, null);
        if (showSettings) OpenSettings();
    }

    private int ClosestCycle(int brightness)
    {
        int best = 0;
        int distance = Int32.MaxValue;
        int[] cycle = { 255, settings.DimBrightness, 0 };
        for (int i = 0; i < cycle.Length; i++)
        {
            int current = Math.Abs(cycle[i] - brightness);
            if (current < distance) { best = i; distance = current; }
        }
        return best;
    }

    private void CycleBrightness()
    {
        cycleIndex = (cycleIndex + 1) % 3;
        settings.Brightness = cycleIndex == 0 ? 255 : (cycleIndex == 1 ? settings.DimBrightness : 0);
        settings.Save(settingsPath);
        controller.Apply(settings, null);
    }

    private void OpenSettings()
    {
        SettingsForm form = new SettingsForm(settings, ApplyFromForm);
        form.Show();
        form.Activate();
    }

    private void ApplyFromForm(BacklightSettings updated, SettingsForm form)
    {
        settings.Brightness = updated.Brightness;
        settings.DimBrightness = updated.DimBrightness;
        settings.Zones = (Color[])updated.Zones.Clone();
        cycleIndex = ClosestCycle(settings.Brightness);
        settings.Save(settingsPath);
        controller.Apply(settings, delegate(string result) { form.SetStatus(result, result == "OK"); });
    }

    private void SetWhite()
    {
        settings.Brightness = 255;
        for (int i = 0; i < 4; i++) settings.Zones[i] = Color.White;
        cycleIndex = 0;
        settings.Save(settingsPath);
        controller.Apply(settings, null);
    }

    private void SetOff()
    {
        settings.Brightness = 0;
        cycleIndex = 2;
        settings.Save(settingsPath);
        controller.Apply(settings, null);
    }

    private void ExitApp()
    {
        startupTimer.Stop();
        activationWait.Unregister(null);
        activationControl.Dispose();
        hotkey.Dispose();
        controller.Dispose();
        tray.Visible = false;
        tray.Dispose();
        ExitThread();
    }
}

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        bool created;
        using (Mutex mutex = new Mutex(true, "Local\\DellG155515BacklightControl", out created))
        using (EventWaitHandle activationEvent = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            "Local\\DellG155515BacklightShowSettings"))
        {
            if (!created)
            {
                activationEvent.Set();
                return;
            }
            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "G15Backlight");
            Directory.CreateDirectory(baseDir);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool showSettings = args.Length > 0 && args[0] == "--settings";
            Application.Run(new TrayContext(baseDir, showSettings, activationEvent));
        }
    }
}
