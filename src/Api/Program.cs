using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Zpi.Api;
using Zpi.Api.ErrorHandling;
using Zpi.Modules.Identity;
using Zpi.Modules.Care;
using Zpi.Modules.SeniorProfile;
using Zpi.Modules.Safety;
using Zpi.Modules.Routines;
using Zpi.Modules.MedicalRecords;
using Zpi.Modules.Notifications;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiErrorHandling();
builder.Services.AddApiDocumentation();
builder.Services.AddApiCors(builder.Configuration);
if (builder.Environment.IsDevelopment())
    builder.Services.AddHostedService<DevelopmentDatabaseMigrationService>();

builder.Services.AddIdentityModule(builder.Configuration, builder.Environment);
builder.Services.AddCareModule(builder.Configuration);
builder.Services.AddSeniorProfileModule(builder.Configuration);
builder.Services.AddSafetyModule(builder.Configuration);
builder.Services.AddRoutinesModule(builder.Configuration);
builder.Services.AddMedicalRecordsModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs", options => options.WithTitle("ZPI API"));
}
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();


public partial class Program;
