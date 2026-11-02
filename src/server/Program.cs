using MyHospitalAI.Features.Appointments;
using MyHospitalAI.Features.Auth;
using MyHospitalAI.Features.Chats;
using MyHospitalAI.Features.Chats.Tools;
using MyHospitalAI.Features.Common;
using MyHospitalAI.Features.Doctors;
using MyHospitalAI.Features.Knowledge;
using MyHospitalAI.Features.Settings;
using MyHospitalAI.Features.Specialties;
using MyHospitalAI.Features.Users;
using MyHospitalAI.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

// The secrets (admin login, token key, AI service keys) come from environment variables. With `dotnet run` they are
// read from the .env file; Docker passes the same file with --env-file.
DotEnv.LoadForDevelopment();

var builder = WebApplication.CreateBuilder(args);

// A client hosted on another address (for example Vercel) needs its address here: Cors:AllowedOrigins, separated by
// commas. With no value, CORS stays off, which is right when the page and the API share an address (Vite dev server).
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// The starting chat and embedding models of a new database (none unless AiDefaults__ variables are set).
SettingDefaults.UseOverrides(
    (builder.Configuration.GetSection(AiDefaultsOptions.SectionName).Get<AiDefaultsOptions>() ?? new()).ToOverrides());

builder.Services.AddControllers(options => options.Filters.Add<ServiceExceptionFilter>());
// Detailed errors send the real exception message to the client; only safe in development.
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.AddFilter<HubTimingFilter>();   // logs how long each chat hub call takes
});
builder.Services.AddJwtAuthentication(builder.Configuration);
if (allowedOrigins.Length > 0)
{
    // The chat hub's connection needs credentials allowed.
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
}

// The repositories are singletons (the chat tools and the hub are too), so each one opens a short-lived context
// from this factory for every call.
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default"))
        .AddInterceptors(new SqliteVecConnectionInterceptor()));

builder.Services.AddSingleton<IUsersRepo, UsersRepo>();
builder.Services.AddSingleton<ISpecialtiesRepo, SpecialtiesRepo>();
builder.Services.AddSingleton<IDoctorsRepo, DoctorsRepo>();
builder.Services.AddSingleton<IChatsRepo, ChatsRepo>();
builder.Services.AddSingleton<IHospitalRepo, HospitalRepo>();
builder.Services.AddSingleton<IDoctorSchedulesRepo, DoctorSchedulesRepo>();
builder.Services.AddSingleton<IAppointmentsRepo, AppointmentsRepo>();
builder.Services.AddSingleton<ISettingsRepo, SettingsRepo>();
builder.Services.AddSingleton<IAiModelsRepo, AiModelsRepo>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<ISpecialtyService, SpecialtyService>();
builder.Services.AddSingleton<IDoctorService, DoctorService>();
builder.Services.AddSingleton<IChatService, ChatService>();
builder.Services.AddSingleton<IAvailabilityService, AvailabilityService>();
builder.Services.AddSingleton<IAppointmentService, AppointmentService>();
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IAiSettingsProvider>(services => services.GetRequiredService<ISettingsService>());
builder.Services.AddSingleton<AiModelsService>();
builder.Services.AddSingleton<DataCleanup>();
builder.Services.AddSingleton<KnowledgeSources>();
builder.Services.AddSingleton<IVectorStore, SqliteVectorStore>();
builder.Services.AddSingleton<IKnowledgeRetriever, KnowledgeRetriever>();
builder.Services.AddSingleton<IKnowledgeService, KnowledgeService>();
builder.Services.AddSingleton<IAssistantTool, GetAvailableSlotsTool>();
builder.Services.AddSingleton<IAssistantTool, GetMyAppointmentsTool>();
builder.Services.AddSingleton<BookingDrafts>();
builder.Services.AddSingleton<IAssistantTool, BookAppointmentTool>();
builder.Services.AddSingleton<ToolTranscripts>();
builder.Services.AddSingleton<ReplyFactChecker>();
// The chat model runs on Ollama or an online provider, chosen on the Settings page. The addresses come from the
// settings, so the HTTP clients have no base address.
builder.Services.AddHttpClient<OllamaChatModel>(http => http.Timeout = TimeSpan.FromMinutes(2));   // a local model can be slow, especially on first load
builder.Services.AddHttpClient<OnlineChatModel>(http => http.Timeout = TimeSpan.FromMinutes(2));
builder.Services.AddTransient<IChatModel, ChatModelRouter>();
builder.Services.AddTransient<IChatAssistant, ChatAssistant>();
builder.Services.AddHttpClient<IEmbedder, Embedder>();
builder.Services.AddHttpClient<AiModelSwitcher>();

Directory.CreateDirectory("data");

var app = builder.Build();

// Creates the tables or brings them up to date. No data is added: the admin loads it on Settings → Manage App Data.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

// Logs each request with its status and time, and the list of endpoints at startup.
app.UseRequestTiming();
app.LogEndpoints();
if (allowedOrigins.Length > 0)
{
    app.UseCors();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy" }));
app.Run();
