using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using I2CBridge.Framework.Contracts;
using I2CBridge.Framework.Contracts.Devices;

namespace I2CBridge.Devices;

/// <summary>
/// Maxim DS3231 Real-Time Clock with Square-Wave Output Implementation
/// 
/// The DS3231 is a low-cost, extremely accurate I2C real-time clock (RTC) with an 
/// integrated temperature-compensated crystal oscillator (TCXO) and crystal.
/// 
/// Key Features:
/// - Extremely accurate I2C RTC
/// - Temperature-compensated crystal oscillator (TCXO)
/// - Integrated crystal resonator
/// - Battery-backed operation for timekeeping during power loss
/// - 24-hour or 12-hour format with AM/PM indicator
/// - Two programmable time-of-day alarms
/// - Programmable square-wave output (signal generator)
/// - Operating voltage: 2.3V to 5.5V
/// - I2C bus interface: 100 kHz to 400 kHz
/// - Available packages: 16-pin, 300-mil SO
/// 
/// I2C Address:
/// Slave address: 0x68 (fixed, no address pins)
/// 
/// Square-Wave Output Frequencies:
/// The SQW/INT pin can generate the following frequencies:
/// - 1 Hz
/// - 1024 Hz (1 kHz)
/// - 4096 Hz (4 kHz)
/// - 8192 Hz (8 kHz)
/// 
/// Register Map:
/// 0x00-0x06: Date/Time registers (seconds, minutes, hours, day, date, month, year)
/// 0x07-0x0A: Control and Status registers
/// 0x0E-0x10: Temperature register
/// 
/// Control Register (0x0E):
/// Bit 7: EOSC - Enable Oscillator
/// Bit 6: BBSQW - Battery-backed SQW
/// Bit 5: CONV - Convert Temperature
/// Bit 4: RS1, Bit 3: RS0 - Rate Select (frequency selection)
/// Bit 2: INTCN - Interrupt Control Enable
/// Bit 1: A2IE - Alarm 2 Interrupt Enable
/// Bit 0: A1IE - Alarm 1 Interrupt Enable
/// 
/// Reference: Maxim DS3231 Datasheet
/// </summary>
public class Ds3231SignalGenerator : II2cDevice, ISignalGenerator
{
    // I2C Configuration
    private const byte Ds3231SlaveAddress = 0x68;  // Fixed I2C address

    // DS3231 Register Addresses
    private const byte RegisterSeconds = 0x00;
    private const byte RegisterMinutes = 0x01;
    private const byte RegisterHours = 0x02;
    private const byte RegisterDay = 0x03;
    private const byte RegisterDate = 0x04;
    private const byte RegisterMonth = 0x05;
    private const byte RegisterYear = 0x06;
    private const byte RegisterControl = 0x0E;
    private const byte RegisterStatus = 0x0F;
    private const byte RegisterTemperature = 0x11;

    // Control Register Bit Masks
    private const byte ControlBitEosc = 0x80;      // Enable Oscillator
    private const byte ControlBitBbsqw = 0x40;     // Battery-backed SQW
    private const byte ControlBitConv = 0x20;      // Convert Temperature
    private const byte ControlBitIntcn = 0x04;     // Interrupt Control Enable
    private const byte ControlBitA2ie = 0x02;      // Alarm 2 Interrupt Enable
    private const byte ControlBitA1ie = 0x01;      // Alarm 1 Interrupt Enable
    private const byte ControlBitRsMask = 0x18;    // Rate Select Mask (bits 4 and 3)

    // Square-Wave Output Frequencies (rate select values)
    private const byte RateSelect1Hz = 0x00;       // RS1=0, RS0=0 -> 1 Hz
    private const byte RateSelect1024Hz = 0x08;    // RS1=0, RS0=1 -> 1024 Hz
    private const byte RateSelect4096Hz = 0x10;    // RS1=1, RS0=0 -> 4096 Hz
    private const byte RateSelect8192Hz = 0x18;    // RS1=1, RS0=1 -> 8192 Hz

    // Supported frequencies in Hz
    private static readonly double[] SupportedFrequencies = { 1.0, 1024.0, 4096.0, 8192.0 };

    private readonly II2cBridge _bridge;
    private readonly ILogger<Ds3231SignalGenerator>? _logger;
    private readonly string _deviceId;

    private double _currentFrequency = 1.0;
    private bool _outputEnabled = false;
    private byte _controlRegisterCache = 0x00;

    private bool _disposed;
    private bool _initialized;

    /// <summary>
    /// Gets the device ID.
    /// </summary>
    public string DeviceId => _deviceId;

    /// <summary>
    /// Gets the I2C slave address (always 0x68 for DS3231).
    /// </summary>
    public byte SlaveAddress => Ds3231SlaveAddress;

    /// <summary>
    /// Gets the device type name.
    /// </summary>
    public string DeviceType => "DS3231 Real-Time Clock Signal Generator";

    /// <summary>
    /// Gets the array of supported frequencies in Hz.
    /// </summary>
    public double[] SupportedFrequenciesHz => SupportedFrequencies;

    /// <summary>
    /// Initializes a new instance of the Ds3231SignalGenerator class.
    /// </summary>
    /// <param name="deviceId">Unique identifier for this device instance.</param>
    /// <param name="bridge">The I2C bridge used for communication.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentException">Thrown if deviceId is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if bridge is null.</exception>
    public Ds3231SignalGenerator(
        string deviceId,
        II2cBridge bridge,
        ILogger<Ds3231SignalGenerator>? logger = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId, nameof(deviceId));
        ArgumentNullException.ThrowIfNull(bridge, nameof(bridge));

        _deviceId = deviceId;
        _bridge = bridge;
        _logger = logger;

        _logger?.LogInformation(
            "Ds3231SignalGenerator instance created: ID={deviceId}, SlaveAddress=0x{slaveAddress:X2}",
            deviceId, Ds3231SlaveAddress);
    }

    /// <summary>
    /// Initializes the DS3231 device for communication.
    /// Reads current control register state and sets up square-wave output.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ObjectDisposedException">Thrown if device is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device initialization fails.</exception>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_initialized)
        {
            _logger?.LogWarning("Signal generator {deviceId} is already initialized", _deviceId);
            return;
        }

        try
        {
            _logger?.LogInformation(
                "Initializing signal generator {deviceId} at address 0x{slaveAddress:X2}",
                _deviceId, Ds3231SlaveAddress);

            // Read current control register to preserve settings
            var controlData = await ReadRegisterAsync(RegisterControl, 1, cancellationToken).ConfigureAwait(false);
            _controlRegisterCache = controlData[0];

            // Enable oscillator (clear EOSC bit)
            _controlRegisterCache &= unchecked((byte)~ControlBitEosc);

            // Write updated control register
            await WriteRegisterAsync(RegisterControl, new[] { _controlRegisterCache }, cancellationToken).ConfigureAwait(false);

            _initialized = true;
            _logger?.LogInformation("Signal generator {deviceId} initialized successfully", _deviceId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize signal generator {deviceId}", _deviceId);
            throw new InvalidOperationException(
                $"Failed to initialize signal generator device {_deviceId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sets the square-wave output frequency.
    /// Supported frequencies: 1 Hz, 1024 Hz, 4096 Hz, 8192 Hz
    /// </summary>
    /// <param name="frequency">The desired frequency in Hz.</param>
    /// <exception cref="ObjectDisposedException">Thrown if device is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device is not initialized or operation fails.</exception>
    /// <exception cref="ArgumentException">Thrown if frequency is not supported.</exception>
    public async Task SetFrequencyAsync(double frequency)
    {
        await SetFrequencyAsync(frequency, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the square-wave output frequency with cancellation support.
    /// Supported frequencies: 1 Hz, 1024 Hz, 4096 Hz, 8192 Hz
    /// </summary>
    /// <param name="frequency">The desired frequency in Hz.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ObjectDisposedException">Thrown if device is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device is not initialized or operation fails.</exception>
    /// <exception cref="ArgumentException">Thrown if frequency is not supported.</exception>
    private async Task SetFrequencyAsync(double frequency, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureInitialized();
        ValidateFrequency(frequency);

        try
        {
            _logger?.LogInformation(
                "Setting square-wave output frequency to {frequency} Hz on signal generator {deviceId}",
                frequency, _deviceId);

            // Determine rate select bits based on frequency
            byte rateSelectBits = GetRateSelectBits(frequency);

            // Clear old rate select bits and set new ones
            _controlRegisterCache = (byte)((_controlRegisterCache & ~ControlBitRsMask) | rateSelectBits);

            // Write updated control register
            await WriteRegisterAsync(RegisterControl, new[] { _controlRegisterCache }, cancellationToken).ConfigureAwait(false);

            _currentFrequency = frequency;

            _logger?.LogInformation(
                "Square-wave output frequency set to {frequency} Hz successfully",
                frequency);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set frequency on signal generator {deviceId}", _deviceId);
            throw new InvalidOperationException(
                $"Failed to set frequency on signal generator {_deviceId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the current square-wave output frequency.
    /// </summary>
    /// <returns>The current frequency in Hz.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if device is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device is not initialized or operation fails.</exception>
    public async Task<double> GetFrequencyAsync()
    {
        return await GetFrequencyAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the current square-wave output frequency with cancellation support.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current frequency in Hz.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if device is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device is not initialized or operation fails.</exception>
    private async Task<double> GetFrequencyAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        try
        {
            _logger?.LogDebug("Reading current frequency from signal generator {deviceId}", _deviceId);

            // Read control register to get current rate select bits
            var controlData = await ReadRegisterAsync(RegisterControl, 1, cancellationToken).ConfigureAwait(false);
            byte controlRegister = controlData[0];

            // Extract rate select bits
            byte rateSelectBits = (byte)(controlRegister & ControlBitRsMask);

            // Convert rate select bits to frequency
            double frequency = rateSelectBits switch
            {
                RateSelect1Hz => 1.0,
                RateSelect1024Hz => 1024.0,
                RateSelect4096Hz => 4096.0,
                RateSelect8192Hz => 8192.0,
                _ => 1.0  // Default fallback
            };

            _currentFrequency = frequency;
            _logger?.LogDebug("Current frequency: {frequency} Hz", frequency);

            return frequency;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read frequency from signal generator {deviceId}", _deviceId);
            throw new InvalidOperationException(
                $"Failed to read frequency from signal generator {_deviceId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Enables or disables the square-wave output.
    /// </summary>
    /// <param name="state">True to enable output, false to disable.</param>
    /// <exception cref="ObjectDisposedException">Thrown if device is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device is not initialized or operation fails.</exception>
    public async Task SetOutputAsync(bool state)
    {
        await SetOutputAsync(state, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Enables or disables the square-wave output with cancellation support.
    /// </summary>
    /// <param name="state">True to enable output, false to disable.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ObjectDisposedException">Thrown if device is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device is not initialized or operation fails.</exception>
    private async Task SetOutputAsync(bool state, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        try
        {
            _logger?.LogInformation(
                "Setting square-wave output to {state} on signal generator {deviceId}",
                state ? "ENABLED" : "DISABLED", _deviceId);

            // INTCN bit controls the output:
            // INTCN = 0: SQW output enabled
            // INTCN = 1: Interrupt output (SQW disabled)
            if (state)
            {
                // Enable SQW output: clear INTCN bit
                _controlRegisterCache &= unchecked((byte)~ControlBitIntcn);
            }
            else
            {
                // Disable SQW output: set INTCN bit
                _controlRegisterCache |= ControlBitIntcn;
            }

            // Write updated control register
            await WriteRegisterAsync(RegisterControl, new[] { _controlRegisterCache }, cancellationToken).ConfigureAwait(false);

            _outputEnabled = state;

            _logger?.LogInformation("Square-wave output set to {state} successfully", state ? "enabled" : "disabled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set output state on signal generator {deviceId}", _deviceId);
            throw new InvalidOperationException(
                $"Failed to set output state on signal generator {_deviceId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reads the current time from the DS3231.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A DateTime object representing the current time.</returns>
    public async Task<DateTime> ReadTimeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        try
        {
            _logger?.LogDebug("Reading time from signal generator {deviceId}", _deviceId);

            // Read time registers (7 bytes: seconds through year)
            var timeData = await ReadRegisterAsync(RegisterSeconds, 7, cancellationToken).ConfigureAwait(false);

            // Extract BCD-encoded values
            int seconds = BcdToDec((byte)(timeData[0] & 0x7F));      // Remove CH bit
            int minutes = BcdToDec((byte)(timeData[1] & 0x7F));
            int hours = BcdToDec((byte)(timeData[2] & 0x3F));        // Remove 12/24 and AM/PM bits
            int day = BcdToDec((byte)(timeData[3] & 0x07));          // Day of week (not used for DateTime)
            int date = BcdToDec((byte)(timeData[4] & 0x3F));
            int month = BcdToDec((byte)(timeData[5] & 0x1F));        // Remove century bit
            int year = 2000 + BcdToDec(timeData[6]);

            var dateTime = new DateTime(year, month, date, hours, minutes, seconds);
            _logger?.LogDebug("Time read: {dateTime}", dateTime);

            return dateTime;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read time from signal generator {deviceId}", _deviceId);
            throw new InvalidOperationException(
                $"Failed to read time from signal generator {_deviceId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reads the current temperature from the DS3231.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The temperature in degrees Celsius.</returns>
    public async Task<double> ReadTemperatureAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        try
        {
            _logger?.LogDebug("Reading temperature from signal generator {deviceId}", _deviceId);

            // Read temperature register (2 bytes)
            var tempData = await ReadRegisterAsync(RegisterTemperature, 2, cancellationToken).ConfigureAwait(false);

            // Temperature is in upper byte (integer) and lower byte (fractional, upper 2 bits)
            int integerPart = (sbyte)tempData[0];  // Signed byte
            double fractionalPart = ((tempData[1] >> 6) & 0x03) * 0.25;
            double temperature = integerPart + fractionalPart;

            _logger?.LogDebug("Temperature read: {temperature}°C", temperature);

            return temperature;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read temperature from signal generator {deviceId}", _deviceId);
            throw new InvalidOperationException(
                $"Failed to read temperature from signal generator {_deviceId}: {ex.Message}", ex);
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
        _logger?.LogInformation("Disposing signal generator device {deviceId}", _deviceId);

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Reads one or more registers from the DS3231.
    /// </summary>
    private async Task<byte[]> ReadRegisterAsync(byte registerAddress, int length, CancellationToken cancellationToken)
    {
        try
        {
            // Send register address
            await _bridge.WriteToSlaveAsync(Ds3231SlaveAddress, new[] { registerAddress }, cancellationToken).ConfigureAwait(false);

            // Read data
            return await _bridge.ReadFromSlaveAsync(Ds3231SlaveAddress, (byte)length, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read register 0x{registerAddress:X2} from signal generator", registerAddress);
            throw;
        }
    }

    /// <summary>
    /// Writes to one or more registers of the DS3231.
    /// </summary>
    private async Task WriteRegisterAsync(byte registerAddress, byte[] data, CancellationToken cancellationToken)
    {
        try
        {
            // Combine register address and data
            var writeBuffer = new byte[1 + data.Length];
            writeBuffer[0] = registerAddress;
            Array.Copy(data, 0, writeBuffer, 1, data.Length);

            // Write to device
            await _bridge.WriteToSlaveAsync(Ds3231SlaveAddress, writeBuffer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to write to register 0x{registerAddress:X2} on signal generator", registerAddress);
            throw;
        }
    }

    /// <summary>
    /// Gets the rate select bits for the specified frequency.
    /// </summary>
    private static byte GetRateSelectBits(double frequency)
    {
        return frequency switch
        {
            1.0 => RateSelect1Hz,
            1024.0 => RateSelect1024Hz,
            4096.0 => RateSelect4096Hz,
            8192.0 => RateSelect8192Hz,
            _ => throw new ArgumentException($"Unsupported frequency: {frequency} Hz", nameof(frequency))
        };
    }

    /// <summary>
    /// Validates that the frequency is supported.
    /// </summary>
    private void ValidateFrequency(double frequency)
    {
        bool isValid = Array.Exists(SupportedFrequencies, f => Math.Abs(f - frequency) < 0.01);
        if (!isValid)
        {
            throw new ArgumentException(
                $"Unsupported frequency: {frequency} Hz. Supported frequencies: 1 Hz, 1024 Hz, 4096 Hz, 8192 Hz",
                nameof(frequency));
        }
    }

    /// <summary>
    /// Converts BCD (Binary-Coded Decimal) to decimal.
    /// </summary>
    private static int BcdToDec(byte bcd)
    {
        return ((bcd >> 4) * 10) + (bcd & 0x0F);
    }

    /// <summary>
    /// Converts decimal to BCD (Binary-Coded Decimal).
    /// </summary>
    private static byte DecToBcd(int dec)
    {
        return (byte)(((dec / 10) << 4) | (dec % 10));
    }

    /// <summary>
    /// Ensures the device is initialized before operations.
    /// </summary>
    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                $"Signal generator {_deviceId} is not initialized. Call InitializeAsync() first.");
        }
    }

    /// <summary>
    /// Throws ObjectDisposedException if the device has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name, $"Signal generator {_deviceId} has been disposed");
        }
    }
}
