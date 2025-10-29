using LablabBean.Reporting.Analytics;
using LablabBean.Reporting.Contracts.Models;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace LablabBean.Game.SadConsole.Services;

/// <summary>
/// Service for exporting session reports during gameplay
/// </summary>
public class ReportExportService
{
    private readonly SessionMetricsCollector _metricsCollector;
    private readonly ILogger<ReportExportService> _logger;
    private readonly string _baseReportDirectory;

    public ReportExportService(
        SessionMetricsCollector metricsCollector,
        ILogger<ReportExportService> logger)
    {
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Get version for directory structure
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly.GetName().Version?.ToString()
                   ?? "0.1.0-dev";

        _baseReportDirectory = Path.Combine("build", "_artifacts", version, "reports", "sessions");

        // Ensure directory exists
        Directory.CreateDirectory(_baseReportDirectory);
    }

    /// <summary>
    /// Exports the current session report
    /// </summary>
    /// <param name="format">Report format (HTML or CSV)</param>
    /// <returns>Path to exported report, or null if failed</returns>
    public async Task<string?> ExportSessionReportAsync(ReportFormat format = ReportFormat.HTML)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var extension = format switch
            {
                ReportFormat.HTML => "html",
                ReportFormat.CSV => "csv",
                _ => "html"
            };

            var fileName = $"windows-session-{timestamp}.{extension}";
            var reportPath = Path.Combine(_baseReportDirectory, fileName);

            await _metricsCollector.ExportSessionReportAsync(reportPath, format).ConfigureAwait(false);

            _logger.LogInformation("Session report exported to {Path}", reportPath);
            return reportPath ?? "";  // Return empty string instead of null for consistency
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export session report");
            return "";  // Return empty string on error
        }
    }

    /// <summary>
    /// Exports reports in all formats
    /// </summary>
    /// <returns>Dictionary of format to path</returns>
    public async Task<Dictionary<ReportFormat, string>> ExportAllFormatsAsync()
    {
        var results = new Dictionary<ReportFormat, string>();

        var formats = new[] { ReportFormat.HTML, ReportFormat.CSV };

        foreach (var format in formats)
        {
            var path = await ExportSessionReportAsync(format).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(path))
            {
                results[format] = path;
            }
        }

        return results;
    }

    /// <summary>
    /// Lists all available session reports
    /// </summary>
    public IEnumerable<FileInfo> ListSessionReports()
    {
        if (!Directory.Exists(_baseReportDirectory))
            return Enumerable.Empty<FileInfo>();

        var directory = new DirectoryInfo(_baseReportDirectory);
        return directory.GetFiles("windows-session-*.json")
            .OrderByDescending(f => f.CreationTimeUtc);
    }

    /// <summary>
    /// Gets the latest session report
    /// </summary>
    public FileInfo? GetLatestReport()
    {
        return ListSessionReports().FirstOrDefault();
    }

    /// <summary>
    /// Gets quick stats summary for display
    /// </summary>
    public string GetQuickStats()
    {
        return $"Kills: {_metricsCollector.TotalKills} | " +
               $"Deaths: {_metricsCollector.TotalDeaths} | " +
               $"Items: {_metricsCollector.ItemsCollected} | " +
               $"Levels: {_metricsCollector.LevelsCompleted} | " +
               $"K/D: {_metricsCollector.KDRatio:F2}";
    }
}
