using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DiagnosticLabs.StationSetup.Core;

/// <summary>
/// Saves the share login in Windows Credential Manager for the signed-in user, which is what
/// "cmdkey /add:SERVER /user:SERVER\dlupdate /pass:..." does, without putting the password on a command line.
/// </summary>
internal static class ShareCredential
{
    private const uint CredTypeDomainPassword = 2;
    private const uint CredPersistLocalMachine = 2;

    public static void Save(string host, string user, string password)
    {
        var blob = Marshal.StringToCoTaskMemUni(password);
        try
        {
            var cred = new Credential
            {
                Type = CredTypeDomainPassword,
                TargetName = host,
                UserName = user,
                CredentialBlob = blob,
                CredentialBlobSize = (uint)(password.Length * 2),
                Persist = CredPersistLocalMachine,
                Comment = "Diagnostic Labs update share",
            };
            if (!CredWrite(ref cred, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            Marshal.ZeroFreeCoTaskMemUnicode(blob);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, uint flags);
}
