using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using I2CBridge.Framework.Contracts;

namespace I2CBridge.Framework.Core;

/// <summary>
/// Thread-safe registry for managing I2C devices.
/// 
/// Implements the Registry Pattern with thread-safe access using ReaderWriterLockSlim
/// for optimal performance with high read-to-write ratios (typical for device registries).
/// 
/// Supports:
/// - Register/Unregister devices
/// - Query devices by ID with type safety
/// - Enumerate all devices or filter by type
/// - Thread-safe concurrent access
/// </summary>
public class DeviceRegistry : IDisposable
{
    private readonly Dictionary<string, II2cDevice> _devices;
    private readonly ILogger<DeviceRegistry>? _logger;
    private readonly ReaderWriterLockSlim _lock;
    private bool _disposed;

    /// <summary>
    /// Gets the count of registered devices.
    /// </summary>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _devices.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the DeviceRegistry class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public DeviceRegistry(ILogger<DeviceRegistry>? logger = null)
    {
        _devices = new Dictionary<string, II2cDevice>();
        _logger = logger;
        _lock = new ReaderWriterLockSlim();
    }

    /// <summary>
    /// Registers a device in the registry.
    /// </summary>
    /// <param name="device">The device to register.</param>
    /// <exception cref="ArgumentNullException">Thrown if device is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if registry is disposed.</exception>
    public void Register(II2cDevice device)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(device, nameof(device));

        _lock.EnterWriteLock();
        try
        {
            bool isOverwrite = _devices.ContainsKey(device.DeviceId);

            if (isOverwrite)
            {
                _logger?.LogWarning(
                    "Device {deviceId} is already registered. Overwriting with new instance.",
                    device.DeviceId);
            }

            _devices[device.DeviceId] = device;

            _logger?.LogInformation(
                "Device {deviceId} ({deviceType}) registered at address 0x{slaveAddress:X2}",
                device.DeviceId, device.DeviceType, device.SlaveAddress);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Unregisters a device from the registry.
    /// </summary>
    /// <param name="deviceId">The device ID to unregister.</param>
    /// <returns>True if device was unregistered; false if not found.</returns>
    /// <exception cref="ArgumentException">Thrown if deviceId is null or empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if registry is disposed.</exception>
    public bool Unregister(string deviceId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(deviceId, nameof(deviceId));

        _lock.EnterWriteLock();
        try
        {
            bool removed = _devices.Remove(deviceId);

            if (removed)
            {
                _logger?.LogInformation("Device {deviceId} unregistered", deviceId);
            }
            else
            {
                _logger?.LogWarning("Device {deviceId} not found for unregistration", deviceId);
            }

            return removed;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets a device by ID.
    /// </summary>
    /// <param name="deviceId">The device ID.</param>
    /// <returns>The device instance.</returns>
    /// <exception cref="ArgumentException">Thrown if deviceId is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">Thrown if device not found.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if registry is disposed.</exception>
    public II2cDevice GetDevice(string deviceId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(deviceId, nameof(deviceId));

        _lock.EnterReadLock();
        try
        {
            if (!_devices.TryGetValue(deviceId, out var device))
            {
                throw new KeyNotFoundException($"Device '{deviceId}' not found in registry");
            }

            return device;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets a device by ID with type casting.
    /// </summary>
    /// <typeparam name="T">The expected device type.</typeparam>
    /// <param name="deviceId">The device ID.</param>
    /// <returns>The device instance cast to the specified type.</returns>
    /// <exception cref="ArgumentException">Thrown if deviceId is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">Thrown if device not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown if device is not of the specified type.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if registry is disposed.</exception>
    public T GetDevice<T>(string deviceId) where T : class, II2cDevice
    {
        ThrowIfDisposed();

        var device = GetDevice(deviceId);

        if (device is not T typedDevice)
        {
            throw new InvalidOperationException(
                $"Device '{deviceId}' is of type '{device.GetType().Name}', not '{typeof(T).Name}'");
        }

        return typedDevice;
    }

    /// <summary>
    /// Gets a copy of all registered devices.
    /// </summary>
    /// <returns>An enumerable of all registered devices.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if registry is disposed.</exception>
    public IEnumerable<II2cDevice> GetAllDevices()
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            return _devices.Values.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all devices of a specific type.
    /// </summary>
    /// <typeparam name="T">The device type to filter by.</typeparam>
    /// <returns>An enumerable of devices of the specified type.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if registry is disposed.</exception>
    public IEnumerable<T> GetDevicesByType<T>() where T : class, II2cDevice
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            return _devices.Values.OfType<T>().ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Checks if a device is registered.
    /// </summary>
    /// <param name="deviceId">The device ID.</param>
    /// <returns>True if device is registered; false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown if deviceId is null or empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if registry is disposed.</exception>
    public bool Contains(string deviceId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(deviceId, nameof(deviceId));

        _lock.EnterReadLock();
        try
        {
            return _devices.ContainsKey(deviceId);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Clears all registered devices.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if registry is disposed.</exception>
    public void Clear()
    {
        ThrowIfDisposed();

        _lock.EnterWriteLock();
        try
        {
            int count = _devices.Count;
            _devices.Clear();
            _logger?.LogInformation("Device registry cleared ({count} devices removed)", count);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Releases all resources associated with the registry.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _lock?.Dispose();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error disposing DeviceRegistry");
        }
    }

    /// <summary>
    /// Throws ObjectDisposedException if the registry has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name, "DeviceRegistry has been disposed");
        }
    }
}
