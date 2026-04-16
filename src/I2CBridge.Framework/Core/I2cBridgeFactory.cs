using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using I2CBridge.Framework.Contracts;

namespace I2CBridge.Framework.Core
{
    /// <summary>
    /// Factory for creating and managing I2C bridge instances
    /// </summary>
    public class I2cBridgeFactory
    {
        private readonly Dictionary<string, II2cBridge> _bridges;
        private readonly ILogger<I2cBridgeFactory> _logger;
        private II2cBridge _activeBridge;

        public I2cBridgeFactory(ILogger<I2cBridgeFactory> logger)
        {
            _bridges = new Dictionary<string, II2cBridge>();
            _logger = logger;
        }

        /// <summary>
        /// Registers a bridge implementation
        /// </summary>
        public void RegisterBridge(string bridgeId, II2cBridge bridge)
        {
            if (string.IsNullOrEmpty(bridgeId))
                throw new ArgumentNullException(nameof(bridgeId));

            if (bridge == null)
                throw new ArgumentNullException(nameof(bridge));

            if (_bridges.ContainsKey(bridgeId))
            {
                _logger?.LogWarning($"Bridge {bridgeId} is already registered. Overwriting.");
            }

            _bridges[bridgeId] = bridge;
            _logger?.LogInformation($"Bridge {bridgeId} ({bridge.Name}) registered");
        }

        /// <summary>
        /// Gets a registered bridge by ID
        /// </summary>
        public II2cBridge GetBridge(string bridgeId)
        {
            if (string.IsNullOrEmpty(bridgeId))
                throw new ArgumentNullException(nameof(bridgeId));

            if (!_bridges.TryGetValue(bridgeId, out var bridge))
            {
                throw new KeyNotFoundException($"Bridge {bridgeId} not found");
            }

            return bridge;
        }

        /// <summary>
        /// Sets the active bridge for operations
        /// </summary>
        public void SetActiveBridge(string bridgeId)
        {
            var bridge = GetBridge(bridgeId);
            _activeBridge = bridge;
            _logger?.LogInformation($"Active bridge set to {bridgeId}");
        }

        /// <summary>
        /// Gets the currently active bridge
        /// </summary>
        public II2cBridge GetActiveBridge()
        {
            if (_activeBridge == null)
            {
                throw new InvalidOperationException("No active bridge has been set");
            }

            return _activeBridge;
        }

        /// <summary>
        /// Gets all registered bridges
        /// </summary>
        public IEnumerable<II2cBridge> GetAllBridges()
        {
            return _bridges.Values;
        }

        /// <summary>
        /// Checks if a bridge is registered
        /// </summary>
        public bool Contains(string bridgeId)
        {
            return _bridges.ContainsKey(bridgeId);
        }

        /// <summary>
        /// Unregisters a bridge
        /// </summary>
        public bool UnregisterBridge(string bridgeId)
        {
            if (string.IsNullOrEmpty(bridgeId))
                throw new ArgumentNullException(nameof(bridgeId));

            if (_activeBridge?.Name == bridgeId || _bridges[bridgeId] == _activeBridge)
            {
                _activeBridge = null;
                _logger?.LogWarning($"Active bridge was unregistered");
            }

            var removed = _bridges.Remove(bridgeId);
            if (removed)
            {
                _logger?.LogInformation($"Bridge {bridgeId} unregistered");
            }

            return removed;
        }

        /// <summary>
        /// Gets the count of registered bridges
        /// </summary>
        public int Count => _bridges.Count;

        /// <summary>
        /// Clears all registered bridges
        /// </summary>
        public void Clear()
        {
            _bridges.Clear();
            _activeBridge = null;
            _logger?.LogInformation("Bridge factory cleared");
        }
    }
}