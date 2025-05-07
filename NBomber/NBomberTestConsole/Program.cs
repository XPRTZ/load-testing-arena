using NBomber.CSharp;
using NBomber.Contracts;
using NBomber.Contracts.Stats;

var httpClient = new HttpClient();

var scenario = Scenario.Create("quickpizza_homepage", async context =>
    {
        var response = await httpClient.GetAsync("https://quickpizza.grafana.com");
        await Task.Delay(1000);
        return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
    })
    .WithoutWarmUp()
    .WithLoadSimulations(
        Simulation.RampingInject(rate: 0, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60)),  // Ramp-up to 20 users
        Simulation.Inject(rate: 20, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(180)),       // Hold at 20 users
        Simulation.RampingInject(rate: 20, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))  // Ramp-down
    )
    .WithThresholds(
        Threshold.Create("quickpizza_homepage", stats => stats.Fail.Request.Percent < 2),        // Fail rate < 2%
        Threshold.Create("quickpizza_homepage", stats => stats.Ok.Latency.Percent95 < 2000)      // p95 latency < 2000ms
    );

NBomberRunner
    .RegisterScenarios(scenario)
    .WithReportFormats(ReportFormat.Html, ReportFormat.Md)
    .Run();
