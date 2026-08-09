using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Plugin.ApiTest;

public sealed class ApiProbePage : Page
{
    private readonly Button _runButton;
    private readonly TextBlock _status;
    private readonly TextBlock _summary;
    private readonly TextBlock _report;

    public ApiProbePage()
    {
        _runButton = new Button
        {
            Content = "Run API probe",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetAutomationId(_runButton, "PluginApiProbe_RunButton");
        _runButton.Click += RunButtonOnClick;

        _status = new TextBlock { Text = ProbeState.Status, TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetAutomationId(_status, "PluginApiProbe_Status");

        _summary = new TextBlock
        {
            Text = ProbeState.Summary,
            Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style,
        };
        AutomationProperties.SetAutomationId(_summary, "PluginApiProbe_Summary");

        _report = new TextBlock
        {
            Text = ProbeState.Report,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetAutomationId(_report, "PluginApiProbe_Report");

        StackPanel content = new()
        {
            Spacing = 12,
            Margin = new Thickness(24),
            Children =
            {
                new TextBlock
                {
                    Text = "Plugin API Probe",
                    Style = Application.Current.Resources["TitleTextBlockStyle"] as Style,
                },
                _runButton,
                _status,
                _summary,
                new ScrollViewer
                {
                    MaxHeight = 560,
                    Content = _report,
                },
            },
        };
        AutomationProperties.SetAutomationId(content, "PluginApiProbeRoot");

        Content = content;
        Loaded += (_, _) =>
        {
            ProbeState.Changed += Refresh;
            Refresh();
        };
        Unloaded += (_, _) => ProbeState.Changed -= Refresh;
    }

    private async void RunButtonOnClick(object sender, RoutedEventArgs e)
    {
        _runButton.IsEnabled = false;
        try
        {
            await new ApiProbeRunner(ApiProbePlugin.HostApi).RunAsync();
        }
        finally
        {
            _runButton.IsEnabled = true;
        }
    }

    private void Refresh()
    {
        _status.Text = ProbeState.Status;
        _summary.Text = ProbeState.Summary;
        _report.Text = ProbeState.Report;
    }
}

internal static class ProbeState
{
    private static readonly List<ProbeResult> Results = [];

    internal static event Action? Changed;
    internal static string Status { get; private set; } = "Ready";
    internal static string Summary { get; private set; } = "Not run";
    internal static string Report => string.Join(Environment.NewLine,
        Results.Select(result => $"{(result.Passed ? "PASS" : "FAIL")} | {result.Api} | {result.Detail}"));

    internal static void Reset()
    {
        Results.Clear();
        Status = "Running";
        Summary = "0/0";
        Changed?.Invoke();
    }

    internal static void SetStatus(string status)
    {
        Status = status;
        Changed?.Invoke();
    }

    internal static void Add(ProbeResult result)
    {
        Results.Add(result);
        int passed = Results.Count(item => item.Passed);
        Summary = $"{passed}/{Results.Count}";
        Changed?.Invoke();
    }

    internal static void Complete()
    {
        int passed = Results.Count(item => item.Passed);
        Status = "Completed";
        Summary = passed == Results.Count
            ? $"PASS {passed}/{Results.Count}"
            : $"FAIL {passed}/{Results.Count}";
        Changed?.Invoke();
    }
}

internal sealed record ProbeResult(string Api, bool Passed, string Detail);
