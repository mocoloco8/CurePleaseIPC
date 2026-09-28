namespace CurePlease.Game
{
    using Cortana;
    using System;
    using System.Diagnostics;
    using System.Linq;

    // The one link to the Windower "cortana" addon (CortanaIPC.dll). It binds the first free UDP
    // port in 59332-59341, so Cure Please can run alongside other cortana apps on the same characters.
    public static class CortanaLink
    {
        private static readonly object _lock = new object();
        private static CortanaIPC _ipc;

        public static CortanaIPC Ipc
        {
            get
            {
                lock (_lock)
                {
                    if (_ipc == null)
                    {
                        var ipc = new CortanaIPC { AppName = "CurePlease" };
                        ipc.Start();
                        _ipc = ipc;
                    }
                    return _ipc;
                }
            }
        }

        public static void Shutdown()
        {
            lock (_lock)
            {
                if (_ipc != null) { try { _ipc.Dispose(); } catch { } _ipc = null; }
            }
        }

        // Windower titles the game window with the character name, which is how a POL process is
        // matched to its addon link.
        public static string CharacterName(int processId)
        {
            try { return (Process.GetProcessById(processId).MainWindowTitle ?? "").Trim(); }
            catch { return ""; }
        }

        // Is this character's addon linked and streaming an in-world snapshot?
        public static bool IsReady(string characterName)
        {
            CortanaCharacter c;
            return !string.IsNullOrEmpty(characterName) && Ipc.TryGetCharacter(characterName, out c)
                && c.IsLinked && c.State.IsFresh && c.State.Player != null && c.State.Player.Login == LoginState.InWorld;
        }

        public static string[] LinkedNames()
        {
            return Ipc.LinkedCharacters.Select(c => c.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}
