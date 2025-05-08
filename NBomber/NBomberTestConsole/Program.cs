using NBomber.CSharp;
using NBomber.Http.CSharp;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.Sinks.InfluxDB;
using Microsoft.Extensions.Configuration;

var httpClient = new HttpClient();

#if DEBUG
var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var token = config["Token"];
var json = File.ReadAllText("infra-config.json");

// replace the token directly in memory
json = json.Replace("\"Token\": \"REPLACE_ME\"", $"\"Token\": \"{token}\"");

// write to a temporary file (optional) or parse it as JSON
var infraConfigPath = Path.Combine(Path.GetTempPath(), "patched-infra-config.json");
File.WriteAllText(infraConfigPath, json);
#else
var tempPath = "infra-config.json";
#endif

var influxDbSink = new InfluxDBSink();

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
        Threshold.Create(scenarioStats => scenarioStats.Fail.Request.Percent < 2),
        Threshold.Create("step_1", stepStats => stepStats.Fail.Request.Percent < 2),
        Threshold.Create(stats => stats.Ok.Latency.Percent95 < 2000, abortWhenErrorCount: 5)
    );

NBomberRunner
    .RegisterScenarios(scenario)
    .WithReportFormats(ReportFormat.Html, ReportFormat.Md)
    .WithReportingInterval(TimeSpan.FromSeconds(5))
    .WithReportingSinks(influxDbSink)
    .LoadInfraConfig(infraConfigPath)
    .Run();