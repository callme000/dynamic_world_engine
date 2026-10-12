using System;
using System.Threading;
using System.Windows.Forms;

namespace DynamicWorldEngine.Utils
{
    public static class ClipboardHelper
    {
        /// <summary>
        /// Retrieves the current text from the Windows Clipboard safely across threads.
        /// Spawns an STA thread to comply with OLE/COM requirements in GTA V's native runtime.
        /// </summary>
        public static string GetClipboardText()
        {
            string clipboardText = null;
            try
            {
                Thread staThread = new Thread(() =>
                {
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            clipboardText = Clipboard.GetText();
                        }
                    }
                    catch
                    {
                        clipboardText = null;
                    }
                });

                staThread.SetApartmentState(ApartmentState.STA);
                staThread.IsBackground = true;
                staThread.Start();
                if (!staThread.Join(400))
                {
                    try { staThread.Abort(); } catch { }
                }
            }
            catch
            {
                clipboardText = null;
            }

            return clipboardText != null ? clipboardText.Trim() : string.Empty;
        }
    }
}
