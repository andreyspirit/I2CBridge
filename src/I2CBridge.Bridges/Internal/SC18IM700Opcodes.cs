using System.Diagnostics.CodeAnalysis;

namespace I2CBridge.Bridges.Internal;

/// <summary>
/// SC18IM700 protocol operation codes and constants.
/// Based on SC18IM700 UART to I2C Controller Datasheet.
/// 
/// The SC18IM700 uses ASCII-based command protocol:
/// - I2C write: [S][SlaveAddr][Length][Data1...DataN][P]
/// - I2C read:  [S][SlaveAddr][Length][P]
/// - Register access: [R/W][RegisterAddr][Value?]
/// - GPIO control: [I/O][GPIOValue?]
/// </summary>
[SuppressMessage("Design", "CA1052:Static holder types should be Static or NotInheritable")]
public static class SC18IM700Opcodes
{
    /// <summary>
    /// I2C frame control opcodes (ASCII characters).
    /// Fundamental protocol commands for I2C communication.
    /// </summary>
    public static class FrameControl
    {
        /// <summary>
        /// I2C START condition (0x53 = ASCII 'S').
        /// Initiates an I2C transaction frame.
        /// </summary>
        public const byte Start = 0x53;

        /// <summary>
        /// I2C STOP condition (0x50 = ASCII 'P').
        /// Terminates an I2C transaction frame.
        /// </summary>
        public const byte Stop = 0x50;
    }

    /// <summary>
    /// Register access opcodes for SC18IM700 internal configuration.
    /// Allows reading/writing device registers for baud rate, I2C clock, etc.
    /// </summary>
    public static class RegisterAccess
    {
        /// <summary>
        /// Read register opcode (0x52 = ASCII 'R').
        /// Command: [R][RegisterAddress]
        /// Response: [Status][RegisterValue]
        /// </summary>
        public const byte Read = 0x52;

        /// <summary>
        /// Write register opcode (0x57 = ASCII 'W').
        /// Command: [W][RegisterAddress][RegisterValue]
        /// Response: [Status]
        /// </summary>
        public const byte Write = 0x57;
    }

    /// <summary>
    /// GPIO operation opcodes for controlling device I/O pins.
    /// </summary>
    public static class GpioControl
    {
        /// <summary>
        /// Read GPIO opcode (0x49 = ASCII 'I').
        /// Command: [I]
        /// Response: [Status][GPIOState]
        /// </summary>
        public const byte Read = 0x49;

        /// <summary>
        /// Write GPIO opcode (0x4F = ASCII 'O').
        /// Command: [O][GPIOValue]
        /// Response: [Status]
        /// </summary>
        public const byte Write = 0x4F;
    }

    /// <summary>
    /// Power management opcodes.
    /// </summary>
    public static class PowerControl
    {
        /// <summary>
        /// Power down opcode (0x5A = ASCII 'Z').
        /// Command: [Z]
        /// Response: [Status]
        /// </summary>
        public const byte PowerDown = 0x5A;
    }

    /// <summary>
    /// SC18IM700 internal registers for device configuration.
    /// Register address mapping for configuration operations.
    /// </summary>
    public static class Registers
    {
        /// <summary>
        /// Baud Rate Generator (low byte).
        /// Sets lower 8 bits of baud rate divisor.
        /// </summary>
        public const byte BRG0 = 0x00;

        /// <summary>
        /// Baud Rate Generator (high byte).
        /// Sets upper 8 bits of baud rate divisor.
        /// </summary>
        public const byte BRG1 = 0x01;

        /// <summary>
        /// Port Configuration Register.
        /// Configures I/O port behavior and GPIO modes.
        /// </summary>
        public const byte PortConfiguration = 0x02;

        /// <summary>
        /// I2C Clock Register.
        /// Sets I2C bus clock frequency (standard/fast mode).
        /// </summary>
        public const byte I2cClockDivisor = 0x03;

        /// <summary>
        /// I/O Port Pin Configuration.
        /// Configures which pins are GPIO vs I2C.
        /// </summary>
        public const byte IoPinConfiguration = 0x04;

        /// <summary>
        /// Device Status Register.
        /// Contains device state and error information.
        /// </summary>
        public const byte DeviceStatus = 0x05;

        /// <summary>
        /// I2C Bus Status Register.
        /// Contains I2C bus state and transaction results.
        /// </summary>
        public const byte I2cStatus = 0x06;
    }

    /// <summary>
    /// SC18IM700 response status codes.
    /// Device returns these codes to indicate operation success or failure.
    /// </summary>
    public static class Status
    {
        /// <summary>
        /// Success (0xF0).
        /// Operation completed without errors.
        /// </summary>
        public const byte OK = 0xF0;

        /// <summary>
        /// No ACK on slave address (0xF1).
        /// I2C slave did not acknowledge its address.
        /// </summary>
        public const byte NACK_ON_ADDRESS = 0xF1;

        /// <summary>
        /// No ACK on data byte (0xF2).
        /// I2C slave did not acknowledge sent data byte.
        /// </summary>
        public const byte NACK_ON_DATA = 0xF2;

        /// <summary>
        /// I2C bus error (0xF3).
        /// Collision or other bus error occurred.
        /// </summary>
        public const byte BUS_ERROR = 0xF3;

        /// <summary>
        /// Timeout (0xF4).
        /// I2C operation timed out waiting for bus condition.
        /// </summary>
        public const byte TIMEOUT = 0xF4;
    }

    /// <summary>
    /// I2C addressing and limits.
    /// </summary>
    public static class I2cLimits
    {
        /// <summary>
        /// Maximum payload per I2C frame (255 bytes).
        /// </summary>
        public const int MaxPayloadLength = 255;

        /// <summary>
        /// Minimum valid 7-bit I2C slave address (0x08).
        /// </summary>
        public const byte MinSlaveAddress = 0x08;

        /// <summary>
        /// Maximum valid 7-bit I2C slave address (0x77).
        /// </summary>
        public const byte MaxSlaveAddress = 0x77;

        /// <summary>
        /// Standard I2C clock (100 kHz).
        /// </summary>
        public const int StandardClockFrequency = 100000;

        /// <summary>
        /// Fast-mode I2C clock (400 kHz).
        /// </summary>
        public const int FastClockFrequency = 400000;
    }

    /// <summary>
    /// Baud rate configuration values.
    /// Maps common baud rates to SC18IM700 divisor values.
    /// Formula: BaudRate = 115200 / Divisor
    /// </summary>
    public static class BaudRates
    {
        /// <summary>1200 baud (divisor 96).</summary>
        public const ushort Rate1200 = 96;

        /// <summary>2400 baud (divisor 48).</summary>
        public const ushort Rate2400 = 48;

        /// <summary>4800 baud (divisor 24).</summary>
        public const ushort Rate4800 = 24;

        /// <summary>9600 baud (divisor 12).</summary>
        public const ushort Rate9600 = 12;

        /// <summary>19200 baud (divisor 6).</summary>
        public const ushort Rate19200 = 6;

        /// <summary>38400 baud (divisor 3).</summary>
        public const ushort Rate38400 = 3;

        /// <summary>57600 baud (divisor 2).</summary>
        public const ushort Rate57600 = 2;

        /// <summary>115200 baud (divisor 1).</summary>
        public const ushort Rate115200 = 1;
    }

    /// <summary>
    /// I2C clock frequency configuration values.
    /// </summary>
    public static class I2cClockDivisors
    {
        /// <summary>100 kHz I2C clock (standard mode).</summary>
        public const byte StandardMode = 0x04;

        /// <summary>400 kHz I2C clock (fast mode).</summary>
        public const byte FastMode = 0x01;
    }

    /// <summary>
    /// GPIO port configuration bit masks.
    /// </summary>
    public static class GpioPorts
    {
        /// <summary>GPIO port 0 (IO0).</summary>
        public const byte Port0 = 0x01;

        /// <summary>GPIO port 1 (IO1).</summary>
        public const byte Port1 = 0x02;

        /// <summary>GPIO port 2 (IO2).</summary>
        public const byte Port2 = 0x04;

        /// <summary>GPIO port 3 (IO3).</summary>
        public const byte Port3 = 0x08;

        /// <summary>All GPIO ports (IO0-IO3).</summary>
        public const byte AllPorts = Port0 | Port1 | Port2 | Port3;
    }
}