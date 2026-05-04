using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<GenerationClient>();
builder.Services.AddSingleton<NotificationClient>();
var app = builder.Build();
app.MapControllers();
app.MapGet("/health", () => "healthy");
app.Run();

namespace SchedulingSvc
{
    public class GenerationClient
    {
        private readonly HttpClient _http = new();
        private readonly string _baseUrl = Environment.GetEnvironmentVariable("GENERATION_SVC_URL") ?? "http://localhost:8081";
        public async Task<string> GetForecastAsync(int plantId) => await _http.GetStringAsync($"{_baseUrl}/api/generation/forecast?plantId={plantId}");
    }

    public class NotificationClient
    {
        private readonly HttpClient _http = new();
        private readonly string _baseUrl = Environment.GetEnvironmentVariable("NOTIFICATION_SVC_URL") ?? "http://localhost:8083";
        public async Task<HttpResponseMessage> SendAlertAsync(string recipient, string subject, string body) =>
            await _http.PostAsJsonAsync($"{_baseUrl}/api/notification/alert", new { Recipient = recipient, Subject = subject, Body = body, AlertType = "schedule" });
    }
}

namespace SchedulingSvc.Controllers
{
    [ApiController]
    [Route("api/scheduling")]
    public class SchedulingController : ControllerBase
    {
        private readonly GenerationClient _generation;
        private readonly NotificationClient _notification;

        public SchedulingController(GenerationClient generation, NotificationClient notification)
        { _generation = generation; _notification = notification; }

        [HttpGet("maintenance-window/{plantId}")]
        public async Task<IActionResult> GetMaintenanceWindow(int plantId)
        {
            var forecast = await _generation.GetForecastAsync(plantId);
            return Ok(new { PlantId = plantId, Forecast = forecast, SuggestedWindow = "02:00-06:00 UTC", GeneratedAt = DateTime.UtcNow });
        }

        [HttpPost("notify-schedule")]
        public async Task<IActionResult> NotifySchedule([FromBody] ScheduleNotification request)
        {
            await _notification.SendAlertAsync(request.Recipient, $"Maintenance scheduled for Plant {request.PlantId}", request.Details);
            return Ok(new { Status = "notified" });
        }
    }
    public class ScheduleNotification { public int PlantId { get; set; } public string Recipient { get; set; } public string Details { get; set; } }
}
