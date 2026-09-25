using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using AFK4.SetupWizard.Core;
using AFK4.SetupWizard.Core.Kiosk;
using Microsoft.Win32;

namespace AFK4.SetupWizard;

/// <summary>
/// Системная половина киоска (спека оболочки, §6.1): учётка через NetUserAdd, пароль автовхода
/// секретом LSA — как у Sysinternals Autologon, — реестр ПК в 64-битном виде и куст игрока с
/// загрузкой NTUSER.DAT. Мастер работает от администратора: без этого ни одно из действий не пройдёт.
/// </summary>
public sealed class WindowsKioskMachine : IKioskMachine
{
    private const int NerrSuccess = 0;
    private const int NerrUserNotFound = 2221;
    private const int NerrUserExists = 2224;
    private const uint UserPrivUser = 1;
    private const uint UfScript = 0x0001;
    private const uint UfPasswdCantChange = 0x0040;
    private const uint UfDontExpirePasswd = 0x10000;
    private const string AutologonSecretName = "DefaultPassword";
    private const string TemporaryHiveName = "AFK4.KioskSetup";

    public string EnsureUser(string userName, string password)
    {
        var info = new UserInfo1
        {
            Name = userName,
            Password = password,
            Privilege = UserPrivUser,
            Comment = "AFK4: гость за этим ПК входит сюда автоматически.",
            // Пароль не меняется и не устаревает: его знает только LSA, и сменить его некому.
            Flags = UfScript | UfPasswdCantChange | UfDontExpirePasswd
        };
        var result = NetUserAdd(null, 1, ref info, out _);
        if (result == NerrUserExists)
        {
            var reset = new UserInfo1003 { Password = password };
            result = NetUserSetInfo(null, userName, 1003, ref reset, out _);
        }

        if (result != NerrSuccess)
        {
            throw new Win32Exception(result, $"Could not create or update the local user '{userName}'.");
        }

        return new NTAccount(Environment.MachineName, userName).Translate(typeof(SecurityIdentifier)).Value;
    }

    public void DeleteUser(string userName, string sid)
    {
        var result = NetUserDel(null, userName);
        if (result != NerrSuccess && result != NerrUserNotFound)
        {
            throw new Win32Exception(result, $"Could not delete the local user '{userName}'.");
        }

        // Профиль не уходит вместе с учёткой: без этого в C:\Users остаётся папка сироты.
        if (!DeleteProfile(sid, null, null))
        {
            var error = Marshal.GetLastWin32Error();
            const int errorFileNotFound = 2;
            if (error != errorFileNotFound)
            {
                SetupWizardStartupLog.Write($"The kiosk profile {sid} could not be deleted.", new Win32Exception(error));
            }
        }
    }

    public void StoreAutologonPassword(string? password)
    {
        var attributes = new LsaObjectAttributes { Length = Marshal.SizeOf<LsaObjectAttributes>() };
        const uint policyCreateSecret = 0x00000020;
        ThrowOnLsaError(LsaOpenPolicy(IntPtr.Zero, ref attributes, policyCreateSecret, out var policy), "open the LSA policy");
        var keyBuffer = Marshal.StringToHGlobalUni(AutologonSecretName);
        var dataBuffer = password is null ? IntPtr.Zero : Marshal.StringToHGlobalUni(password);
        try
        {
            var key = UnicodeString(keyBuffer, AutologonSecretName.Length);
            if (password is null)
            {
                // Нулевые данные удаляют секрет. Его может и не быть — это тоже «удалён».
                const uint statusObjectNameNotFound = 0xC0000034;
                var deleted = LsaStorePrivateData(policy, ref key, IntPtr.Zero);
                if (deleted != statusObjectNameNotFound)
                {
                    ThrowOnLsaError(deleted, "delete the autologon secret");
                }

                return;
            }

            var data = UnicodeString(dataBuffer, password.Length);
            var dataPointer = Marshal.AllocHGlobal(Marshal.SizeOf<LsaUnicodeString>());
            try
            {
                Marshal.StructureToPtr(data, dataPointer, fDeleteOld: false);
                ThrowOnLsaError(LsaStorePrivateData(policy, ref key, dataPointer), "store the autologon secret");
            }
            finally
            {
                Marshal.FreeHGlobal(dataPointer);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(keyBuffer);
            if (dataBuffer != IntPtr.Zero)
            {
                // Пароль не должен пережить эту строку в памяти мастера.
                Marshal.ZeroFreeGlobalAllocUnicode(dataBuffer);
            }

            LsaClose(policy);
        }
    }

    public KioskRegistryValue ReadMachineValue(string key, string name)
    {
        using var root = MachineRoot();
        using var subKey = root.OpenSubKey(key);
        return subKey?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) switch
        {
            string text => KioskRegistryValue.Of(text),
            int number => KioskRegistryValue.Of(number),
            _ => new KioskRegistryValue(null, null)
        };
    }

    public void WriteMachineValue(string key, string name, KioskRegistryValue value)
    {
        using var root = MachineRoot();
        using var subKey = root.CreateSubKey(key, writable: true);
        if (value.Number is { } number)
        {
            subKey.SetValue(name, number, RegistryValueKind.DWord);
        }
        else
        {
            subKey.SetValue(name, value.Text ?? string.Empty, RegistryValueKind.String);
        }
    }

    public void DeleteMachineValue(string key, string name)
    {
        using var root = MachineRoot();
        using var subKey = root.OpenSubKey(key, writable: true);
        subKey?.DeleteValue(name, throwOnMissingValue: false);
    }

    public void WriteUserValue(string sid, string key, string name, string value)
    {
        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);

        // Игрок сейчас вошёл — его куст уже загружен под SID.
        using (var loaded = users.OpenSubKey(sid, writable: true))
        {
            if (loaded is not null)
            {
                using var target = loaded.CreateSubKey(key, writable: true);
                target.SetValue(name, value, RegistryValueKind.String);
                return;
            }
        }

        // Не входил ни разу — профиля ещё нет. Windows создаёт его сама, а куст подгружается на время.
        var hive = Path.Combine(EnsureProfile(sid), "NTUSER.DAT");
        EnablePrivilege("SeRestorePrivilege");
        EnablePrivilege("SeBackupPrivilege");
        var hkeyUsers = new IntPtr(unchecked((int)0x80000003));
        ThrowOnError(RegLoadKey(hkeyUsers, TemporaryHiveName, hive), $"load the kiosk hive '{hive}'");
        try
        {
            using var temporary = users.OpenSubKey(TemporaryHiveName, writable: true)
                ?? throw new InvalidOperationException("The kiosk hive loaded but cannot be opened.");
            using var target = temporary.CreateSubKey(key, writable: true);
            target.SetValue(name, value, RegistryValueKind.String);
        }
        finally
        {
            // Все дескрипторы куста закрыты выше: с открытым Windows выгрузить его не даст.
            ThrowOnError(RegUnLoadKey(hkeyUsers, TemporaryHiveName), "unload the kiosk hive");
        }
    }

    private static RegistryKey MachineRoot() => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

    private static string EnsureProfile(string sid)
    {
        var path = new StringBuilder(260);
        var result = CreateProfile(sid, KioskSettings.UserName, path, (uint)path.Capacity);
        const int alreadyExists = unchecked((int)0x800700B7);
        if (result == 0)
        {
            return path.ToString();
        }

        if (result != alreadyExists)
        {
            Marshal.ThrowExceptionForHR(result);
        }

        using var root = MachineRoot();
        using var profile = root.OpenSubKey($@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{sid}");
        var image = profile?.GetValue("ProfileImagePath") as string
            ?? throw new InvalidOperationException($"The profile of {sid} exists but has no path.");
        return Environment.ExpandEnvironmentVariables(image);
    }

    private static void EnablePrivilege(string privilege)
    {
        const uint tokenAdjustPrivileges = 0x0020;
        const uint tokenQuery = 0x0008;
        const uint sePrivilegeEnabled = 0x00000002;
        if (!OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle, tokenAdjustPrivileges | tokenQuery, out var token))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the wizard's own token.");
        }

        try
        {
            if (!LookupPrivilegeValue(null, privilege, out var luid))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unknown privilege {privilege}.");
            }

            var state = new TokenPrivileges { Count = 1, Luid = luid, Attributes = sePrivilegeEnabled };
            if (!AdjustTokenPrivileges(token, false, ref state, 0, IntPtr.Zero, IntPtr.Zero) || Marshal.GetLastWin32Error() != 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"The wizard could not enable {privilege}.");
            }
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static LsaUnicodeString UnicodeString(IntPtr buffer, int characters) => new()
    {
        Length = (ushort)(characters * 2),
        MaximumLength = (ushort)(characters * 2 + 2),
        Buffer = buffer
    };

    private static void ThrowOnLsaError(uint status, string action)
    {
        if (status != 0)
        {
            throw new Win32Exception((int)LsaNtStatusToWinError(status), $"Could not {action}.");
        }
    }

    private static void ThrowOnError(int result, string action)
    {
        if (result != 0)
        {
            throw new Win32Exception(result, $"Could not {action}.");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct UserInfo1
    {
        public string Name;
        public string Password;
        public uint PasswordAge;
        public uint Privilege;
        public string? HomeDirectory;
        public string? Comment;
        public uint Flags;
        public string? ScriptPath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct UserInfo1003
    {
        public string Password;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LsaUnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LsaObjectAttributes
    {
        public int Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges
    {
        public uint Count;
        public Luid Luid;
        public uint Attributes;
    }

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetUserAdd(string? server, int level, ref UserInfo1 info, out int parameterError);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetUserSetInfo(string? server, string userName, int level, ref UserInfo1003 info, out int parameterError);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetUserDel(string? server, string userName);

    [DllImport("advapi32.dll")]
    private static extern uint LsaOpenPolicy(IntPtr systemName, ref LsaObjectAttributes attributes, uint access, out IntPtr policy);

    [DllImport("advapi32.dll")]
    private static extern uint LsaStorePrivateData(IntPtr policy, ref LsaUnicodeString key, IntPtr privateData);

    [DllImport("advapi32.dll")]
    private static extern uint LsaNtStatusToWinError(uint status);

    [DllImport("advapi32.dll")]
    private static extern uint LsaClose(IntPtr policy);

    [DllImport("userenv.dll", CharSet = CharSet.Unicode)]
    private static extern int CreateProfile(string sid, string userName, StringBuilder profilePath, uint capacity);

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool DeleteProfile(string sid, string? profilePath, string? computerName);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegLoadKey(IntPtr key, string subKey, string file);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegUnLoadKey(IntPtr key, string subKey);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool LookupPrivilegeValue(string? system, string name, out Luid luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TokenPrivileges state, uint length, IntPtr previous, IntPtr returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
