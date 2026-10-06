using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Zpi.Modules.Identity;
using Zpi.Modules.Care;
using Zpi.Modules.SeniorProfile;
using Zpi.Modules.Safety;
using Zpi.Modules.Routines;
using Zpi.Modules.MedicalRecords;
using Zpi.Modules.Notifications;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCareModule(builder.Configuration);
builder.Services.AddSeniorProfileModule(builder.Configuration);
builder.Services.AddSafetyModule(builder.Configuration);
builder.Services.AddRoutinesModule(builder.Configuration);
builder.Services.AddMedicalRecordsModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();
