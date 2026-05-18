using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using I2CBridge.Framework.Contracts;
using I2CBridge.Framework.Contracts.Devices;
using I2CBridge.Framework.Contracts.Transport;

namespace I2CBridge.Devices;

/// <summary>
/// Microchip 24XX08 I2C EEPROM Device Implementation
/// 
/// The 24XX08 is an 8 Kbit Electrically Erasable PROM organized as four blocks 
/// of 256 x 8-bit memory with a 2-wire (I2C) serial interface.
/// 
/// Key Features:
/// - Total Capacity: 8 Kbits (1024 bytes)
/// - Organization: Four 256-byte blocks
/// - Memory Type: Electrically Erasable PROM (EEPROM)
/// - Interface: I2C (2-wire serial)
/// - Operating Voltage: 1.8V to 5.5V
/// - Page Write: Up to 16 bytes per write cycle
/// - Current: 1 µA (standby), 1 mA (active)
/// - Packages: 8-pin PDIP, SOIC, TSSOP, MSOP, 5-lead SOT-23
/// 
/// I2C Address Format:
/// - Base Address: 0x50 (default)
/// - Address with block select: 0x50 + block (0-3)
/// - Full address: 1010[A2][A1][A0]RW
/// 
/// Memory Layout:
/// - Block 0: 0x000 - 0x0FF (Bytes 0-255)
/// - Block 1: 0x100 - 0x1FF (Bytes 256-511)
/// - Block 2: 0x200 - 0x2FF (Bytes 512-767)
/// - Block 3: 0x300 - 0x3FF (Bytes 768-1023)
/// 
/// Write Operations:
/// - Single Byte Write: Address (1 byte) + Data (1 byte)
/// - Page Write: Address (1 byte) + Data (up to 16 bytes)
/// - Write Cycle Time: 5ms typical, 10ms maximum
/// 
/// Read Operations:
/// - Current Address Read: Data is returned from current pointer
/// - Random Address Read: Address (1 byte) + Data (1+ bytes)
/// - Sequential Read: Address (1 byte) + Data (continuous)
/// 
/// Reference: Microchip Technology Inc. 24AA08/24LC08B Datasheet
/// </summary>
public class Eeprom24xx08 : II2cDevice, IMemoryDevice
{
    // Device Constants
    private const int TotalCapacity = 1024;           // 8 Kbits = 1024 bytes
    private const int BlockSize = 256;                // 256 bytes per block
    private const int PageSize = 16;                  // Maximum 16 bytes per page write
    private const int WriteTimeoutMs = 100;           // 10ms typical + margin
    private const int EepromWriteCycleMs = 10;        // 10ms max write cycle time

    // I2C Protocol Constants
    private const byte WriteControlBit = 0x00;
    private const byte ReadControlBit = 0x01;

    private readonly II2cBridge _bridge;
    private readonly ILogger<Eeprom24xx08>? _logger;
    private readonly byte _slaveAddress;
    private readonly string _deviceId;
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
    public string DeviceType => "24XX08 EEPROM";

    /// <summary>
    /// Gets the total capacity of the EEPROM in bytes.
    /// </summary>
    public int Capacity => TotalCapacity;

    /// <summary>
    /// Gets the page size (maximum bytes per write operation).
    /// </summary>
    public int PageWriteSize => PageSize;

    /// <summary>
    /// Initializes a new instance of the Eeprom24xx08 class.
    /// </summary>
    /// <param name="deviceId">Unique identifier for this device instance.</param>
    /// <param name="bridge">The I2C bridge used for communication.</param>
    /// <param name="slaveAddress">The I2C slave address.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentException">Thrown if deviceId is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if bridge is null.</exception>
    /// <exception cref="ArgumentException">Thrown if slaveAddress is invalid.</exception>
    public Eeprom24xx08(
        string deviceId,
        II2cBridge bridge,
        byte slaveAddress,
        ILogger<Eeprom24xx08>? logger = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId, nameof(deviceId));
        ArgumentNullException.ThrowIfNull(bridge, nameof(bridge));
        ValidateSlaveAddress(slaveAddress);

        _deviceId = deviceId;
        _bridge = bridge;
        _slaveAddress = slaveAddress;
        _logger = logger;

        _logger?.LogInformation(
            "Eeprom24xx08 instance created: ID={deviceId}, Address=0x{slaveAddress:X2}, Capacity={capacity} bytes",
            deviceId, slaveAddress, TotalCapacity);
    }

    /// <summary>
    /// Initializes the EEPROM device for communication.
    /// Verifies device presence by attempting a read operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ObjectDisposedException">Thrown if device is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device initialization fails.</exception>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_initialized)
        {
            _logger?.LogWarning("EEPROM {deviceId} is already initialized", _deviceId);
            return;
        }

        try
        {
            _logger?.LogInformation("Initializing EEPROM {deviceId} at address 0x{slaveAddress:X2}", _deviceId, _slaveAddress);

            // Verify device presence by reading 1 byte from address 0
            var testData = await ReadAsync(0, 1, cancellationToken).ConfigureAwait(false);

            if (testData == null || testData.Length != 1)
            {
                throw new InvalidOperationException($"EEPROM device {_deviceId} failed verification read");
            }

            _initialized = true;
            _logger?.LogInformation("EEPROM {deviceId} initialized successfully", _deviceId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize EEPROM {deviceId}", _deviceId);
            throw new InvalidOperationException($"Failed to initialize EEPROM device {_deviceId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reads data from a specified address in the EEPROM.
    /// </summary>
    /// <param name="address">The starting address (0 to 1023).</param>
    /// <param name="length">The number of bytes to read (1 to 1024).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The data read from the EEPROM.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if device is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device is not initialized or operation fails.</exception>
    /// <exception cref="ArgumentException">Thrown if parameters are invalid.</exception>
    public async Task<byte[]> ReadAsync(int address, int length, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        ValidateReadParameters(address, length);

        try
        {
            _logger?.LogDebug(
                "Reading {length} bytes from EEPROM {deviceId} at address 0x{address:X3}",
                length, _deviceId, address);

            var result = new byte[length];
            int bytesRead = 0;

            // Read in chunks to handle multi-block reads
            while (bytesRead < length)
            {
                int chunkSize = Math.Min(PageSize, length - bytesRead);
                int currentAddress = address + bytesRead;

                // Send address (random read)
                byte addressByte = (byte)currentAddress;
                await _bridge.WriteToSlaveAsync(_slaveAddress, new[] { addressByte }, cancellationToken).ConfigureAwait(false);

                // Read data
                var chunkData = await _bridge.ReadFromSlaveAsync(_slaveAddress, (byte)chunkSize, cancellationToken).ConfigureAwait(false);

                Array.Copy(chunkData, 0, result, bytesRead, chunkData.Length);
                bytesRead += chunkData.Length;

                // Allow EEPROM to process between chunks
                if (bytesRead < length)
                {
                    await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                }
            }

            _logger?.LogDebug(
                "Successfully read {bytesRead} bytes from EEPROM {deviceId}",
                bytesRead, _deviceId);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to read from EEPROM {deviceId} at address 0x{address:X3}",
                _deviceId, address);
            throw new InvalidOperationException(
                $"Failed to read from EEPROM device {_deviceId} at address 0x{address:X3}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Writes data to a specified address in the EEPROM.
    /// Automatically handles page boundaries and respects the 16-byte page write limit.
    /// </summary>
    /// <param name="address">The starting address (0 to 1023).</param>
    /// <param name="data">The data to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ObjectDisposedException">Thrown if device is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device is not initialized or operation fails.</exception>
    /// <exception cref="ArgumentException">Thrown if parameters are invalid.</exception>
    public async Task WriteAsync(int address, byte[] data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        ArgumentNullException.ThrowIfNull(data, nameof(data));
        ValidateWriteParameters(address, data.Length);

        try
        {
            _logger?.LogDebug(
                "Writing {length} bytes to EEPROM {deviceId} at address 0x{address:X3}",
                data.Length, _deviceId, address);

            int bytesWritten = 0;

            // Write in page-aligned chunks
            while (bytesWritten < data.Length)
            {
                int currentAddress = address + bytesWritten;
                int remainingBytes = data.Length - bytesWritten;

                // Calculate chunk size respecting page boundaries
                int pageOffset = currentAddress % PageSize;
                int chunkSize = Math.Min(PageSize - pageOffset, remainingBytes);

                // Prepare write frame: [Address] [Data...]
                var writeBuffer = new byte[1 + chunkSize];
                writeBuffer[0] = (byte)currentAddress;
                Array.Copy(data, bytesWritten, writeBuffer, 1, chunkSize);

                // Write page
                await _bridge.WriteToSlaveAsync(_slaveAddress, writeBuffer, cancellationToken).ConfigureAwait(false);

                bytesWritten += chunkSize;

                // Wait for EEPROM write cycle to complete
                await Task.Delay(EepromWriteCycleMs, cancellationToken).ConfigureAwait(false);

                // Allow processing between chunks
                if (bytesWritten < data.Length)
                {
                    await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                }
            }

            _logger?.LogInformation(
                "Successfully wrote {bytesWritten} bytes to EEPROM {deviceId} at address 0x{address:X3}",
                bytesWritten, _deviceId, address);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to write to EEPROM {deviceId} at address 0x{address:X3}",
                _deviceId, address);
            throw new InvalidOperationException(
                $"Failed to write to EEPROM device {_deviceId} at address 0x{address:X3}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Writes data asynchronously to the memory device (default address 0).
    /// Implements IMemoryDevice interface.
    /// </summary>
    /// <param name="data">The byte array containing data to be written.</param>
    async Task IMemoryDevice.WriteAsync(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data, nameof(data));
        await WriteAsync(0, data).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads data asynchronously from the memory device (default address 0).
    /// Implements IMemoryDevice interface.
    /// </summary>
    /// <param name="length">The number of bytes to read.</param>
    /// <returns>The data read from the device.</returns>
    async Task<byte[]> IMemoryDevice.ReadAsync(int length)
    {
        return await ReadAsync(0, length).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a single byte to a specific address.
    /// </summary>
    /// <param name="address">The address to write to.</param>
    /// <param name="data">The byte value to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task WriteSingleByteAsync(int address, byte data, CancellationToken cancellationToken = default)
    {
        await WriteAsync(address, new[] { data }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a single byte from a specific address.
    /// </summary>
    /// <param name="address">The address to read from.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The byte value read.</returns>
    public async Task<byte> ReadSingleByteAsync(int address, CancellationToken cancellationToken = default)
    {
        var data = await ReadAsync(address, 1, cancellationToken).ConfigureAwait(false);
        return data[0];
    }

    /// <summary>
    /// Erases the entire EEPROM by writing 0xFF to all addresses.
    /// WARNING: This operation is time-consuming (up to ~10 seconds for 1024 bytes).
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task EraseAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        _logger?.LogWarning("Erasing entire EEPROM {deviceId}", _deviceId);

        try
        {
            var erasePattern = new byte[PageSize];
            for (int i = 0; i < erasePattern.Length; i++)
            {
                erasePattern[i] = 0xFF;
            }

            for (int address = 0; address < TotalCapacity; address += PageSize)
            {
                await WriteAsync(address, erasePattern, cancellationToken).ConfigureAwait(false);
            }

            _logger?.LogInformation("EEPROM {deviceId} erased successfully", _deviceId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to erase EEPROM {deviceId}", _deviceId);
            throw;
        }
    }

    /// <summary>
    /// Verifies that written data matches the expected values.
    /// </summary>
    /// <param name="address">The starting address.</param>
    /// <param name="expectedData">The expected data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if data matches; false otherwise.</returns>
    public async Task<bool> VerifyAsync(int address, byte[] expectedData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedData, nameof(expectedData));

        try
        {
            var readData = await ReadAsync(address, expectedData.Length, cancellationToken).ConfigureAwait(false);
            return readData.SequenceEqual(expectedData);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Verification failed for EEPROM {deviceId}", _deviceId);
            return false;
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
        _logger?.LogInformation("Disposing EEPROM device {deviceId}", _deviceId);

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Validates the slave address.
    /// </summary>
    private void ValidateSlaveAddress(byte address)
    {
        // Valid range: 0x50-0x57 (base address 0x50 with A2, A1, A0 bits)
        if (address < 0x50 || address > 0x57)
        {
            throw new ArgumentException(
                $"Invalid I2C slave address 0x{address:X2}. Valid range: 0x50-0x57 (24XX08 base address with block selection)",
                nameof(address));
        }
    }

    /// <summary>
    /// Validates read parameters.
    /// </summary>
    private void ValidateReadParameters(int address, int length)
    {
        if (address < 0 || address >= TotalCapacity)
        {
            throw new ArgumentException(
                $"Invalid read address 0x{address:X3}. Valid range: 0x000-0x{TotalCapacity - 1:X3}",
                nameof(address));
        }

        if (length <= 0 || length > TotalCapacity)
        {
            throw new ArgumentException(
                $"Invalid read length {length}. Valid range: 1-{TotalCapacity}",
                nameof(length));
        }

        if (address + length > TotalCapacity)
        {
            throw new ArgumentException(
                $"Read would exceed EEPROM capacity. Address: 0x{address:X3}, Length: {length}, Capacity: {TotalCapacity}",
                nameof(length));
        }
    }

    /// <summary>
    /// Validates write parameters.
    /// </summary>
    private void ValidateWriteParameters(int address, int length)
    {
        if (address < 0 || address >= TotalCapacity)
        {
            throw new ArgumentException(
                $"Invalid write address 0x{address:X3}. Valid range: 0x000-0x{TotalCapacity - 1:X3}",
                nameof(address));
        }

        if (length <= 0 || length > TotalCapacity)
        {
            throw new ArgumentException(
                $"Invalid write length {length}. Valid range: 1-{TotalCapacity}",
                nameof(length));
        }

        if (address + length > TotalCapacity)
        {
            throw new ArgumentException(
                $"Write would exceed EEPROM capacity. Address: 0x{address:X3}, Length: {length}, Capacity: {TotalCapacity}",
                nameof(length));
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
                $"EEPROM device {_deviceId} is not initialized. Call InitializeAsync() first.");
        }
    }

    /// <summary>
    /// Throws ObjectDisposedException if the device has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name, $"EEPROM device {_deviceId} has been disposed");
        }
    }
}
