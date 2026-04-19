using Prometheus;

namespace Kriteriom.Credits.API.Metrics;

public static class CreditMetrics
{
    public static readonly Counter CreatedTotal = Prometheus.Metrics.CreateCounter(
        "credits_created_total",
        "Total credit requests created");

    public static readonly Counter ApprovedTotal = Prometheus.Metrics.CreateCounter(
        "credits_approved_total",
        "Total credits approved by risk assessment");

    public static readonly Counter RejectedTotal = Prometheus.Metrics.CreateCounter(
        "credits_rejected_total",
        "Total credits rejected by risk assessment");

    public static readonly Counter UnderReviewTotal = Prometheus.Metrics.CreateCounter(
        "credits_under_review_total",
        "Total credits placed under review by risk assessment");
}
