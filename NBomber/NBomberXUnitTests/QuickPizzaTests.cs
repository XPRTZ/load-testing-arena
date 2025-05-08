using System;
using System.Net.Http;
using NBomber.CSharp;
using NBomber.Http.CSharp;
using Xunit;
using NBomber.Contracts.Stats;
using Xunit.Abstractions;
using Microsoft.Extensions.Configuration;
using NBomber.Sinks.InfluxDB;
using System.IO;
using System.Linq;

namespace NBomberXUnitTests
{
    public class QuickPizzaTests
    {
        private readonly ITestOutputHelper _outputHelper;

        public QuickPizzaTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public void QuickPizza_Homepage_Should_Meet_Performance_Thresholds()
        {
            var httpClient = new HttpClient();

            var isCi = Environment.GetEnvironmentVariable("CI") == "true";
            string infraConfigPath;

            var token = Environment.GetEnvironmentVariable("INFLUXDB_TOKEN");

            if (!isCi && string.IsNullOrWhiteSpace(token))
            {
                var config = new ConfigurationBuilder()
                    .AddUserSecrets<QuickPizzaTests>()
                    .Build();
                token = config["Token"];
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new Exception("InfluxDB token was not found in env var or user secrets.");
            }

            var json = File.ReadAllText("infra-config.json");
            json = json.Replace("\"Token\": \"REPLACE_ME\"", $"\"Token\": \"{token}\"");

            infraConfigPath = Path.Combine(Path.GetTempPath(), "patched-infra-config.json");
            File.WriteAllText(infraConfigPath, json);

            var influxDbSink = new InfluxDBSink();

            var scenario = Scenario.Create("quickpizza_homepage", async context =>
            {
                var step = await Step.Run("step_1", context, async () =>
                {
                    var request = Http.CreateRequest("GET", "https://quickpizza.grafana.com");
                    var response = await Http.Send(httpClient, request);
                    return response;
                });

                return step;
            })
                .WithWarmUpDuration(TimeSpan.FromSeconds(1))
                .WithLoadSimulations(
                    Simulation.RampingInject(rate: 0, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
                    Simulation.Inject(rate: 20, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(15)),
                    Simulation.RampingInject(rate: 20, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10))
                );

            var postFix = $"XUNIT-{(isCi ? "CI" : "MANUAL")}";
            var sessionId = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{postFix}";
            _outputHelper.WriteLine($"[NBomber] Writing reports to: {Path.GetFullPath("reports")}");

            var stats = NBomberRunner
                .RegisterScenarios(scenario)
                .WithSessionId(sessionId)
                .WithReportFolder("reports")
                .WithReportFormats(ReportFormat.Html, ReportFormat.Md)
                .WithReportingInterval(TimeSpan.FromSeconds(5))
                .WithReportingSinks(influxDbSink)
                .LoadInfraConfig(infraConfigPath)
                .Run();

            var scenarioStats = stats.ScenarioStats.FirstOrDefault(s => s.ScenarioName == "quickpizza_perf");
            Assert.NotNull(scenarioStats);

            var stepStats = scenarioStats.StepStats.FirstOrDefault(s => s.StepName == "step_1");
            Assert.NotNull(stepStats);

            var p95 = stepStats.Ok.Latency.Percent95;
            var failRate = scenarioStats.AllFailCount / (double)scenarioStats.AllRequestCount;

            _outputHelper.WriteLine($"P95 latency: {p95} ms");
            _outputHelper.WriteLine($"Failure rate: {failRate:P2}");

            Assert.True(p95 < 2000, $"P95 latency too high: {p95} ms");
            Assert.True(failRate < 0.02, $"Failure rate too high: {failRate:P2}");
        }
    }
}
