using NBomber.CSharp;
using NBomber.Http.CSharp;
using NBomber.Contracts;
using NBomber.Contracts.Stats;

var httpClient = new HttpClient();

var failMode = args.Contains("--fail");
Console.WriteLine($"Fail mode: {failMode}");

var scenario = Scenario.Create("quickpizza_homepage", async context =>
    {
        var step1 = await Step.Run("step_1", context, async () =>
        {
            var request = Http.CreateRequest("GET", "https://quickpizza.grafana.com");
            var response = await Http.Send(httpClient, request);

            await Task.Delay(1000);

            return response;
        });

        return step1;
    })
    .WithoutWarmUp()
    .WithLoadSimulations(
        Simulation.RampingInject(rate: 0, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60)),
        Simulation.Inject(rate: 20, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(180)),
        Simulation.RampingInject(rate: 20, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))
    )
    .WithThresholds(
        // Scenario's threshold that checks: error rate < 2%
        Threshold.Create(scenarioStats => scenarioStats.Fail.Request.Percent < 2),

        // Step's threshold that checks: error rate < 2%
        Threshold.Create("step_1", stepStats => stepStats.Fail.Request.Percent < 2),

        // Scenario's threshold that checks: 95th percentile of latency < 2000ms or 20ms if failMode is true
        Threshold.Create(stats => stats.Ok.Latency.Percent95 < (failMode ? 20 : 2000), abortWhenErrorCount: 5)
    );

NBomberRunner
    .RegisterScenarios(scenario)
    .WithReportFormats(ReportFormat.Html, ReportFormat.Md)
    .Run();
