using System.Windows;
using ANPT.Application.Models.Findings;

namespace ANPT.UI;

public partial class FindingDetailsWindow : Window
{
    public FindingDetailsWindow(FindingDetailDto detail)
    {
        InitializeComponent();
        Bind(detail);
    }

    private void Bind(FindingDetailDto d)
    {
        TitleText.Text = d.Title;
        SeverityText.Text = $"Severity: {d.Severity}";
        PriorityText.Text = $"Priority: {d.Priority}";
        StatusText.Text = $"Status: {d.Status}";
        RuleText.Text = string.IsNullOrEmpty(d.RuleId) ? "" : $"Rule: {d.RuleId}";
        CategoryText.Text = string.IsNullOrEmpty(d.Category) ? "" : $"Category: {d.Category}";

        DescriptionText.Text = string.IsNullOrWhiteSpace(d.Description) ? "-" : d.Description;
        RiskText.Text = $"{d.RiskLabel}. {d.RiskRationale}";
        AssetText.Text =
            $"Host: {d.HostDisplay}\n" +
            $"IP: {d.HostIp ?? "-"}\n" +
            $"Hostname: {d.HostHostname ?? "-"}\n" +
            $"MAC: {d.HostMac ?? "-"}";

        var portPart = d.Port.HasValue
            ? $"{(d.Protocol ?? "tcp").ToUpperInvariant()}/{d.Port}"
            : d.AffectedPort ?? "-";
        NetworkText.Text =
            $"Port: {portPart}\n" +
            $"State: {d.PortState ?? "-"}\n" +
            $"Service: {d.ServiceName ?? d.AffectedService ?? "-"}\n" +
            $"Product: {d.Product ?? "-"}\n" +
            $"Version: {d.Version ?? "-"}";

        EvidenceText.Text = string.IsNullOrWhiteSpace(d.Evidence) ? "-" : d.Evidence;
        ImpactText.Text = string.IsNullOrWhiteSpace(d.Impact) ? "-" : d.Impact;
        RecommendationText.Text = string.IsNullOrWhiteSpace(d.Recommendation) ? "-" : d.Recommendation;

        var completed = d.ScanCompletedAt.HasValue
            ? d.ScanCompletedAt.Value.ToString("yyyy-MM-dd HH:mm") + " UTC"
            : "-";
        ScanContextText.Text =
            $"Scan: {d.ScanName}\n" +
            $"Target: {d.TargetName ?? "-"} ({d.TargetAddress ?? "-"})\n" +
            $"Completed: {completed}\n" +
            $"Confidence: {d.Confidence:0.00}";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
