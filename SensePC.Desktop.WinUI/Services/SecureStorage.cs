using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Serilog;
using System;
using System.Threading.Tasks;

namespace SensePC.Desktop.WinUI.Services;

/// <summary>
/// Secure storage implementation using Windows Credential Manager and DPAPI
/// </summary>
public class SecureStorage : ISecureStorage
{
    private const string CredentialPrefix = "SensePC_Native_"; // Changed prefix for safety
    
    public Task<bool> SetAsync(string key, string value)
    {
        return Task.Run(() =>
        {
            try
            {
                // Encrypt using DPAPI (CurrentUser scope)
                var plainBytes = Encoding.UTF8.GetBytes(value);
                var encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );
                
                var credential = new CREDENTIAL
                {
                    Type = CRED_TYPE.GENERIC,
                    TargetName = CredentialPrefix + key,
                    CredentialBlobSize = (uint)encryptedBytes.Length,
                    CredentialBlob = Marshal.AllocHGlobal(encryptedBytes.Length),
                    Persist = CRED_PERSIST.LOCAL_MACHINE,
                    UserName = Environment.UserName
                };

                Marshal.Copy(encryptedBytes, 0, credential.CredentialBlob, encryptedBytes.Length);

                var result = CredWrite(ref credential, 0);
                
                Marshal.FreeHGlobal(credential.CredentialBlob);

                if (result)
                {
                    Log.Debug("Credential stored: {Key}", key);
                }
                else
                {
                    Log.Warning("Failed to store credential: {Key}, Error: {Error}", key, Marshal.GetLastWin32Error());
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error storing credential: {Key}", key);
                return false;
            }
        });
    }

    public Task<string?> GetAsync(string key)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!CredRead(CredentialPrefix + key, CRED_TYPE.GENERIC, 0, out var credentialPtr))
                {
                    Log.Debug("Credential not found: {Key}", key);
                    return null;
                }

                try
                {
                    var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
                    
                    if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
                    {
                        return null;
                    }

                    var encryptedBytes = new byte[credential.CredentialBlobSize];
                    Marshal.Copy(credential.CredentialBlob, encryptedBytes, 0, (int)credential.CredentialBlobSize);

                    // Decrypt using DPAPI
                    var plainBytes = ProtectedData.Unprotect(
                        encryptedBytes,
                        null,
                        DataProtectionScope.CurrentUser
                    );

                    return Encoding.UTF8.GetString(plainBytes);
                }
                finally
                {
                    CredFree(credentialPtr);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving credential: {Key}", key);
                return null;
            }
        });
    }

    public Task<bool> RemoveAsync(string key)
    {
        return Task.Run(() =>
        {
            try
            {
                var result = CredDelete(CredentialPrefix + key, CRED_TYPE.GENERIC, 0);
                
                if (result)
                {
                    Log.Debug("Credential removed: {Key}", key);
                }
                else
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error != 1168) // ERROR_NOT_FOUND
                    {
                        Log.Warning("Failed to remove credential: {Key}, Error: {Error}", key, error);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error removing credential: {Key}", key);
                return false;
            }
        });
    }

    public Task<bool> ExistsAsync(string key)
    {
        return Task.Run(() =>
        {
            try
            {
                if (CredRead(CredentialPrefix + key, CRED_TYPE.GENERIC, 0, out var credentialPtr))
                {
                    CredFree(credentialPtr);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        });
    }

    #region Windows Credential Manager P/Invoke

    private enum CRED_TYPE : uint
    {
        GENERIC = 1,
        DOMAIN_PASSWORD = 2,
        DOMAIN_CERTIFICATE = 3,
        DOMAIN_VISIBLE_PASSWORD = 4,
        GENERIC_CERTIFICATE = 5,
        DOMAIN_EXTENDED = 6,
        MAXIMUM = 7
    }

    private enum CRED_PERSIST : uint
    {
        SESSION = 1,
        LOCAL_MACHINE = 2,
        ENTERPRISE = 3
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public CRED_TYPE Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public CRED_PERSIST Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, CRED_TYPE type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, CRED_TYPE type, int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree([In] IntPtr buffer);

    #endregion
}
