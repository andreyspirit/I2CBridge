using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using I2CBridge.Framework.Contracts;
using I2CBridge.Framework.Contracts.Devices;

namespace I2CBridge.Devices;

/// <summary>
/// Texas Instruments PCF8574 Remote 8-Bit I/O Expander for I2C Bus Implementation
/// 
/// The PCF8574 is an 8-bit quasi-bidirectional I/O expander designed for 
/// two-line bidirectional bus (I2C) applications with 2.5V to 6V VCC operation.
/// 
/// Key Features:
/// - 8 quasi-bidirectional I/O pins (P0-P7)
/// - Latched outputs with high-current drive capability
/// - Directly drives LEDs without external buffers
/// - At power-on, all I/Os are high (high impedance input)
/// - Operating voltage: 2.5V to 6V
/// - I2C bus compatible (SMBus)
/// - Bus frequency: 100 kHz to 400 kHz
/// - Available packages: DIP16, SOIC16
/// 
/// I2C Address Configuration:
/// The slave address is determined by hardware address pins A2, A1, A0:
/// Binary format: 0100-A2-A1-A0 (fixed base address 0100 with user-configurable bits)
/// Valid address range depends on A2, A1, A0 configuration
/// The actual address must be passed to the constructor by the user.
/// 
/// Pin Configuration (P0-P7):
/// - All pins are quasi-bidirectional I/O with individual control
/// - Pins can function as inputs or outputs without data-direction control signal
/// - At power-on, all I/Os default to high (high impedance input mode)
/// - In input mode: only a current source to VCC is active
/// - In output mode: can drive low or be released high
/// 
/// Data Format:
/// Single byte I2C read/write with bit positions corresponding to pins:
/// Bit 0 = P0, Bit 1 = P1, ... Bit 7 = P7
/// 1 = high (input or released output)
/// 0 = low (driven output)
/// 
/// Reference: Texas Instruments PCF8574 Datasheet
/// </summary>
public class Pcf8574GpioExpander : II2cDevice, IGpioExpander
{
    // Device Constants
    private const int TotalPins = 8;
    private const byte AllPinsHigh = 0xFF;      // All pins released (high impedance input)
    private const byte AllPinsLow = 0x00;       // All pins driven low (output)

    // I2C Address Validation Limits (7-bit addressing)
    private const byte MinI2cAddress = 0x00;
    private const byte MaxI2cAddress = 0x7F;

    private readonly II2cBridge _bridge;
    private readonly ILogger<Pcf8574GpioExpander>? _logger;
    private readonly byte _slaveAddress;
    private readonly string _deviceId;

    // Pin configuration state (0 = output low, 1 = input/released)
    private byte _portState = AllPinsHigh;

    private bool _disposed;
    private bool _initialized;

    /// <summary>
    /// Gets the device ID.
    /// </summary>
    public string DeviceId => _deviceId;

    /// <summary>
    /// Gets the I2C slave address of this device.
    /// </summary>
    public byte SlaveAddress => _slaveAddress;

    /// <summary>
    /// Gets the device type name.
    /// </summary>
    public string DeviceType => "PCF8574 GPIO Expander";

    /// <summary>
    /// Gets the number of GPIO pins (always 8 for PCF8574).
    /// </summary>
    public int PinCount => TotalPins;

    /// <summary>
    /// Initializes a new instance of the Pcf8574GpioExpander class.
    /// </summary>
    /// <param name="deviceId">Unique identifier for this device instance.</param>
    /// <param name="bridge">The I2C bridge used for communication.</param>
    /// <param name="slaveAddress">The I2C slave address configured via hardware address pins A2, A1, A0.
    /// The actual address is determined by: 0100-A2-A1-A0 (where 0100 is the fixed base address).
    /// Users are responsible for providing the correct address based on their hardware configuration.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentException">Thrown if deviceId is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if bridge is null.</exception>
    /// <exception cref="ArgumentException">Thrown if slaveAddress is invalid (outside 7-bit I2C range 0x00-0x7F).</exception>
    public Pcf8574GpioExpander(
        string deviceId,
        II2cBridge bridge,
        byte slaveAddress,
        ILogger<Pcf8574GpioExpander>? logger = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId, nameof(deviceId));
        ArgumentNullException.ThrowIfNull(bridge, nameof(bridge));
        ValidateSlaveAddress(slaveAddress);

        _deviceId = deviceId;
        _bridge = bridge;
        _slaveAddress = slaveAddress;
        _logger = logger;

        _logger?.LogInformation(
            "Pcf8574GpioExpander instance created: ID={deviceId}, SlaveAddress=0x{slaveAddress:X2}",
            deviceId, slaveAddress);
    }

    /// <summary>
    /// Initializes the GPIO expander device for communication.
    /// Sets all pins to input mode (high impedance) by default, matching the power-on state.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ObjectDisposedException">Thrown if device is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device initialization fails.</exception>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_initialized)
        {
            _logger?.LogWarning("GPIO expander {deviceId} is already initialized", _deviceId);
            return;
        }

        try
        {
            _logger?.LogInformation(
                "Initializing GPIO expander {deviceId} at address 0x{slaveAddress:X2}",
                _deviceId, _slaveAddress);

            // Initialize all pins to input mode (high impedance) - matches power-on state
            _portState = AllPinsHigh;
            await WritePortAsync(_portState, cancellationToken).ConfigureAwait(false);

            _initialized = true;
            _logger?.LogInformation("GPIO expander {deviceId} initialized successfully", _deviceId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize GPIO expander {deviceId}", _deviceId);
            throw new InvalidOperationException(
                $"Failed to initialize GPIO expander device {_deviceId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sets a GPIO pin to high or low.
    /// </summary>
    /// <param name="pin">The pin number (0-7, corresponding to P0-P7).</param>
    /// <param name="value">True to set high (release/input mode), false to set low (drive output).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ObjectDisposedException">Thrown if device is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device is not initialized or operation fails.</exception>
    /// <exception cref="ArgumentException">Thrown if pin number is invalid.</exception>
    public async Task SetPinAsync(int pin, bool value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();
        ValidatePinNumber(pin);

        try
        {
            _logger?.LogDebug(
                "Setting pin P{pin} to {value} on GPIO expander {deviceId}",
                pin, value ? "HIGH" : "LOW", _deviceId);

            // Update port state
            if (value)
            {
                _portState |= (byte)(1 << pin);  // Set bit to 1 (high/released)
            }
            else
            {
                _portState &= (byte)~(1 << pin); // Clear bit to 0 (low/driven)
            }

            // Write updated port state to device
            await WritePortAsync(_portState, cancellationToken).ConfigureAwait(false);

            _logger?.LogDebug("Pin P{pin} set successfully", pin);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set pin {pin} on GPIO expander {deviceId}", pin, _deviceId);
            throw new InvalidOperationException(
                $"Failed to set pin {pin} on GPIO expander {_deviceId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reads the state of a GPIO pin.
    /// </summary>
    /// <param name="pin">The pin number (0-7, corresponding to P0-P7).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if pin is high, false if low.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if device is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device is not initialized or operation fails.</exception>
    /// <exception cref="ArgumentException">Thrown if pin number is invalid.</exception>
    public async Task<bool> GetPinAsync(int pin, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();
        ValidatePinNumber(pin);

        try
        {
            _logger?.LogDebug("Reading pin P{pin} from GPIO expander {deviceId}", pin, _deviceId);

            // Read current port state from device
            byte portState = await ReadPortAsync(cancellationToken).ConfigureAwait(false);

            // Extract bit for requested pin
            bool pinValue = (portState & (1 << pin)) != 0;

            _logger?.LogDebug("Pin P{pin} state: {value}", pin, pinValue ? "HIGH" : "LOW");

            return pinValue;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read pin P{pin} from GPIO expander {deviceId}", pin, _deviceId);
            throw new InvalidOperationException(
                $"Failed to read pin P{pin} from GPIO expander {_deviceId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sets pin as input (high impedance) or output (driven).
    /// In quasi-bidirectional mode: input releases the pin, output drives it low.
    /// </summary>
    /// <param name="pin">The pin number (0-7, corresponding to P0-P7).</param>
    /// <param name="isInput">True to set as input (high impedance), false to set as output (driven low).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ObjectDisposedException">Thrown if device is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device is not initialized or operation fails.</exception>
    /// <exception cref="ArgumentException">Thrown if pin number is invalid.</exception>
    public async Task SetPinModeAsync(int pin, bool isInput, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();
        ValidatePinNumber(pin);

        try
        {
            _logger?.LogDebug(
                "Setting pin P{pin} mode to {mode} on GPIO expander {deviceId}",
                pin, isInput ? "INPUT" : "OUTPUT", _deviceId);

            // In quasi-bidirectional mode:
            // - Set bit to 1 (released/high impedance) for input mode
            // - Set bit to 0 (driven low) for output mode
            if (isInput)
            {
                _portState |= (byte)(1 << pin);  // Set bit to 1 (input/high impedance)
            }
            else
            {
                _portState &= (byte)~(1 << pin); // Clear bit to 0 (output/driven low)
            }

            await WritePortAsync(_portState, cancellationToken).ConfigureAwait(false);

            _logger?.LogDebug("Pin P{pin} mode set successfully", pin);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex, "Failed to set pin P{pin} mode on GPIO expander {deviceId}", pin, _deviceId);
            throw new InvalidOperationException(
                $"Failed to set pin P{pin} mode on GPIO expander {_deviceId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reads all GPIO pins at once.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Byte with bit values representing pin states (1=high, 0=low).</returns>
    public async Task<byte> ReadPortAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        try
        {
            _logger?.LogDebug("Reading port from GPIO expander {deviceId}", _deviceId);

            var data = await _bridge.ReadFromSlaveAsync(_slaveAddress, 1, cancellationToken).ConfigureAwait(false);

            if (data == null || data.Length != 1)
            {
                throw new InvalidOperationException("Invalid response from GPIO expander");
            }

            byte portState = data[0];
            _logger?.LogDebug("Port state read: 0x{portState:X2}", portState);

            return portState;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read port from GPIO expander {deviceId}", _deviceId);
            throw new InvalidOperationException(
                $"Failed to read port from GPIO expander {_deviceId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Writes to all GPIO pins at once.
    /// </summary>
    /// <param name="portValue">Byte with bit values for pins (1=high, 0=low).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task WritePortAsync(byte portValue, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Writing port to GPIO expander {deviceId}: 0x{portValue:X2}", _deviceId, portValue);

            await _bridge.WriteToSlaveAsync(_slaveAddress, new[] { portValue }, cancellationToken).ConfigureAwait(false);

            _portState = portValue;
            _logger?.LogDebug("Port write completed");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to write port to GPIO expander {deviceId}", _deviceId);
            throw new InvalidOperationException(
                $"Failed to write port to GPIO expander {_deviceId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sets multiple pins simultaneously using a bitmask.
    /// </summary>
    /// <param name="pinMask">Bitmask indicating which pins to modify (1=modify, 0=leave unchanged).</param>
    /// <param name="value">The byte value to write to masked pins.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SetMultiplePinsAsync(byte pinMask, byte value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        try
        {
            _logger?.LogDebug(
                "Setting multiple pins on GPIO expander {deviceId}: mask=0x{mask:X2}, value=0x{value:X2}",
                _deviceId, pinMask, value);

            // Update only the masked bits
            _portState = (byte)((_portState & ~pinMask) | (value & pinMask));

            await WritePortAsync(_portState, cancellationToken).ConfigureAwait(false);

            _logger?.LogDebug("Multiple pins set successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set multiple pins on GPIO expander {deviceId}", _deviceId);
            throw;
        }
    }

    /// <summary>
    /// Gets all GPIO pin states at once.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Array of booleans representing pin states (true=high, false=low) for pins P0-P7.</returns>
    public async Task<bool[]> GetAllPinsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        try
        {
            byte portState = await ReadPortAsync(cancellationToken).ConfigureAwait(false);
            var pins = new bool[TotalPins];

            for (int i = 0; i < TotalPins; i++)
            {
                pins[i] = (portState & (1 << i)) != 0;
            }

            return pins;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get all pins from GPIO expander {deviceId}", _deviceId);
            throw;
        }
    }

    /// <summary>
    /// Releases all resources associated with the device.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _logger?.LogInformation("Disposing GPIO expander device {deviceId}", _deviceId);

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Validates the I2C slave address is within valid 7-bit range (0x00-0x7F).
    /// The actual PCF8574 address is configured via hardware address pins A2, A1, A0.
    /// Users are responsible for providing the correct address based on their hardware configuration.
    /// </summary>
    private void ValidateSlaveAddress(byte address)
    {
        if (address < MinI2cAddress || address > MaxI2cAddress)
        {
            throw new ArgumentException(
                $"Invalid I2C slave address 0x{address:X2}. Must be within 7-bit I2C address range (0x00-0x7F). " +
                $"The actual PCF8574 address is determined by hardware configuration of address pins A2, A1, A0 " +
                $"according to the formula: 0100-A2-A1-A0",
                nameof(address));
        }
    }

    /// <summary>
    /// Validates pin number is within valid range (0-7, corresponding to P0-P7).
    /// </summary>
    private void ValidatePinNumber(int pin)
    {
        if (pin < 0 || pin >= TotalPins)
        {
            throw new ArgumentException(
                $"Invalid pin number {pin}. Valid range: 0-{TotalPins - 1} (pins P0-P7)",
                nameof(pin));
        }
    }

    /// <summary>
    /// Ensures the device is initialized before operations.
    /// </summary>
    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                $"GPIO expander {_deviceId} is not initialized. Call InitializeAsync() first.");
        }
    }

    /// <summary>
    /// Throws ObjectDisposedException if the device has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name, $"GPIO expander {_deviceId} has been disposed");
        }
    }
}