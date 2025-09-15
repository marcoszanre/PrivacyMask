using System;
using System.Windows.Forms;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Uma janela cobrindo o monitor principal.
        var mask = new PrivacyMaskOverlay();
        Application.Run(mask);
    }    
}