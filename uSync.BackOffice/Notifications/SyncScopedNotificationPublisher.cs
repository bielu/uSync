﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Infrastructure.HostedServices;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Services;
using uSync.BackOffice.SyncHandlers;

namespace uSync.BackOffice.Notifications;

internal class SyncScopedNotificationPublisher
    : ScopedNotificationPublisher<INotificationHandler>, IScopedNotificationPublisher
{
    private readonly ILogger<SyncScopedNotificationPublisher> _logger;
    private readonly IEventAggregator _eventAggregator;
    private readonly SyncUpdateCallback _updateCallback;
    private readonly uSyncConfigService _uSyncConfig;
    private readonly uSyncEventService _uSyncEventService;

    private readonly IBackgroundTaskQueue _backgroundTaskQueue;

    public SyncScopedNotificationPublisher(
        IEventAggregator eventAggregator,
        ILogger<SyncScopedNotificationPublisher> logger,
        SyncUpdateCallback callback,
        uSyncConfigService uSyncConfig,
        IBackgroundTaskQueue backgroundTaskQueue,
        uSyncEventService uSyncEventService)
        : base(eventAggregator, false)
    {
        _eventAggregator = eventAggregator;
        _logger = logger;
        _updateCallback = callback;
        _uSyncConfig = uSyncConfig;
        _backgroundTaskQueue = backgroundTaskQueue;
        _uSyncEventService = uSyncEventService;
    }

    bool IScopedNotificationPublisher.PublishCancelable(ICancelableNotification notification)
    {
        SetSingleNotificationState(notification);
        return base.PublishCancelable(notification);
    }

    Task<bool> IScopedNotificationPublisher.PublishCancelableAsync(ICancelableNotification notification)
    {
        SetSingleNotificationState(notification);
        return base.PublishCancelableAsync(notification);
    }

    protected override void PublishScopedNotifications(IList<INotification> notifications)
    {
        if (notifications.Count == 0) return;

        _logger.LogDebug(">> Publishing Notifications [{count}]", notifications.Count);
        var sw = Stopwatch.StartNew();

        SetNotificationStates(notifications);

        var groupedNotifications = notifications
            .Where(x => x != null)
            .GroupBy(x => x.GetType().Name);

        foreach (var items in groupedNotifications)
        {
            if (_uSyncConfig.Settings.BackgroundNotifications is true && _backgroundTaskQueue != null)
            {
                _logger.LogDebug("Pushed {count} notifications into background queue", items.Count());
                _backgroundTaskQueue.QueueBackgroundWorkItem(
                    cancellationToken =>
                    {
                        using (ExecutionContext.SuppressFlow())
                        {
                            Task.Run(() => _eventAggregator.Publish(items), cancellationToken);
                            _logger.LogDebug("Background Events Processed");
                            return Task.CompletedTask;
                        }
                    });
            }
            else
            {
                _updateCallback?.Invoke($"Processing {items.Key}s ({items.Count()})", 90, 100);
                _eventAggregator.Publish(items);
            }
        }

        sw.Stop();
        _logger.LogDebug("<< Notifications processed - {elapsed}ms", sw.ElapsedMilliseconds);

        if (sw.ElapsedMilliseconds / notifications.Count > 2000)
            _logger.LogWarning("Processing notifications is slow, you should check for custom code running on notification events that may slow this down");
    }

    private void SetNotificationStates(IList<INotification> notifications)
    {
        foreach (var notification in notifications)
        {
            SetSingleNotificationState(notification);
        }
    }

    private void SetSingleNotificationState(INotification notification)
    {
        if (notification is StatefulNotification stateful)
        {
            stateful.State[uSync.EventStateKey] = true;
            stateful.State[uSync.EventPausedKey] = _uSyncEventService.IsPaused;
        }
    }
}