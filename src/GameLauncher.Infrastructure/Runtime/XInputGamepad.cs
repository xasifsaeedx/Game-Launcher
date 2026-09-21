using System.Runtime.InteropServices;

namespace GameLauncher.Infrastructure.Runtime;

[Flags]
public enum GamepadButtons : ushort
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000
}

public sealed class XInputGamepad
{
    private uint _lastPacketNumber;
    private GamepadButtons _lastButtons;

    public bool IsConnected { get; private set; }

    public GamepadButtons PollPressedButtons()
    {
        if (!OperatingSystem.IsWindows()) return GamepadButtons.None;

        try
        {
            var result = XInputGetState(0, out var state);
            IsConnected = result == 0;

            if (!IsConnected)
            {
                _lastButtons = GamepadButtons.None;
                return GamepadButtons.None;
            }

            var current = (GamepadButtons)state.Gamepad.Buttons;
            var pressed = current & ~_lastButtons;
            _lastButtons = current;
            _lastPacketNumber = state.PacketNumber;
            return pressed;
        }
        catch (DllNotFoundException)
        {
            IsConnected = false;
            return GamepadButtons.None;
        }
        catch (EntryPointNotFoundException)
        {
            IsConnected = false;
            return GamepadButtons.None;
        }
    }

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepadState Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepadState
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }
}
