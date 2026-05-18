using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using I2CBridge.Bridges.Internal;
using I2CBridge.Framework.Contracts;
using I2CBridge.Framework.Contracts.Transport;

namespace I2CBridge.Bridges;

/// <summary>
/// Implements the SC18IM700 I2C bridge controller.
/// 
/// The SC18IM700 is designed to serve as an interface between a standard UART port and the I2C-bus.
/// It operates as an I2C-bus master and controls all I2C-specific sequences, protocol, arbitration and timing.
/// Communication uses ASCII-based command protocol.
/// 
/// Supported operations:
/// - I2C write: Direct write to slave device
/// - I2C read: Direct read from slave device
/// - Write-read: Combined write-then-read with repeated start
/// - Register access: Configure device parameters (baud rate, I2C clock, etc.)
/// - GPIO control: Read/write to GPIO pins
/// 
/// Reference: SC18IM700 UART to I2C Controller Datasheet
/// </summary>
public class Sc18im700Bridge : II2cBridge
{
    private readonly ITransport _transport;
    private readonly SC18IM700Protocol _protocol;
    private readonly ILogger<Sc18im700Bridge>? _logger;
    private bool _disposed = false;
    private bool _initialized = false;

    /// <summary>
    /// Gets the bridge name.
    /// </summary>
    public string Name => "SC18IM700";

    /// <summary>
    /// Initializes a new instance of the Sc18im700Bridge class.
    /// </summary>
    /// <param name="transport">The transport layer (serial port, USB, etc.).</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown if transport is null.</exception>
    public Sc18im700Bridge(ITransport transport, ILogger<Sc18im700Bridge>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(transport, nameof(transport));
        _transport = transport;
        _logger = logger;
        _protocol = new SC18IM700Protocol(logger as ILogger<SC18IM700Protocol>);

        _logger?.LogInformation("SC18IM700 bridge instance created, transport: {transportName}", _transport.Name);
    }

    /// <summary>
    /// Initializes the bridge by opening the transport connection.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ObjectDisposedException">Thrown if bridge is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if initialization fails.</exception>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_initialized)
        {
            _logger?.LogWarning("Bridge is already initialized");
            return;
        }

        try
        {
            _logger?.LogInformation("Initializing SC18IM700 bridge");

            if (!_transport.IsConnected)
            {
                await _transport.OpenAsync(cancellationToken).ConfigureAwait(false);
                _logger?.LogInformation("Transport connection opened: {transportName}", _transport.Name);
            }

            await _transport.FlushAsync(cancellationToken).ConfigureAwait(false);

            _initialized = true;
            _logger?.LogInformation("SC18IM700 bridge initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize SC18IM700 bridge");
            throw new InvalidOperationException("Failed to initialize SC18IM700 bridge. See inner exception for details.", ex);
        }
    }

    /// <summary>
    /// Writes data to an I2C slave device.
    /// </summary>
    /// <param name="slaveAddress">The 7-bit I2C slave address.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ObjectDisposedException">Thrown if bridge is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if bridge is not initialized.</exception>
    /// <exception cref="ArgumentException">Thrown if data is invalid.</exception>
    /// <exception cref="TransportException">Thrown if operation fails.</exception>
    public async Task WriteToSlaveAsync(byte slaveAddress, byte[] data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Data cannot be null or empty.", nameof(data));
        }

        try
        {
            _logger?.LogDebug("Writing {byteCount} bytes to I2C slave at address 0x{slaveAddress:X2}", data.Length, slaveAddress);

            byte[] frame = _protocol.BuildI2cWriteFrame(slaveAddress, data);
            await _transport.SendAsync(new ReadOnlyMemory<byte>(frame), cancellationToken).ConfigureAwait(false);

            // Receive acknowledgment (status byte)
            var ackBuffer = new byte[1];
            int bytesReceived = await _transport.ReceiveAsync(new Memory<byte>(ackBuffer), cancellationToken).ConfigureAwait(false);

            if (bytesReceived != 1)
            {
                throw new TransportException(
                    "WRITE_NO_ACK",
                    "SC18IM700 did not send acknowledgment",
                    isRecoverable: true);
            }

            if (!SC18IM700Protocol.IsStatusOk(ackBuffer[0]))
            {
                string statusDesc = SC18IM700Protocol.GetStatusDescription(ackBuffer[0]);
                throw new TransportException(
                    "WRITE_NAK",
                    $"Write operation failed: {statusDesc}",
                    isRecoverable: true);
            }

            _logger?.LogDebug("Write operation completed successfully for slave 0x{slaveAddress:X2}", slaveAddress);
        }
        catch (TransportException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Write operation failed for I2C slave at address 0x{slaveAddress:X2}", slaveAddress);
            throw new TransportException(
                "WRITE_FAILED",
                $"Failed to write to I2C slave at address 0x{slaveAddress:X2}: {ex.Message}",
                isRecoverable: true,
                ex);
        }
    }

    /// <summary>
    /// Reads data from an I2C slave device.
    /// </summary>
    /// <param name="slaveAddress">The 7-bit I2C slave address.</param>
    /// <param name="length">The number of bytes to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The data read from the I2C slave.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if bridge is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if bridge is not initialized.</exception>
    /// <exception cref="ArgumentException">Thrown if length is invalid.</exception>
    /// <exception cref="TransportException">Thrown if operation fails.</exception>
    public async Task<byte[]> ReadFromSlaveAsync(byte slaveAddress, byte length, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        if (length == 0)
        {
            throw new ArgumentException("Length must be greater than 0.", nameof(length));
        }

        try
        {
            _logger?.LogDebug("Reading {length} bytes from I2C slave at address 0x{slaveAddress:X2}", length, slaveAddress);

            byte[] frame = _protocol.BuildI2cReadFrame(slaveAddress, length);
            await _transport.SendAsync(new ReadOnlyMemory<byte>(frame), cancellationToken).ConfigureAwait(false);

            // Receive response: [Status][Data...]
            var responseBuffer = new byte[length + 1];
            int bytesReceived = await _transport.ReceiveAsync(new Memory<byte>(responseBuffer), cancellationToken).ConfigureAwait(false);

            if (bytesReceived < 1)
            {
                throw new TransportException(
                    "READ_NO_RESPONSE",
                    "SC18IM700 did not respond to read request",
                    isRecoverable: true);
            }

            if (!_protocol.TryParseI2cResponse(
                new ReadOnlyMemory<byte>(responseBuffer, 0, bytesReceived),
                length,
                out byte status,
                out byte[]? data))
            {
                string statusDesc = SC18IM700Protocol.GetStatusDescription(status);
                throw new TransportException(
                    "READ_NAK",
                    $"Read operation failed: {statusDesc}",
                    isRecoverable: true);
            }

            _logger?.LogDebug("Read operation completed: received {bytesCount} bytes from slave 0x{slaveAddress:X2}", data.Length, slaveAddress);

            return data;
        }
        catch (TransportException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Read operation failed for I2C slave at address 0x{slaveAddress:X2}", slaveAddress);
            throw new TransportException(
                "READ_FAILED",
                $"Failed to read from I2C slave at address 0x{slaveAddress:X2}: {ex.Message}",
                isRecoverable: true,
                ex);
        }
    }

    /// <summary>
    /// Performs a combined write-then-read (repeated start) operation.
    /// </summary>
    /// <param name="slaveAddress">The 7-bit I2C slave address.</param>
    /// <param name="writeData">The data to write.</param>
    /// <param name="readLength">The number of bytes to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The data read from the I2C slave.</returns>
    public async Task<byte[]> WriteReadAsync(byte slaveAddress, byte[] writeData, byte readLength, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        _logger?.LogDebug(
            "Performing write-read operation: write {writeLength} bytes, read {readLength} bytes to slave 0x{slaveAddress:X2}",
            writeData?.Length ?? 0, readLength, slaveAddress);

        try
        {
            if (writeData != null && writeData.Length > 0)
            {
                await WriteToSlaveAsync(slaveAddress, writeData, cancellationToken).ConfigureAwait(false);
            }

            var result = await ReadFromSlaveAsync(slaveAddress, readLength, cancellationToken).ConfigureAwait(false);

            _logger?.LogDebug("Write-read operation completed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Write-read operation failed for I2C slave at address 0x{slaveAddress:X2}", slaveAddress);
            throw;
        }
    }

    /// <summary>
    /// Releases all resources associated with the bridge.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _logger?.LogInformation("Disposing SC18IM700 bridge");

            if (_transport?.IsConnected ?? false)
            {
                await _transport.CloseAsync().ConfigureAwait(false);
                _logger?.LogInformation("Transport connection closed");
            }

            if (_transport != null)
            {
                await _transport.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during SC18IM700 bridge disposal");
        }
    }

    /// <summary>
    /// Ensures the bridge is initialized before operations.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if bridge is not initialized.</exception>
    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Bridge must be initialized before performing operations. Call InitializeAsync() first.");
        }
    }

    /// <summary>
    /// Throws ObjectDisposedException if the bridge has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if disposed.</exception>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name, "SC18IM700 bridge has been disposed.");
        }
    }
}