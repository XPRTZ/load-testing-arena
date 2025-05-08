using System;
using System.Net;
using System.Net.Http;
using NBomber.CSharp;
using NBomber.Http.CSharp;
using NBomber.Sinks.InfluxDB;
using Microsoft.Extensions.Configuration;
using Xunit;
using NBomber.Contracts.Stats;
using System.IO;
using Xunit.Abstractions;

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
        public void QuickPizza_Homepage_Should_Return_200_OK()
        {
            var httpClient = new HttpClient();

            var isCi = Environment.GetEnvironmentVariable("CI") == "true";
            string infraConfigPath;

            if (!isCi)
            {
                var config = new ConfigurationBuilder()
                    .AddUserSecrets<QuickPizzaTests>()
                    .Build();

                var token = config["Token"];
                var json = File.ReadAllText("infra-config.json");
                json = json.Replace("\"Token\": \"REPLACE_ME\"", $"\"Token\": \"{token}\"");
                infraConfigPath = Path.Combine(Path.GetTempPath(), "patched-infra-config.json");
                File.WriteAllText(infraConfigPath, json);
            }
            else
            {
                infraConfigPath = "infra-config.json";
            }

            var influxDbSink = new InfluxDBSink();

            var scenario = Scenario.Create("quickpizza_homepage", async context =>
                {
                    var step = await Step.Run("step_1", context, async () =>
                    {
                        var request = Http.CreateRequest("GET", "https://quickpizza.grafana.com");
                        var response = await Http.Send(httpClient, request);

                        Assert.True(
                            Enum.TryParse<HttpStatusCode>(response.StatusCode, out var statusCode) &&
                            statusCode == HttpStatusCode.OK,
                            $"Expected 200 OK, but got: {response.StatusCode}"
                        );

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

            NBomberRunner
                .RegisterScenarios(scenario)
                .WithSessionId(sessionId)
                .WithReportFolder("reports")
                .WithReportFormats(ReportFormat.Html, ReportFormat.Md)
                .WithReportingInterval(TimeSpan.FromSeconds(5))
                .WithReportingSinks(influxDbSink)
                .LoadInfraConfig(infraConfigPath)
                .Run();
        }
    }
}
