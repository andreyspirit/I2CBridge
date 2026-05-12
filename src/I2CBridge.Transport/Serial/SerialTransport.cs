using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using I2CBridge.Framework.Contracts.Transport;

namespace I2CBridge.Transport.Serial
{
    /// <summary>
    /// Implements serial port communication as a transport layer.
    /// Supports RS232, RS485, and other serial protocols via System.IO.Ports.SerialPort.
    /// </summary>
    public class SerialTransport : ITransport
    {
        private readonly SerialPort _serialPort;
        private readonly ILogger<SerialTransport>? _logger;
        private readonly object _lockObject = new object();
        private bool _isConnected = false;
        private bool _disposed = false;

        /// <summary>
        /// Gets the name of this transport (typically the serial port name, e.g., "COM1").
        /// </summary>
        public string Name => _serialPort.PortName;

        /// <summary>
        /// Gets whether the serial port is currently connected and open.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                lock (_lockObject)
                {
                    return _isConnected && _serialPort.IsOpen;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the SerialTransport class.
        /// </summary>
        /// <param name="portName">The name of the serial port (e.g., "COM1", "/dev/ttyUSB0").</param>
        /// <param name="config">Serial port configuration including baud rate, parity, data bits, stop bits, and timeouts.</param>
        /// <param name="logger">Optional logger for debug and error logging.</param>
        public SerialTransport(string portName, SerialPortConfiguration config, ILogger<SerialTransport>? logger = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(portName, nameof(portName));
            ArgumentNullException.ThrowIfNull(config, nameof(config));

            _logger = logger;
            _serialPort = new SerialPort(portName)
            {
                BaudRate = config.BaudRate,
                DataBits = config.DataBits,
                StopBits = config.StopBits,
                Parity = config.Parity,
                ReadTimeout = config.ReadTimeoutMs,
                WriteTimeout = config.WriteTimeoutMs,
                NewLine = config.NewLine
            };

            _logger?.LogInformation(
                "SerialTransport created: {portName} at {baudRate} baud, {dataBits} data bits, parity={parity}, stopBits={stopBits}",
                portName, config.BaudRate, config.DataBits, config.Parity, config.StopBits);
        }

        /// <summary>
        /// Opens the serial port connection asynchronously.
        /// </summary>
        /// <param name="ct">Cancellation token (note: opening is not cancellable, but token is accepted for interface compatibility).</param>
        /// <exception cref="TransportException">Thrown if the port cannot be opened.</exception>
        public async Task OpenAsync(CancellationToken ct = default)
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (_isConnected && _serialPort.IsOpen)
                {
                    throw new InvalidOperationException($"Serial port {_serialPort.PortName} is already open.");
                }

                try
                {
                    _serialPort.Open();
                    _isConnected = true;
                    _logger?.LogInformation("Serial port {portName} opened successfully", _serialPort.PortName);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger?.LogError(ex, "Access denied to serial port {portName}", _serialPort.PortName);
                    throw new TransportException("ACCESS_DENIED", $"Access denied to port {_serialPort.PortName}", isRecoverable: false, ex);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to open serial port {portName}", _serialPort.PortName);
                    throw new TransportException("OPEN_FAILED", $"Failed to open port {_serialPort.PortName}: {ex.Message}", isRecoverable: true, ex);
                }
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Closes the serial port connection asynchronously.
        /// </summary>
        /// <param name="ct">Cancellation token (closing is not cancellable, but token is accepted for interface compatibility).</param>
        public async Task CloseAsync(CancellationToken ct = default)
        {
            lock (_lockObject)
            {
                if (!_isConnected || !_serialPort.IsOpen)
                {
                    _logger?.LogWarning("Attempted to close already-closed serial port {portName}", _serialPort.PortName);
                    return;
                }

                try
                {
                    _serialPort.Close();
                    _isConnected = false;
                    _logger?.LogInformation("Serial port {portName} closed successfully", _serialPort.PortName);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error closing serial port {portName}", _serialPort.PortName);
                    throw new TransportException("CLOSE_FAILED", $"Error closing port {_serialPort.PortName}: {ex.Message}", isRecoverable: false, ex);
                }
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Sends data through the serial port asynchronously.
        /// </summary>
        /// <param name="data">The data to send (read-only to prevent accidental modification).</param>
        /// <param name="ct">Cancellation token (note: write is not cancellable, but token is accepted for interface compatibility).</param>
        /// <exception cref="TransportNotConnectedException">Thrown if transport is not connected.</exception>
        /// <exception cref="TransportTimeoutException">Thrown if the send operation times out.</exception>
        /// <exception cref="TransportException">Thrown for other transport-layer failures.</exception>
        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (!IsConnected)
                {
                    const string errorMessage = "Cannot send data: transport is not connected.";
                    _logger?.LogError(errorMessage);
                    return new ValueTask(Task.FromException(new TransportNotConnectedException(errorMessage)));
                }

                if (data.Length == 0)
                {
                    _logger?.LogWarning("Attempted to send empty data");
                    return default;
                }

                try
                {
                    byte[] buffer = data.ToArray();
                    _serialPort.Write(buffer, 0, buffer.Length);
                    _logger?.LogDebug("Sent {byteCount} bytes via {portName}", data.Length, _serialPort.PortName);
                }
                catch (TimeoutException ex)
                {
                    const string errorMessage = "Send operation timed out";
                    _logger?.LogError(ex, errorMessage);
                    return new ValueTask(Task.FromException(new TransportTimeoutException(errorMessage, ex)));
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Failed to send data: {ex.Message}";
                    _logger?.LogError(ex, errorMessage);
                    return new ValueTask(Task.FromException(new TransportException("SEND_FAILED", errorMessage, isRecoverable: true, ex)));
                }
            }

            return default;
        }

        /// <summary>
        /// Receives data from the serial port asynchronously into a pre-allocated buffer.
        /// </summary>
        /// <param name="buffer">Pre-allocated buffer to receive data into.</param>
        /// <param name="ct">Cancellation token to cancel the receive operation.</param>
        /// <returns>Number of bytes actually read into the buffer.</returns>
        /// <exception cref="TransportNotConnectedException">Thrown if transport is not connected.</exception>
        /// <exception cref="TransportTimeoutException">Thrown if the receive operation times out.</exception>
        /// <exception cref="TransportException">Thrown for other transport-layer failures.</exception>
        public ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (!IsConnected)
                {
                    const string errorMessage = "Cannot receive data: transport is not connected.";
                    _logger?.LogError(errorMessage);
                    return new ValueTask<int>(Task.FromException<int>(new TransportNotConnectedException(errorMessage)));
                }

                if (buffer.Length == 0)
                {
                    _logger?.LogWarning("Attempted to receive into empty buffer");
                    return new ValueTask<int>(0);
                }

                try
                {
                    byte[] bufferArray = buffer.ToArray();
                    int bytesRead = _serialPort.Read(bufferArray, 0, bufferArray.Length);
                    bufferArray.AsSpan(0, bytesRead).CopyTo(buffer.Span);
                    _logger?.LogDebug("Received {byteCount} bytes from {portName}", bytesRead, _serialPort.PortName);
                    return new ValueTask<int>(bytesRead);
                }
                catch (TimeoutException ex)
                {
                    const string errorMessage = "Receive operation timed out";
                    _logger?.LogError(ex, errorMessage);
                    return new ValueTask<int>(Task.FromException<int>(new TransportTimeoutException(errorMessage, ex)));
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Failed to receive data: {ex.Message}";
                    _logger?.LogError(ex, errorMessage);
                    return new ValueTask<int>(Task.FromException<int>(new TransportException("RECEIVE_FAILED", errorMessage, isRecoverable: true, ex)));
                }
            }
        }

        /// <summary>
        /// Flushes pending data in the serial port buffers.
        /// </summary>
        /// <param name="ct">Cancellation token (flush is not cancellable, but token is accepted for interface compatibility).</param>
        /// <exception cref="TransportException">Thrown if the flush operation fails.</exception>
        public async Task FlushAsync(CancellationToken ct = default)
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (!IsConnected)
                {
                    _logger?.LogWarning("Attempted to flush on disconnected port {portName}", _serialPort.PortName);
                    return;
                }

                try
                {
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();
                    _logger?.LogDebug("Flushed buffers for {portName}", _serialPort.PortName);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error flushing buffers for {portName}", _serialPort.PortName);
                    throw new TransportException("FLUSH_FAILED", $"Error flushing buffers: {ex.Message}", isRecoverable: true, ex);
                }
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Releases all resources associated with the transport.
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
                if (_isConnected && _serialPort.IsOpen)
                {
                    await CloseAsync().ConfigureAwait(false);
                }

                _serialPort?.Dispose();
                _logger?.LogInformation("SerialTransport disposed: {portName}", _serialPort.PortName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing SerialTransport");
            }
        }

        /// <summary>
        /// Throws ObjectDisposedException if the transport has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name, "SerialTransport has been disposed.");
            }
        }
    }
}
