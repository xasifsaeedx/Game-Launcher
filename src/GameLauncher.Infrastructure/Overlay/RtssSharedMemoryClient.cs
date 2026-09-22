using System.IO.MemoryMappedFiles;
using System.Text;

namespace GameLauncher.Infrastructure.Overlay;

internal sealed class RtssSharedMemoryClient : IDisposable
{
    private const string MappingName = "RTSSSharedMemoryV2";
    private const uint Signature = 0x52545353;
    private const string Owner = "GameLauncher";
    private const uint ExtendedOsdEntrySize = 4608;
    private const uint VersionWithForegroundProcessId = 0x00020010;

    private readonly MemoryMappedFile _mapping;
    private readonly MemoryMappedViewAccessor _view;
    private int? _slot;

    private RtssSharedMemoryClient(
        MemoryMappedFile mapping,
        MemoryMappedViewAccessor view)
    {
        _mapping = mapping;
        _view = view;
    }

    public static bool IsAvailable()
    {
        try
        {
            using var mapping = MemoryMappedFile.OpenExisting(MappingName);
            using var view = mapping.CreateViewAccessor();
            return view.ReadUInt32(0) == Signature &&
                   (view.ReadUInt32(4) & 0xFFFF0000u) == 0x00020000u;
        }
        catch
        {
            return false;
        }
    }

    public static RtssSharedMemoryClient Open()
    {
        var mapping = MemoryMappedFile.OpenExisting(MappingName);
        var view = mapping.CreateViewAccessor();

        if (view.ReadUInt32(0) != Signature ||
            (view.ReadUInt32(4) & 0xFFFF0000u) != 0x00020000u)
        {
            view.Dispose();
            mapping.Dispose();
            throw new InvalidOperationException("Unsupported or unavailable RTSS shared memory.");
        }

        return new RtssSharedMemoryClient(mapping, view);
    }

    public double? ReadFramesPerSecond(int processId)
    {
        var appEntrySize = _view.ReadUInt32(8);
        var appArrayOffset = _view.ReadUInt32(12);
        var appArraySize = _view.ReadUInt32(16);

        if (appEntrySize < 284 || appArraySize == 0) return null;

        var direct = ReadFramesPerSecondForPid(
            (uint)processId,
            appEntrySize,
            appArrayOffset,
            appArraySize);

        if (direct.Found)
        {
            return direct.FramesPerSecond;
        }

        // Some launchers hand off rendering to a child process. RTSS publishes the
        // most recent foreground 3D application's PID in v2.16+ shared memory, so
        // use it only when the tracked PID has no RTSS application entry at all.
        var version = _view.ReadUInt32(4);
        if (version >= VersionWithForegroundProcessId)
        {
            var foregroundPid = _view.ReadUInt32(68);
            if (foregroundPid != 0 && foregroundPid != (uint)processId)
            {
                var foreground = ReadFramesPerSecondForPid(
                    foregroundPid,
                    appEntrySize,
                    appArrayOffset,
                    appArraySize);

                if (foreground.Found)
                {
                    return foreground.FramesPerSecond;
                }
            }
        }

        return null;
    }

    public bool WriteOverlay(string text)
    {
        var entrySize = _view.ReadUInt32(20);
        var arrayOffset = _view.ReadUInt32(24);
        var arraySize = _view.ReadUInt32(28);

        if (entrySize < 512 || arraySize <= 1) return false;

        var slot = FindOrClaimSlot(entrySize, arrayOffset, arraySize);
        if (!slot.HasValue) return false;

        var offset = arrayOffset + (long)slot.Value * entrySize;

        // RTSS OSD entries expose either the legacy 256-byte text field or,
        // in newer layouts, a 4096-byte extended field. Writing the same text
        // into both causes duplicate telemetry lines in some RTSS versions.
        if (entrySize >= ExtendedOsdEntrySize)
        {
            WriteString(offset, 256, string.Empty);
            WriteString(offset + 512, 4096, text);
        }
        else
        {
            WriteString(offset, 256, text);
        }

        WriteString(offset + 256, 256, Owner);
        _view.Write(32, _view.ReadUInt32(32) + 1);
        return true;
    }

    public void ClearOverlay()
    {
        if (!_slot.HasValue) return;

        try
        {
            var entrySize = _view.ReadUInt32(20);
            var arrayOffset = _view.ReadUInt32(24);
            var offset = arrayOffset + (long)_slot.Value * entrySize;

            if (!string.Equals(ReadString(offset + 256, 256), Owner, StringComparison.Ordinal))
            {
                return;
            }

            WriteString(offset, 256, string.Empty);
            WriteString(offset + 256, 256, string.Empty);
            if (entrySize >= ExtendedOsdEntrySize)
            {
                WriteString(offset + 512, 4096, string.Empty);
            }

            _view.Write(32, _view.ReadUInt32(32) + 1);
        }
        catch
        {
            // Best-effort OSD cleanup.
        }
        finally
        {
            _slot = null;
        }
    }

    public void Dispose()
    {
        ClearOverlay();
        _view.Dispose();
        _mapping.Dispose();
    }

    private (bool Found, double? FramesPerSecond) ReadFramesPerSecondForPid(
        uint processId,
        uint appEntrySize,
        uint appArrayOffset,
        uint appArraySize)
    {
        for (var index = 0u; index < appArraySize; index++)
        {
            var offset = appArrayOffset + (long)index * appEntrySize;
            if (_view.ReadUInt32(offset) != processId) continue;

            var time0 = _view.ReadUInt32(offset + 268);
            var time1 = _view.ReadUInt32(offset + 272);
            var frames = _view.ReadUInt32(offset + 276);
            var frameTimeMicroseconds = _view.ReadUInt32(offset + 280);

            if (frameTimeMicroseconds > 0)
            {
                return (true, 1_000_000d / frameTimeMicroseconds);
            }

            if (time1 > time0 && frames > 0)
            {
                return (true, 1000d * frames / (time1 - time0));
            }

            return (true, null);
        }

        return (false, null);
    }

    private int? FindOrClaimSlot(uint entrySize, uint arrayOffset, uint arraySize)
    {
        if (_slot.HasValue) return _slot;

        int? firstEmpty = null;
        for (var index = 1; index < arraySize; index++)
        {
            var offset = arrayOffset + (long)index * entrySize;
            var owner = ReadString(offset + 256, 256);

            if (string.Equals(owner, Owner, StringComparison.Ordinal))
            {
                _slot = (int)index;
                return _slot;
            }

            if (!firstEmpty.HasValue && owner.Length == 0)
            {
                firstEmpty = (int)index;
            }
        }

        _slot = firstEmpty;
        return _slot;
    }

    private string ReadString(long offset, int maxLength)
    {
        var buffer = new byte[maxLength];
        _view.ReadArray(offset, buffer, 0, buffer.Length);
        var length = Array.IndexOf(buffer, (byte)0);
        if (length < 0) length = buffer.Length;
        return Encoding.Default.GetString(buffer, 0, length);
    }

    private void WriteString(long offset, int maxLength, string value)
    {
        var buffer = new byte[maxLength];
        var encoded = Encoding.Default.GetBytes(value ?? string.Empty);
        Array.Copy(encoded, buffer, Math.Min(encoded.Length, maxLength - 1));
        _view.WriteArray(offset, buffer, 0, buffer.Length);
    }
}
