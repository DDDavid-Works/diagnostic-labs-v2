using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace DiagnosticLabs.StationSetup.Core;

/// <summary>
/// Server side: the local Windows account stations read the update share with (created by Install-Server.ps1),
/// its password, and a copy of that password kept on the server for administrators only.
/// </summary>
internal static class StationAccount
{
    private const int NerrSuccess = 0;
    private const int NerrUserNotFound = 2221;
    private const uint UfDontExpirePasswd = 0x10000;
    private const uint UfAccountDisable = 0x2;

    /// <summary>Outside the share root on purpose: stations must never be able to read it.</summary>
    public static string VaultFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DiagnosticLabs", "station-password.dat");

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>Null when the account does not exist; otherwise whether it is enabled and its password never expires.</summary>
    public static (bool Enabled, bool NeverExpires)? Read(string name = StationPaths.ShareAccount)
    {
        var rc = NetUserGetInfo(null, name, 1, out var buffer);
        if (rc == NerrUserNotFound) return null;
        if (rc != NerrSuccess) throw new Win32Exception(rc);
        try
        {
            var flags = Marshal.PtrToStructure<UserInfo1>(buffer).Flags;
            return ((flags & UfAccountDisable) == 0, (flags & UfDontExpirePasswd) != 0);
        }
        finally
        {
            _ = NetApiBufferFree(buffer);
        }
    }

    /// <summary>Sets the password, makes it never expire, and keeps a protected copy for administrators.</summary>
    public static void SetPassword(string password, string name = StationPaths.ShareAccount)
    {
        var info = new UserInfo1003 { Password = password };
        var rc = NetUserSetInfo(null, name, 1003, ref info, out _);
        if (rc == NerrUserNotFound) throw new InvalidOperationException($"The Windows account {name} does not exist. Run Install-Server.ps1 first.");
        if (rc != NerrSuccess) throw new Win32Exception(rc);

        rc = NetUserGetInfo(null, name, 1, out var buffer);
        if (rc != NerrSuccess) throw new Win32Exception(rc);
        uint flags;
        try { flags = Marshal.PtrToStructure<UserInfo1>(buffer).Flags; }
        finally { _ = NetApiBufferFree(buffer); }

        var update = new UserInfo1008 { Flags = (flags | UfDontExpirePasswd) & ~UfAccountDisable };
        rc = NetUserSetInfo(null, name, 1008, ref update, out _);
        if (rc != NerrSuccess) throw new Win32Exception(rc);

        SaveCopy(password);
    }

    public static string? ReadCopy()
    {
        if (!File.Exists(VaultFile)) return null;
        var plain = ProtectedData.Unprotect(File.ReadAllBytes(VaultFile), null, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(plain);
    }

    private static void SaveCopy(string password)
    {
        var dir = Path.GetDirectoryName(VaultFile)!;
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(VaultFile, ProtectedData.Protect(Encoding.UTF8.GetBytes(password), null, DataProtectionScope.LocalMachine));

        // Administrators and SYSTEM only, nothing inherited from ProgramData (where Users may read).
        var acl = new FileSecurity();
        acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(VaultFile).SetAccessControl(acl);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct UserInfo1
    {
        public IntPtr Name;
        public IntPtr Password;
        public uint PasswordAge;
        public uint Priv;
        public IntPtr HomeDir;
        public IntPtr Comment;
        public uint Flags;
        public IntPtr ScriptPath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct UserInfo1003
    {
        public string Password;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UserInfo1008
    {
        public uint Flags;
    }

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetUserGetInfo(string? serverName, string userName, int level, out IntPtr buffer);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetUserSetInfo(string? serverName, string userName, int level, ref UserInfo1003 buffer, out int parmErr);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetUserSetInfo(string? serverName, string userName, int level, ref UserInfo1008 buffer, out int parmErr);

    [DllImport("netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr buffer);
}
