using Prometheus;

namespace Kriteriom.Notifications.API.Metrics;

public static class NotificationMetrics
{
    public static readonly Counter SentTotal = Prometheus.Metrics.CreateCounter(
        "notifications_sent_total",
        "Total notifications sent successfully",
        new CounterConfiguration { LabelNames = ["event_type"] });

    public static readonly Counter FailedTotal = Prometheus.Metrics.CreateCounter(
        "notifications_failed_total",
        "Total notification delivery failures",
        new CounterConfiguration { LabelNames = ["event_type"] });

    public static readonly Counter CompensatedTotal = Prometheus.Metrics.CreateCounter(
        "notifications_compensated_total",
        "Total compensation events processed",
        new CounterConfiguration { LabelNames = ["result"] });

    public static readonly Counter PermanentlyFailedTotal = Prometheus.Metrics.CreateCounter(
        "notifications_permanently_failed_total",
        "Total notifications permanently failed after all compensation attempts");
}
