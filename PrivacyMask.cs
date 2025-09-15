using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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
        Text = "PrivacyMask Overlay";
        // Transparent client area
        BackColor = System.Drawing.Color.Lime;
        TransparencyKey = System.Drawing.Color.Lime;
        TopMost = true; // keep overlay on top so it doesn't get covered when clicking through
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

    // Estilos para overlay normal, mas click-through no client area
    int ex = GetWindowLong(Handle, GWL_EXSTYLE);
    // Keep layered style for transparency but DO NOT set WS_EX_TOOLWINDOW — it removes
    // standard window chrome (minimize/maximize and normal borders).
    ex |= WS_EX_LAYERED;
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
            base.WndProc(ref m);
            if ((int)m.Result == HTCLIENT)
            {
                // Make client area click-through but keep window topmost and focused.
                m.Result = (IntPtr)HTTRANSPARENT;
                return;
            }
            return;
        }

        // If the window loses activation because user clicked through, immediately reactivate it
        const int WM_ACTIVATEAPP = 0x001C;
        if (m.Msg == WM_ACTIVATEAPP)
        {
            // wParam == 0 -> deactivating, 1 -> activating
            int activating = m.WParam.ToInt32();
            if (activating == 0)
            {
                // Reactivate ourselves to keep focus for hotkeys. PostMessage to avoid recursion.
                // Use Win32 SetForegroundWindow via P/Invoke
                TryReactivate();
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
}
