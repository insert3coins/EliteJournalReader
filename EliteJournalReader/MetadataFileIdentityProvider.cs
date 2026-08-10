using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EliteJournalReader
{
    /// <summary>
    /// Documented metadata fallback implementation of <see cref="IFileIdentityProvider"/>.
    /// Used on platforms where volume/file identity from the open handle is unavailable.
    /// 
    /// Linux uses the filesystem device and inode from stat(2), which remains stable while a
    /// file grows and changes when a file is replaced. Other unsupported platforms use the
    /// file's creation time (as ticks) combined with a hash of the full file path to
    /// approximate stable identity.
    /// 
    /// Metadata-fallback limitations:
    /// - Systems with low-resolution file timestamps may not detect rapid replace cycles.
    /// - If creation time is not preserved by the filesystem, the metadata fallback may produce
    ///   false positives.
    /// - Unlike the Windows provider, this cannot distinguish hardlinks or detect renames.
    /// For the metadata fallback, these limitations are acceptable because the primary detection
    /// is length-based (truncation) and replacement almost always changes creation time.
    /// </summary>
    internal sealed class MetadataFileIdentityProvider : IFileIdentityProvider
    {
        public FileIdentity? GetIdentity(FileStream stream)
        {
            if (stream == null || string.IsNullOrEmpty(stream.Name))
                return null;

            if (OperatingSystem.IsLinux())
                return GetLinuxIdentity(stream.SafeFileHandle);

            return GetIdentity(stream.Name);
        }

        public FileIdentity? GetIdentity(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            try
            {
                if (!File.Exists(filePath))
                    return null;

                if (OperatingSystem.IsLinux())
                    return GetLinuxIdentity(filePath);

                return GetMetadataIdentity(filePath);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to get file metadata identity for '{filePath}': {ex.Message}");
                Trace.TraceInformation(ex.StackTrace);
                return null;
            }
        }

        private static FileIdentity? GetMetadataIdentity(string filePath)
        {
            var info = new FileInfo(filePath);
            // Use creation time ticks as the "volume" component (approximation)
            long creationTicks = info.CreationTimeUtc.Ticks;
            // Use a stable hash of the full path as the "file index" component
            long pathHash = GetStablePathHash(info.FullName);

            return new FileIdentity(creationTicks, pathHash);
        }

        private static FileIdentity? GetLinuxIdentity(SafeFileHandle handle)
        {
            if (handle == null || handle.IsInvalid)
                return null;

            try
            {
                int fileDescriptor = handle.DangerousGetHandle().ToInt32();
                return FStat(fileDescriptor, out var status) == 0
                    ? CreateLinuxIdentity(status)
                    : null;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to get Linux file identity from handle: {ex.Message}");
                Trace.TraceInformation(ex.StackTrace);
                return null;
            }
        }

        private static FileIdentity? GetLinuxIdentity(string filePath)
        {
            try
            {
                return Stat(filePath, out var status) == 0
                    ? CreateLinuxIdentity(status)
                    : null;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to get Linux file identity for '{filePath}': {ex.Message}");
                Trace.TraceInformation(ex.StackTrace);
                return null;
            }
        }

        private static FileIdentity CreateLinuxIdentity(LinuxFileStatus status) =>
            new FileIdentity(unchecked((long)status.Device), unchecked((long)status.Inode));

        [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
        private static extern int FStat(int fileDescriptor, out LinuxFileStatus status);

        [DllImport("libc", EntryPoint = "stat", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern int Stat(string path, out LinuxFileStatus status);

        [StructLayout(LayoutKind.Sequential)]
        private struct LinuxFileStatus
        {
            public ulong Device;
            public ulong Inode;
            private ulong LinkCount;
            private uint Mode;
            private uint UserId;
            private uint GroupId;
            private int Padding;
            private ulong SpecialDevice;
            private long Size;
            private long BlockSize;
            private long Blocks;
            private long AccessTime;
            private long AccessTimeNanoseconds;
            private long ModificationTime;
            private long ModificationTimeNanoseconds;
            private long StatusChangeTime;
            private long StatusChangeTimeNanoseconds;
            private ulong Reserved0;
            private ulong Reserved1;
            private ulong Reserved2;
        }

        /// <summary>
        /// Produces a stable hash for the given path using case-insensitive comparison
        /// to match typical Windows filesystem behavior.
        /// </summary>
        private static long GetStablePathHash(string fullPath)
        {
            // Use ordinal ignore-case hash for Windows-style paths
            return (long)fullPath.GetHashCode(StringComparison.OrdinalIgnoreCase);
        }
    }
}
