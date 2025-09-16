using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;

public class PrivacyMaskOverlay : Form
{
    // ---- Win32 constants/flags ----
    const int GWL_EXSTYLE = -20;
    const int WS_EX_LAYERED = 0x80000;
    const int WS_EX_TRANSPARENT = 0x20;
    const int WS_EX_TOOLWINDOW = 0x80;
    const int WS_EX_NOACTIVATE = 0x08000000;

    const int LWA_ALPHA = 0x2;

    const int WM_NCHITTEST = 0x84;
    const int HTTRANSPARENT = -1;
    const int HTCLIENT = 1;
    // Hit test values for resizing
    const int HTLEFT = 10;
    const int HTRIGHT = 11;
    const int HTTOP = 12;
    const int HTTOPLEFT = 13;
    const int HTTOPRIGHT = 14;
    const int HTBOTTOM = 15;
    const int HTBOTTOMLEFT = 16;
    const int HTBOTTOMRIGHT = 17;

    const int WM_HOTKEY = 0x0312;
    const uint MOD_ALT = 0x0001;
    const uint MOD_CONTROL = 0x0002;

    const uint WDA_NONE = 0x0;
    const uint WDA_MONITOR = 0x1;            // Fica preto em captura
    const uint WDA_EXCLUDEFROMCAPTURE = 0x11; // Exclui da captura (Win10 2004+)

    // ---- P/Invoke ----
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int n);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int n, int v);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr h, uint affinity);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    const int WM_NCLBUTTONDOWN = 0x00A1;
    const int HTCAPTION = 2;

    bool blackMask = true;   // modo "preto em captura"
    bool excluded = false;   // modo "excluir da captura"

    public PrivacyMaskOverlay()
    {
        // Normal window chrome
        FormBorderStyle = FormBorderStyle.Sizable;
        ControlBox = true;
        MinimizeBox = true;
        MaximizeBox = true;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new System.Drawing.Size(800, 600);
    Text = "Privacy Mask";
        // Transparent client area
        BackColor = Color.Lime;
        TransparencyKey = Color.Lime;
        TopMost = true; // keep overlay on top so it doesn't get covered when clicking through

        // Create menu (use system colors so it follows current theme)
        var menu = new MenuStrip();
        menu.BackColor = SystemColors.MenuBar;
        menu.ForeColor = SystemColors.MenuText;
        menu.RenderMode = ToolStripRenderMode.System;
        menu.Padding = new Padding(2);

        var attachMenu = new ToolStripMenuItem("Attach");
        var refreshItem = new ToolStripMenuItem("Refresh window list");
        var windowsSubmenu = new ToolStripMenuItem("Windows");
        refreshItem.Click += (s, e) => { BuildWindowList(windowsSubmenu); };

        attachMenu.DropDownItems.Add(refreshItem);
        attachMenu.DropDownItems.Add(windowsSubmenu);
        menu.Items.Add(attachMenu);
        this.MainMenuStrip = menu;
        this.Controls.Add(menu);

        // Allow dragging by mouse down on the menu strip (helps after maximize/restore)
        menu.MouseDown += (s, e) => {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            }
        };

        // set a simple lock icon for window and taskbar
        try { this.Icon = CreateLockIcon(32, 32); } catch { }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Estilos para overlay normal, mas click-through no client area
        int ex = GetWindowLong(Handle, GWL_EXSTYLE);
        // Keep layered style for transparency but DO NOT set WS_EX_TOOLWINDOW — it removes
        // standard window chrome (minimize/maximize and normal borders).
        // Also ensure WS_EX_TRANSPARENT is never set globally
        ex |= WS_EX_LAYERED;
        ex &= ~WS_EX_TRANSPARENT; // ensure transparent flag is NEVER set globally
        SetWindowLong(Handle, GWL_EXSTYLE, ex);

        // Por padrão: "preto em captura" (protege conteúdo debaixo)
        SetWindowDisplayAffinity(Handle, WDA_MONITOR); // Ref.: WDA_MONITOR [1](https://learn.microsoft.com/pt-br/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity)

        // Hotkeys globais:
        // Ctrl+Alt+1 → alterna preto em captura
        // Ctrl+Alt+2 → alterna excluir da captura (2004+)
        // Ctrl+Alt+Q → sair
        RegisterHotKey(Handle, 1, MOD_CONTROL | MOD_ALT, (uint)Keys.D1);
        RegisterHotKey(Handle, 2, MOD_CONTROL | MOD_ALT, (uint)Keys.D2);
        RegisterHotKey(Handle, 9, MOD_CONTROL | MOD_ALT, (uint)Keys.Q);

        // Build initial window list in the menu (if menu exists)
        try
        {
            var menu = this.Controls.OfType<MenuStrip>().FirstOrDefault();
            if (menu != null)
            {
                var attachMenu = menu.Items.Cast<ToolStripItem>().OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Text == "Attach");
                if (attachMenu != null)
                {
                    var windowsSubmenu = attachMenu.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(it => it.Text == "Windows");
                    if (windowsSubmenu != null)
                        BuildWindowList(windowsSubmenu);
                }
            }
        }
        catch { }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Reset display affinity so we don't leave any state behind
        try { SetWindowDisplayAffinity(Handle, WDA_NONE); } catch { }
        base.OnFormClosed(e);
    }

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(value);
        if (value && this.IsHandleCreated)
        {
            // Ensure correct window styles after visibility changes
            try
            {
                int ex = GetWindowLong(Handle, GWL_EXSTYLE);
                ex |= WS_EX_LAYERED;
                ex &= ~WS_EX_TRANSPARENT; // ensure transparent flag is NEVER set globally
                SetWindowLong(Handle, GWL_EXSTYLE, ex);
            }
            catch { }
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterHotKey(Handle, 1);
        UnregisterHotKey(Handle, 2);
        UnregisterHotKey(Handle, 9);
        base.OnHandleDestroyed(e);
    }

    // Garante que o mouse "atravessa" mesmo quando WS_EX_TRANSPARENT não basta
    protected override void WndProc(ref Message m)
    {
        // Permite click-through apenas no client area (não barra/título/bordas)
        if (m.Msg == WM_NCHITTEST)
        {
            // Let default processing determine hit-test (so resize/drag behavior works)
            base.WndProc(ref m);
            // If it's client area, only make it click-through if it's below the top drag area
            if ((int)m.Result == HTCLIENT)
            {
                int x = (short)((uint)m.LParam & 0xFFFF);
                int y = (short)(((uint)m.LParam >> 16) & 0xFFFF);
                var pt = PointToClient(new Point(x, y));
                int menuH = this.MainMenuStrip?.Height ?? 0;
                int dragArea = Math.Max(24, menuH + 4);
                if (pt.Y >= dragArea)
                {
                    m.Result = (IntPtr)HTTRANSPARENT; // allow clicks to pass through for the main client area
                }
                // otherwise leave as HTCLIENT so we receive mouse events in the top area
                return;
            }
            return;
        }

        const int WM_LBUTTONDOWN = 0x0201;
        // If the left mouse button is pressed in the top area of the client, start a caption drag.
        if (m.Msg == WM_LBUTTONDOWN)
        {
            int x = (short)((uint)m.LParam & 0xFFFF);
            int y = (short)(((uint)m.LParam >> 16) & 0xFFFF);
            var pt = new Point(x, y);
            // Consider top area including menu height
            int menuH = this.MainMenuStrip?.Height ?? 0;
            int dragArea = Math.Max(24, menuH + 4); // a reasonable drag height
            if (pt.Y < dragArea)
            {
                try { ReleaseCapture(); } catch { }
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                return;
            }
        }

        if (m.Msg == WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            if (id == 1) // Ctrl+Alt+1: alterna WDA_MONITOR
            {
                excluded = false;
                blackMask = !blackMask;
                SetWindowDisplayAffinity(Handle, blackMask ? WDA_MONITOR : WDA_NONE);
            }
            else if (id == 2) // Ctrl+Alt+2: alterna WDA_EXCLUDEFROMCAPTURE
            {
                blackMask = false;
                excluded = !excluded;
                SetWindowDisplayAffinity(Handle, excluded ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);
            }
            else if (id == 9) // Ctrl+Alt+Q: sair
            {
                Close();
            }
        }

        base.WndProc(ref m);

        // Re-apply layered style and ensure WS_EX_TRANSPARENT is cleared after size/position changes
        const int WM_SIZE = 0x0005;
        const int WM_WINDOWPOSCHANGED = 0x0047;
        const int WM_SHOWWINDOW = 0x0018;
        const int WM_SYSCOMMAND = 0x0112;
        
        if (m.Msg == WM_SIZE || m.Msg == WM_WINDOWPOSCHANGED || m.Msg == WM_SHOWWINDOW || m.Msg == WM_SYSCOMMAND)
        {
            // Use BeginInvoke to ensure this happens after the window state change is complete
            this.BeginInvoke((Action)(() => {
                try
                {
                    if (this.IsHandleCreated && !this.IsDisposed)
                    {
                        int ex = GetWindowLong(Handle, GWL_EXSTYLE);
                        ex |= WS_EX_LAYERED;
                        ex &= ~WS_EX_TRANSPARENT; // ensure transparent flag is NEVER set globally
                        SetWindowLong(Handle, GWL_EXSTYLE, ex);
                    }
                }
                catch { }
            }));
        }
    }

    void TryReactivate()
    {
        try
        {
            if (!this.IsDisposed && this.IsHandleCreated)
            {
                this.BeginInvoke((Action)(() => {
                    SetForegroundWindow(this.Handle);
                }));
            }
        }
        catch { }
    }

    // ----- Window enumeration & attach logic -----
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", SetLastError = true)]
    static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    void BuildWindowList(ToolStripMenuItem windowsMenu)
    {
        windowsMenu.DropDownItems.Clear();
        var items = new List<(IntPtr hWnd, string title)>();
        EnumWindows((hWnd, lParam) => {
            try
            {
                if (!IsWindowVisible(hWnd)) return true;
                int len = GetWindowTextLength(hWnd);
                if (len == 0) return true;
                var sb = new System.Text.StringBuilder(len + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();
                if (string.IsNullOrWhiteSpace(title)) return true;
                items.Add((hWnd, title));
            }
            catch { }
            return true;
        }, IntPtr.Zero);

        if (items.Count == 0)
        {
            windowsMenu.DropDownItems.Add(new ToolStripMenuItem("No windows found") { Enabled = false });
            return;
        }

        foreach (var it in items)
        {
            var mi = new ToolStripMenuItem(it.title);
            mi.Tag = it.hWnd;
            mi.Click += (s, e) => { AttachToWindow((IntPtr)mi.Tag); };
            windowsMenu.DropDownItems.Add(mi);
        }
    }

    void AttachToWindow(IntPtr target)
    {
        try
        {
            if (!GetWindowRect(target, out RECT r)) return;
            // Add padding so overlay slightly larger (a little longer as requested)
            int pad = 32;
            var newLeft = r.Left - pad;
            var newTop = r.Top - pad;
            var newWidth = (r.Right - r.Left) + pad * 2;
            var newHeight = (r.Bottom - r.Top) + pad * 2;

            // Move & resize the overlay on UI thread
            this.BeginInvoke((Action)(() => {
                // Keep normal window chrome so the app can close cleanly and be visible in taskbar.
                // This avoids leaving a ghost window behind when the app exits.
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.ControlBox = true;
                this.MinimizeBox = true;
                this.MaximizeBox = true;
                this.ShowInTaskbar = true;
                this.Bounds = new System.Drawing.Rectangle(newLeft, newTop, newWidth, newHeight);
                this.TopMost = true;
            }));
        }
        catch { }
    }

    // Create a simple lock icon in-memory
    Icon CreateLockIcon(int width, int height)
    {
        var bmp = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bmp))
        using (var pen = new Pen(Color.Black, 2))
        using (var br = new SolidBrush(Color.Black))
        {
            // Start with a fully transparent background
            g.Clear(Color.Transparent);
            // Draw rounded lock body
            var body = new Rectangle(width/6, height/3, width*2/3, height/2);
            g.FillRectangle(br, body);
            g.DrawRectangle(pen, body);
            // Draw shackle
            var shackleRect = new Rectangle(width/4, 0, width/2, height/2);
            g.DrawArc(pen, shackleRect, 200, 140);
        }
        // Ensure transparency is preserved where possible
        try { bmp.MakeTransparent(); } catch { }
        IntPtr hIcon = bmp.GetHicon();
        var ico = Icon.FromHandle(hIcon);
        return ico;
    }
}
