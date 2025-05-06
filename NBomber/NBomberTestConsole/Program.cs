// See https://aka.ms/new-console-template for more information

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using NBomber.CSharp;
using NBomber.Contracts;
using NBomber.Contracts.Stats;

Console.WriteLine("Press any key to start the load test...");
Console.ReadLine();

var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:5246") // Use HTTP to avoid HTTPS cert issues
};

// Scenario 1: Weather Forecast API (localhost:5246/weatherforecast)
var weatherForecastScenario = Scenario.Create("weatherforecast_scenario", async context =>
    {
        try
        {
            var response = await httpClient.GetAsync("/weatherforecast");
            var isSuccess = response.IsSuccessStatusCode;

            var sizeBytes = response.Content.Headers.ContentLength;

            return isSuccess ? Response.Ok(response.StatusCode, sizeBytes: sizeBytes ?? 0) : Response.Fail(response.StatusCode, sizeBytes: sizeBytes ?? 0);
        }
        catch (Exception ex)
        {
            return Response.Fail(message: ex.Message);
        }
    })
    .WithoutWarmUp()
    .WithLoadSimulations(
        Simulation.Inject(rate: 5, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
        Simulation.RampingInject(rate: 5, interval: TimeSpan.FromMilliseconds(100), during: TimeSpan.FromSeconds(5))
    );

// Scenario 2: JSON Placeholder API (jsonplaceholder.typicode.com/posts/1)
var jsonPlaceholderScenario = Scenario.Create("json_placeholder", async context =>
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await httpClient.GetAsync("https://jsonplaceholder.typicode.com/posts/1");
            sw.Stop();

            var contentLength = response.Content.Headers.ContentLength ?? 0;

            return response.IsSuccessStatusCode
                ? Response.Ok(
                    statusCode: ((int)response.StatusCode).ToString(),
                    sizeBytes: contentLength,
                    message: "Success!",
                    customLatencyMs: sw.Elapsed.TotalMilliseconds)
                : Response.Fail(
                    statusCode: ((int)response.StatusCode).ToString(),
                    message: "Request failed",
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
        Simulation.Inject(rate: 5, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(20)));

// Scenario 3: Slow API (httpbin.org/delay/1)
var httpBinScenario = Scenario.Create("httpbin_delay", async context =>
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await httpClient.GetAsync("https://httpbin.org/delay/1");
            sw.Stop();

            return response.IsSuccessStatusCode
                ? Response.Ok(
                    statusCode: ((int)response.StatusCode).ToString(),
                    message: "Delay Success",
                    customLatencyMs: sw.Elapsed.TotalMilliseconds)
                : Response.Fail(
                    statusCode: ((int)response.StatusCode).ToString(),
                    message: "Delay Failed",
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
        Simulation.Inject(rate: 2, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(20)));



NBomberRunner
    .RegisterScenarios(weatherForecastScenario, jsonPlaceholderScenario, httpBinScenario)
    .Run();

Console.WriteLine("Load test completed. Press any key to exit...");
Console.ReadLine();
