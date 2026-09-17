using System.Windows;
using ANPT.Domain.Entities;

namespace ANPT.UI;

public partial class FindingDetailsWindow : Window
{
    public FindingDetailsWindow(Finding finding)
    {
        InitializeComponent();

        TitleText.Text = finding.Title;
        SeverityText.Text = $"Severity: {finding.Severity}";
        StatusText.Text = $"Status: {finding.Status}";
        RuleText.Text = string.IsNullOrWhiteSpace(finding.RuleId) ? "" : $"Rule: {finding.RuleId}";
        DescriptionText.Text = finding.Description;
        EvidenceText.Text = finding.Evidence ?? "—";
        ImpactText.Text = finding.Impact ?? "—";
        RecommendationText.Text = finding.Recommendation ?? "—";

        var host = finding.Host is null
            ? "—"
            : !string.IsNullOrWhiteSpace(finding.Host.IpAddress)
                ? finding.Host.IpAddress
                : finding.Host.Hostname ?? finding.Host.MacAddress ?? "—";

        ContextText.Text =
            $"Scan: {finding.ScanId:N}\n" +
            $"Host: {host}\n" +
            $"Port: {finding.AffectedPort ?? "—"}\n" +
            $"Service: {finding.AffectedService ?? "—"}\n" +
            $"Created: {finding.CreatedAt:u}\n" +
            $"Confidence: {finding.Confidence:0.00}";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
