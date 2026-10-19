using System;
using System.IO;

namespace UniversalSqueaker;

/// <summary>Same-directory staging: a failed serializer never truncates the live Config file.</summary>
internal static class UsAtomicConfigFile
{
    internal static bool TryWrite(string target, Action<string> serialize, out string failure, Action? beforeCommit = null)
    {
        string staging = target + ".us-source-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            serialize(staging);
            // The serializer has closed its stream. Flush staged bytes before the filesystem commit.
            using (var stream = new FileStream(staging, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) stream.Flush(true);
            beforeCommit?.Invoke();
            if (File.Exists(target)) File.Replace(staging, target, null);
            else File.Move(staging, target);
            failure = "";
            return true;
        }
        catch (Exception error)
        {
            // Retain a failed staged file for diagnosis; it is never consumed as settings.
            failure = error.GetType().Name + ":" + error.Message + "; staged candidate=" + staging;
            return false;
        }
    }
}
