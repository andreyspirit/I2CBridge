using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace I2CBridge.Bridges.Internal;

/// <summary>
/// SC18IM700 protocol implementation for frame construction, parsing, and device communication.
/// 
/// Handles:
/// - I2C write frames: [S][SlaveAddr][Length][Data...][P]
/// - I2C read frames: [S][SlaveAddr][Length][P]
/// - Register read/write operations
/// - GPIO control operations
/// - Status code interpretation
/// </summary>
public class SC18IM700Protocol
{
    private readonly ILogger<SC18IM700Protocol>? _logger;

    /// <summary>
    /// Initializes a new instance of the SC18IM700Protocol class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public SC18IM700Protocol(ILogger<SC18IM700Protocol>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Constructs an I2C write frame for transmitting data to an I2C slave.
    /// </summary>
    /// <param name="slaveAddress">The 7-bit I2C slave address.</param>
    /// <param name="data">The data payload to send.</param>
    /// <returns>The complete write frame: [S][SlaveAddr][Length][Data...][P]</returns>
    /// <exception cref="ArgumentException">Thrown if parameters are invalid.</exception>
    public byte[] BuildI2cWriteFrame(byte slaveAddress, ReadOnlyMemory<byte> data)
    {
        ValidateSlaveAddress(slaveAddress);

        if (data.Length == 0)
        {
            throw new ArgumentException("Data cannot be empty.", nameof(data));
        }

        if (data.Length > SC18IM700Opcodes.I2cLimits.MaxPayloadLength)
        {
            throw new ArgumentException(
                $"Data length cannot exceed {SC18IM700Opcodes.I2cLimits.MaxPayloadLength} bytes.",
                nameof(data));
        }

        // Frame: [S] [SlaveAddr] [Length] [Data...] [P]
        var frame = new byte[4 + data.Length];

        frame[0] = SC18IM700Opcodes.FrameControl.Start;
        frame[1] = slaveAddress;
        frame[2] = (byte)data.Length;
        data.Span.CopyTo(frame.AsSpan(3, data.Length));
        frame[frame.Length - 1] = SC18IM700Opcodes.FrameControl.Stop;

        _logger?.LogTrace(
            "I2C write frame constructed: SlaveAddr=0x{slaveAddr:X2}, DataLen={dataLen}, Frame={frameHex}",
            slaveAddress, data.Length, FormatFrameAsHex(frame));

        return frame;
    }

    /// <summary>
    /// Constructs an I2C read frame for receiving data from an I2C slave.
    /// </summary>
    /// <param name="slaveAddress">The 7-bit I2C slave address.</param>
    /// <param name="bytesToRead">The number of bytes to read.</param>
    /// <returns>The complete read frame: [S][SlaveAddr][Length][P]</returns>
    /// <exception cref="ArgumentException">Thrown if parameters are invalid.</exception>
    public byte[] BuildI2cReadFrame(byte slaveAddress, int bytesToRead)
    {
        ValidateSlaveAddress(slaveAddress);

        if (bytesToRead <= 0)
        {
            throw new ArgumentException("Bytes to read must be greater than 0.", nameof(bytesToRead));
        }

        if (bytesToRead > SC18IM700Opcodes.I2cLimits.MaxPayloadLength)
        {
            throw new ArgumentException(
                $"Read length cannot exceed {SC18IM700Opcodes.I2cLimits.MaxPayloadLength} bytes.",
                nameof(bytesToRead));
        }

        // Frame: [S] [SlaveAddr] [Length] [P]
        var frame = new byte[4];

        frame[0] = SC18IM700Opcodes.FrameControl.Start;
        frame[1] = slaveAddress;
        frame[2] = (byte)bytesToRead;
        frame[3] = SC18IM700Opcodes.FrameControl.Stop;

        _logger?.LogTrace(
            "I2C read frame constructed: SlaveAddr=0x{slaveAddr:X2}, Length={length}, Frame={frameHex}",
            slaveAddress, bytesToRead, FormatFrameAsHex(frame));

        return frame;
    }

    /// <summary>
    /// Constructs a register write command.
    /// </summary>
    /// <param name="register">The register address to write.</param>
    /// <param name="value">The value to write.</param>
    /// <returns>The register write command: [W][RegisterAddr][Value]</returns>
    public byte[] BuildRegisterWriteFrame(byte register, byte value)
    {
        var frame = new byte[3];
        frame[0] = SC18IM700Opcodes.RegisterAccess.Write;
        frame[1] = register;
        frame[2] = value;

        _logger?.LogTrace(
            "Register write frame constructed: Reg=0x{reg:X2}, Value=0x{value:X2}",
            register, value);

        return frame;
    }

    /// <summary>
    /// Constructs a register read command.
    /// </summary>
    /// <param name="register">The register address to read.</param>
    /// <returns>The register read command: [R][RegisterAddr]</returns>
    public byte[] BuildRegisterReadFrame(byte register)
    {
        var frame = new byte[2];
        frame[0] = SC18IM700Opcodes.RegisterAccess.Read;
        frame[1] = register;

        _logger?.LogTrace("Register read frame constructed: Reg=0x{reg:X2}", register);

        return frame;
    }

    /// <summary>
    /// Constructs a GPIO read command.
    /// </summary>
    /// <returns>The GPIO read command: [I]</returns>
    public byte[] BuildGpioReadFrame()
    {
        return new[] { SC18IM700Opcodes.GpioControl.Read };
    }

    /// <summary>
    /// Constructs a GPIO write command.
    /// </summary>
    /// <param name="gpioValue">The GPIO port values to write.</param>
    /// <returns>The GPIO write command: [O][GPIOValue]</returns>
    public byte[] BuildGpioWriteFrame(byte gpioValue)
    {
        var frame = new byte[2];
        frame[0] = SC18IM700Opcodes.GpioControl.Write;
        frame[1] = gpioValue;

        _logger?.LogTrace("GPIO write frame constructed: GPIOValue=0x{value:X2}", gpioValue);

        return frame;
    }

    /// <summary>
    /// Attempts to parse an I2C response from the device.
    /// </summary>
    /// <param name="response">The response buffer.</param>
    /// <param name="expectedDataLength">Expected data length for validation.</param>
    /// <param name="status">The status byte from response.</param>
    /// <param name="data">The extracted data (if successful).</param>
    /// <returns>True if response was successfully parsed; false otherwise.</returns>
    public bool TryParseI2cResponse(ReadOnlyMemory<byte> response, int expectedDataLength, out byte status, [NotNullWhen(true)] out byte[]? data)
    {
        data = null;

        if (response.Length < 1)
        {
            _logger?.LogError("Invalid I2C response: insufficient data");
            status = SC18IM700Opcodes.Status.OK;
            return false;
        }

        status = response.Span[0];

        if (!IsStatusOk(status))
        {
            _logger?.LogWarning("I2C operation failed: {statusDescription}", GetStatusDescription(status));
            return false;
        }

        // Extract data (skip status byte)
        int dataLength = Math.Min(expectedDataLength, response.Length - 1);
        if (dataLength > 0)
        {
            data = new byte[dataLength];
            response.Span.Slice(1, dataLength).CopyTo(data);
        }
        else
        {
            data = Array.Empty<byte>();
        }

        return true;
    }

    /// <summary>
    /// Validates a device response status code.
    /// </summary>
    /// <param name="status">The status byte.</param>
    /// <returns>True if status indicates success; false otherwise.</returns>
    public static bool IsStatusOk(byte status)
    {
        return status == SC18IM700Opcodes.Status.OK;
    }

    /// <summary>
    /// Gets a human-readable description of a status code.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <returns>Descriptive string for the status code.</returns>
    public static string GetStatusDescription(byte status)
    {
        return status switch
        {
            SC18IM700Opcodes.Status.OK => "OK (0xF0) - Operation successful",
            SC18IM700Opcodes.Status.NACK_ON_ADDRESS => "NACK on Address (0xF1) - Slave did not acknowledge address",
            SC18IM700Opcodes.Status.NACK_ON_DATA => "NACK on Data (0xF2) - Slave did not acknowledge data",
            SC18IM700Opcodes.Status.BUS_ERROR => "Bus Error (0xF3) - I2C bus collision or error",
            SC18IM700Opcodes.Status.TIMEOUT => "Timeout (0xF4) - I2C operation timeout",
            _ => $"Unknown Status (0x{status:X2})"
        };
    }

    /// <summary>
    /// Validates I2C slave address (7-bit addressing, 0x08-0x77 valid range).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if address is invalid.</exception>
    private void ValidateSlaveAddress(byte slaveAddress)
    {
        if (slaveAddress < SC18IM700Opcodes.I2cLimits.MinSlaveAddress ||
            slaveAddress > SC18IM700Opcodes.I2cLimits.MaxSlaveAddress)
        {
            throw new ArgumentException(
                $"Invalid I2C slave address 0x{slaveAddress:X2}. Must be between 0x{SC18IM700Opcodes.I2cLimits.MinSlaveAddress:X2} and 0x{SC18IM700Opcodes.I2cLimits.MaxSlaveAddress:X2}.",
                nameof(slaveAddress));
        }
    }

    /// <summary>
    /// Formats a byte array as a hexadecimal string for logging.
    /// </summary>
    private static string FormatFrameAsHex(byte[] frame)
    {
        return $"[{string.Join(", ", System.Linq.Enumerable.Select(frame, b => $"0x{b:X2}"))}]";
    }
}