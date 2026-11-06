using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

// Update trust comes only from the RSA signature checked against the key built into this exe.
// HTTPS and GitHub are transport: a tampered, unsigned or older release is rejected.
static class Updater
{
#if UPDATE_TEST
    const string BaseUrl = "http://127.0.0.1:8765/";
#else
    const string BaseUrl = "https://github.com/ggleoo/claude-usage-taskbar/releases/latest/download/";
#endif
    const int MaxManifestBytes = 16 * 1024;
    const int MaxSignatureBytes = 4 * 1024;
    const int MaxExeBytes = 20 * 1024 * 1024;
    const int TimeoutMs = 30000;

    static string NewFile { get { return App.ExePath + ".new"; } }
    static string OldFile { get { return App.ExePath + ".old"; } }

    static bool CanUpdate
    {
        get
        {
#if UPDATE_TEST
            return true;
#else
            return App.IsInstalled && Settings.AutoUpdate && UpdateKey.PublicKeyXml.Length > 0;
#endif
        }
    }

    // Removes what an earlier update left behind; the old exe was still running at that moment.
    public static void CleanUp()
    {
        try
        {
            if (File.Exists(OldFile)) File.Delete(OldFile);
            if (File.Exists(NewFile)) File.Delete(NewFile);
        }
        catch (Exception)
        {
            // Retried on the next start.
        }
    }

    // Network and disk work only; returns true when a verified exe is ready to install.
    public static bool DownloadVerified()
    {
        if (!CanUpdate) return false;
        try
        {
            byte[] manifestBytes = Download("update.json", MaxManifestBytes);
            byte[] signature = Download("update.json.sig", MaxSignatureBytes);
            if (!HasValidSignature(manifestBytes, signature)) return false;

            Dictionary<string, object> manifest = Json.Object(Json.Parse(Encoding.UTF8.GetString(manifestBytes)));
            Version latest = new Version((string)Json.Get(manifest, "version"));
            if (latest <= new Version(App.Version)) return false;

            byte[] exe = Download(App.ExeName, MaxExeBytes);
            if (!Sha256Hex(exe).Equals((string)Json.Get(manifest, "sha256"), StringComparison.OrdinalIgnoreCase)) return false;

            File.WriteAllBytes(NewFile, exe);
            return true;
        }
        catch (Exception)
        {
            return false; // Tried again on the next scheduled check.
        }
    }

    // Windows allows renaming a running exe, which is what lets it replace itself.
    public static bool ApplyAndRestart()
    {
        string exe = App.ExePath;
        try
        {
            if (File.Exists(OldFile)) File.Delete(OldFile);
            File.Move(exe, OldFile);
            File.Move(NewFile, exe);
            Process.Start(exe, "--after-update").Dispose();
            return true;
        }
        catch (Exception)
        {
            if (!File.Exists(exe) && File.Exists(OldFile)) File.Move(OldFile, exe);
            return false;
        }
    }

    static bool HasValidSignature(byte[] data, byte[] signature)
    {
        using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
        {
            rsa.PersistKeyInCsp = false;
            rsa.FromXmlString(UpdateKey.PublicKeyXml);
            return rsa.VerifyData(data, "SHA256", signature);
        }
    }

    static string Sha256Hex(byte[] data)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "");
    }

    static byte[] Download(string fileName, int maxBytes)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288; // TLS 1.3
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(BaseUrl + fileName);
        request.Timeout = TimeoutMs;
        request.ReadWriteTimeout = TimeoutMs;
        request.UserAgent = App.Id + "-updater";
        request.KeepAlive = false;

        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        using (Stream stream = response.GetResponseStream())
        using (MemoryStream buffer = new MemoryStream())
        {
#if !UPDATE_TEST
            if (response.ResponseUri.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException();
#endif
            byte[] chunk = new byte[8192];
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > maxBytes) throw new InvalidDataException();
            }
            return buffer.ToArray();
        }
    }
}
