using System.Globalization;
using System.Text;
using SafeRideKids.Dashboard.Blazor.Models;

namespace SafeRideKids.Dashboard.Blazor.Services;

/// <summary>
/// Geração server-side de CSV para download. Produz CSV RFC4180 com BOM UTF-8
/// (Excel-friendly). Não inclui PII clara — apenas iniciais e hashes.
/// </summary>
public interface ICsvExportService
{
    byte[] BuildCheckInsCsv(IEnumerable<CheckInListItem> items);
    byte[] BuildMetricsSummaryCsv(MetricsSummary summary);
}

public sealed class CsvExportService : ICsvExportService
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    public byte[] BuildCheckInsCsv(IEnumerable<CheckInListItem> items)
    {
        var sb = new StringBuilder();
        // Cabeçalho — separador padrão "," para conformidade RFC4180.
        sb.AppendLine(string.Join(",",
            "id",
            "started_at",
            "finished_at",
            "result",
            "provider_id",
            "confidence",
            "liveness_passed",
            "used_fallback",
            "fallback_type",
            "latency_ms",
            "provider_cost_microcents",
            "child_initials",
            "route_id",
            "route_stop_id",
            "motorista_id_hash"));

        foreach (var item in items)
        {
            sb.AppendLine(string.Join(",",
                Escape(item.Id.ToString()),
                Escape(item.StartedAt.ToString("o", Pt)),
                Escape(item.FinishedAt?.ToString("o", Pt) ?? string.Empty),
                Escape(item.Result ?? string.Empty),
                Escape(item.ProviderId ?? string.Empty),
                Escape(item.Confidence?.ToString("F4", CultureInfo.InvariantCulture) ?? string.Empty),
                Escape(item.LivenessPassed.ToString()),
                Escape(item.UsedFallback.ToString()),
                Escape(item.FallbackType ?? string.Empty),
                Escape(item.LatencyMs?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                Escape(item.ProviderCostMicroCents?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                Escape(item.ChildDisplayInitials),
                Escape(item.RouteId.ToString()),
                Escape(item.RouteStopId.ToString()),
                Escape(item.MotoristaIdHash)));
        }

        return PrependUtf8Bom(sb.ToString());
    }

    public byte[] BuildMetricsSummaryCsv(MetricsSummary summary)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Resumo do periodo");
        sb.AppendLine(string.Join(",", "from", "to", "total_check_ins"));
        sb.AppendLine(string.Join(",",
            Escape(summary.From.ToString("o", Pt)),
            Escape(summary.To.ToString("o", Pt)),
            summary.TotalCheckIns.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine();

        sb.AppendLine("# Distribuicao por outcome");
        sb.AppendLine("outcome,count");
        foreach (var (k, v) in summary.ByOutcome)
        {
            sb.AppendLine($"{Escape(k)},{v}");
        }
        sb.AppendLine();

        sb.AppendLine("# Comparativo por provedor");
        sb.AppendLine(string.Join(",",
            "provider_id",
            "total",
            "approved",
            "inconclusive",
            "rejected",
            "fallback",
            "success_rate",
            "fallback_rate",
            "p50_ms",
            "p95_ms",
            "p99_ms",
            "avg_confidence",
            "cost_usd"));
        foreach (var pm in summary.ProviderBreakdown)
        {
            sb.AppendLine(string.Join(",",
                Escape(pm.ProviderId),
                pm.TotalCheckIns,
                pm.ApprovedCount,
                pm.InconclusiveCount,
                pm.RejectedCount,
                pm.FallbackCount,
                pm.SuccessRate.ToString("F4", CultureInfo.InvariantCulture),
                pm.FallbackRate.ToString("F4", CultureInfo.InvariantCulture),
                pm.P50LatencyMs,
                pm.P95LatencyMs,
                pm.P99LatencyMs,
                pm.AverageConfidence.ToString("F4", CultureInfo.InvariantCulture),
                pm.EstimatedCostUsd.ToString("F4", CultureInfo.InvariantCulture)));
        }
        sb.AppendLine();

        sb.AppendLine("# Serie temporal diaria");
        sb.AppendLine("day,approved,inconclusive,rejected,fallback");
        foreach (var d in summary.DailySeries)
        {
            sb.AppendLine(string.Join(",",
                d.Day.ToString("yyyy-MM-dd"),
                d.Approved,
                d.Inconclusive,
                d.Rejected,
                d.Fallback));
        }

        return PrependUtf8Bom(sb.ToString());
    }

    /// <summary>Escape RFC4180: aspas duplas duplicadas, valor entre aspas se contiver ',' ou '"' ou nova linha.</summary>
    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuoting = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"");
        return needsQuoting ? $"\"{escaped}\"" : escaped;
    }

    private static byte[] PrependUtf8Bom(string content)
    {
        // BOM UTF-8 — ajuda Excel a abrir corretamente acentos/cedilhas.
        var bom = Encoding.UTF8.GetPreamble();
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var result = new byte[bom.Length + contentBytes.Length];
        Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
        Buffer.BlockCopy(contentBytes, 0, result, bom.Length, contentBytes.Length);
        return result;
    }
}
