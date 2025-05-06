using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using NBomber.CSharp;
using NBomber.Contracts;

Console.WriteLine("Starting load test...");

var httpClient = new HttpClient();

// Define scenario for GET https://quickpizza.grafana.com/
var quickPizzaScenario = Scenario.Create("quickpizza_homepage", async context =>
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await httpClient.GetAsync("https://quickpizza.grafana.com/");
            sw.Stop();

            var contentLength = response.Content.Headers.ContentLength ?? 0;

            return response.IsSuccessStatusCode
                ? Response.Ok(
                    statusCode: ((int)response.StatusCode).ToString(),
                    sizeBytes: contentLength,
                    message: "Homepage Success",
                    customLatencyMs: sw.Elapsed.TotalMilliseconds)
                : Response.Fail(
                    statusCode: ((int)response.StatusCode).ToString(),
                    message: "Homepage Failed",
                    sizeBytes: contentLength,
                    customLatencyMs: sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Response.Fail<string>(
                message: $"Exception: {ex.Message}",
                customLatencyMs: sw.Elapsed.TotalMilliseconds);
        }
    })
    .WithoutWarmUp()
    .WithLoadSimulations(
        Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
        Simulation.RampingInject(rate: 30, interval: TimeSpan.FromMilliseconds(250), during: TimeSpan.FromSeconds(10))
    );

NBomberRunner
    .RegisterScenarios(quickPizzaScenario)
    .Run();

Console.WriteLine("Load test completed. Press any key to exit...");
Console.ReadLine();