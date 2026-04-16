using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using I2CBridge.Framework.Contracts;

namespace I2CBridge.Framework.Core
{
    /// <summary>
    /// Registry for managing I2C devices
    /// </summary>
    public class DeviceRegistry
    {
        private readonly Dictionary<string, II2cDevice> _devices;
        private readonly ILogger<DeviceRegistry> _logger;

        public DeviceRegistry(ILogger<DeviceRegistry> logger)
        {
            _devices = new Dictionary<string, II2cDevice>();
            _logger = logger;
        }

        /// <summary>
        /// Registers a device in the registry
        /// </summary>
        public void Register(II2cDevice device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            if (_devices.ContainsKey(device.DeviceId))
            {
                _logger?.LogWarning($"Device {device.DeviceId} is already registered. Overwriting.");
            }

            _devices[device.DeviceId] = device;
            _logger?.LogInformation($"Device {device.DeviceId} ({device.DeviceType}) registered at address 0x{device.SlaveAddress:X2}");
        }

        /// <summary>
        /// Unregisters a device from the registry
        /// </summary>
        public bool Unregister(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentNullException(nameof(deviceId));

            var removed = _devices.Remove(deviceId);
            if (removed)
            {
                _logger?.LogInformation($"Device {deviceId} unregistered");
            }
            else
            {
                _logger?.LogWarning($"Device {deviceId} not found for unregistration");
            }

            return removed;
        }

        /// <summary>
        /// Gets a device by ID
        /// </summary>
        public II2cDevice GetDevice(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentNullException(nameof(deviceId));

            if (!_devices.TryGetValue(deviceId, out var device))
            {
                throw new KeyNotFoundException($"Device {deviceId} not found in registry");
            }

            return device;
        }

        /// <summary>
        /// Gets a device by ID with type casting
        /// </summary>
        public T GetDevice<T>(string deviceId) where T : class, II2cDevice
        {
            var device = GetDevice(deviceId);
            var typedDevice = device as T;

            if (typedDevice == null)
            {
                throw new InvalidOperationException($"Device {deviceId} is not of type {typeof(T).Name}");
            }

            return typedDevice;
        }

        /// <summary>
        /// Gets all registered devices
        /// </summary>
        public IEnumerable<II2cDevice> GetAllDevices()
        {
            return _devices.Values.ToList();
        }

        /// <summary>
        /// Gets all devices of a specific type
        /// </summary>
        public IEnumerable<T> GetDevicesByType<T>() where T : class, II2cDevice
        {
            return _devices.Values.OfType<T>();
        }

        /// <summary>
        /// Checks if a device is registered
        /// </summary>
        public bool Contains(string deviceId)
        {
            return _devices.ContainsKey(deviceId);
        }

        /// <summary>
        /// Gets the count of registered devices
        /// </summary>
        public int Count => _devices.Count;

        /// <summary>
        /// Clears all registered devices
        /// </summary>
        public void Clear()
        {
            _devices.Clear();
            _logger?.LogInformation("Device registry cleared");
        }
    }
}