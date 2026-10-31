using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Features.Auth;
using BookMyDoctor.Features.Chats;
using BookMyDoctor.Features.Chats.Tools;
using BookMyDoctor.Features.Common;
using BookMyDoctor.Features.Doctors;
using BookMyDoctor.Features.Knowledge;
using BookMyDoctor.Features.Settings;
using BookMyDoctor.Features.Specialties;
using BookMyDoctor.Features.Users;
using BookMyDoctor.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var clientOrigin = builder.Configuration["Cors:ClientOrigin"] ?? "http://localhost:5173";

builder.Services.AddControllers(options => options.Filters.Add<ServiceExceptionFilter>());
// Detailed errors send the real exception message to the client; only safe in development.
builder.Services.AddSignalR(options => options.EnableDetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(clientOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

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
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));
builder.Services.AddHttpClient<IChatAssistant, OllamaChatAssistant>((services, http) =>
{
    var options = services.GetRequiredService<IOptions<OllamaOptions>>().Value;
    http.BaseAddress = new Uri(options.BaseUrl);
    http.Timeout = TimeSpan.FromMinutes(2);   // a local model can be slow, especially on first load
});
builder.Services.AddHttpClient<IEmbedder, OllamaEmbedder>((services, http) =>
{
    var options = services.GetRequiredService<IOptions<OllamaOptions>>().Value;
    http.BaseAddress = new Uri(options.BaseUrl);
});

Directory.CreateDirectory("data");

var app = builder.Build();

// Creates the tables or brings them up to date. No data is added: the admin loads it on Settings → Manage App Data.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.Run();
