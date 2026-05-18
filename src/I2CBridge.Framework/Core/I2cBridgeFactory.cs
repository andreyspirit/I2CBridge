using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using I2CBridge.Framework.Contracts;

namespace I2CBridge.Framework.Core;

/// <summary>
/// Thread-safe factory for creating and managing I2C bridge instances.
/// 
/// Implements the Factory Pattern with thread-safe access using ReaderWriterLockSlim
/// for optimal performance with concurrent bridge access.
/// 
/// Supports:
/// - Register/Unregister multiple bridge implementations
/// - Query bridges by ID
/// - Set and retrieve active bridge for operations
/// - Thread-safe concurrent access from multiple threads
/// - Automatic cleanup of active bridge reference on unregister
/// </summary>
public class I2cBridgeFactory : IDisposable
{
    private readonly Dictionary<string, II2cBridge> _bridges;
    private readonly ILogger<I2cBridgeFactory>? _logger;
    private readonly ReaderWriterLockSlim _lock;
    private II2cBridge? _activeBridge;
    private bool _disposed;

    /// <summary>
    /// Gets the count of registered bridges.
    /// </summary>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _bridges.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the I2cBridgeFactory class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public I2cBridgeFactory(ILogger<I2cBridgeFactory>? logger = null)
    {
        _bridges = new Dictionary<string, II2cBridge>();
        _logger = logger;
        _lock = new ReaderWriterLockSlim();
    }

    /// <summary>
    /// Registers a bridge implementation.
    /// </summary>
    /// <param name="bridgeId">Unique identifier for the bridge.</param>
    /// <param name="bridge">The bridge implementation to register.</param>
    /// <exception cref="ArgumentException">Thrown if bridgeId is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if bridge is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if factory is disposed.</exception>
    public void RegisterBridge(string bridgeId, II2cBridge bridge)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(bridgeId, nameof(bridgeId));
        ArgumentNullException.ThrowIfNull(bridge, nameof(bridge));

        _lock.EnterWriteLock();
        try
        {
            bool isOverwrite = _bridges.ContainsKey(bridgeId);

            if (isOverwrite)
            {
                _logger?.LogWarning(
                    "Bridge {bridgeId} is already registered. Overwriting with new instance.",
                    bridgeId);
            }

            _bridges[bridgeId] = bridge;

            _logger?.LogInformation(
                "Bridge {bridgeId} ({bridgeName}) registered",
                bridgeId, bridge.Name);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets a registered bridge by ID.
    /// </summary>
    /// <param name="bridgeId">The bridge ID.</param>
    /// <returns>The bridge instance.</returns>
    /// <exception cref="ArgumentException">Thrown if bridgeId is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">Thrown if bridge not found.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if factory is disposed.</exception>
    public II2cBridge GetBridge(string bridgeId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(bridgeId, nameof(bridgeId));

        _lock.EnterReadLock();
        try
        {
            if (!_bridges.TryGetValue(bridgeId, out var bridge))
            {
                throw new KeyNotFoundException($"Bridge '{bridgeId}' not found in factory");
            }

            return bridge;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Sets the active bridge for operations.
    /// </summary>
    /// <param name="bridgeId">The bridge ID to activate.</param>
    /// <exception cref="ArgumentException">Thrown if bridgeId is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">Thrown if bridge not found.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if factory is disposed.</exception>
    public void SetActiveBridge(string bridgeId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(bridgeId, nameof(bridgeId));

        var bridge = GetBridge(bridgeId);

        _lock.EnterWriteLock();
        try
        {
            _activeBridge = bridge;
            _logger?.LogInformation("Active bridge set to {bridgeId}", bridgeId);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets the currently active bridge.
    /// </summary>
    /// <returns>The active bridge instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no active bridge has been set.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if factory is disposed.</exception>
    public II2cBridge GetActiveBridge()
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            if (_activeBridge == null)
            {
                throw new InvalidOperationException(
                    "No active bridge has been set. Call SetActiveBridge() first.");
            }

            return _activeBridge;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Tries to get the currently active bridge.
    /// </summary>
    /// <param name="bridge">The active bridge instance, or null if none set.</param>
    /// <returns>True if an active bridge is set; false otherwise.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if factory is disposed.</exception>
    public bool TryGetActiveBridge(out II2cBridge? bridge)
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            bridge = _activeBridge;
            return _activeBridge != null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets a copy of all registered bridges.
    /// </summary>
    /// <returns>An enumerable of all registered bridges.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if factory is disposed.</exception>
    public IEnumerable<II2cBridge> GetAllBridges()
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            return _bridges.Values.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Checks if a bridge is registered.
    /// </summary>
    /// <param name="bridgeId">The bridge ID.</param>
    /// <returns>True if bridge is registered; false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown if bridgeId is null or empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if factory is disposed.</exception>
    public bool Contains(string bridgeId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(bridgeId, nameof(bridgeId));

        _lock.EnterReadLock();
        try
        {
            return _bridges.ContainsKey(bridgeId);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Unregisters a bridge from the factory.
    /// If the unregistered bridge is the active bridge, clears the active bridge reference.
    /// </summary>
    /// <param name="bridgeId">The bridge ID to unregister.</param>
    /// <returns>True if bridge was unregistered; false if not found.</returns>
    /// <exception cref="ArgumentException">Thrown if bridgeId is null or empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if factory is disposed.</exception>
    public bool UnregisterBridge(string bridgeId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(bridgeId, nameof(bridgeId));

        _lock.EnterWriteLock();
        try
        {
            if (!_bridges.TryGetValue(bridgeId, out var bridge))
            {
                _logger?.LogWarning("Bridge {bridgeId} not found for unregistration", bridgeId);
                return false;
            }

            // Clear active bridge reference if it's being unregistered
            if (_activeBridge == bridge)
            {
                _activeBridge = null;
                _logger?.LogWarning(
                    "Active bridge {bridgeId} was unregistered. Active bridge reference cleared.",
                    bridgeId);
            }

            _bridges.Remove(bridgeId);
            _logger?.LogInformation("Bridge {bridgeId} unregistered", bridgeId);

            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clears all registered bridges and clears the active bridge reference.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if factory is disposed.</exception>
    public void Clear()
    {
        ThrowIfDisposed();

        _lock.EnterWriteLock();
        try
        {
            int count = _bridges.Count;
            _bridges.Clear();
            _activeBridge = null;
            _logger?.LogInformation("Bridge factory cleared ({count} bridges removed)", count);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Releases all resources associated with the factory.
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
            _logger?.LogError(ex, "Error disposing I2cBridgeFactory");
        }
    }

    /// <summary>
    /// Throws ObjectDisposedException if the factory has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name, "I2cBridgeFactory has been disposed");
        }
    }
}
