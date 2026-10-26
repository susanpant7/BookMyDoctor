using BookMyDoctor.Features.Auth;
using BookMyDoctor.Features.Chats;
using BookMyDoctor.Features.Doctors;
using BookMyDoctor.Features.Specialties;
using BookMyDoctor.Persistence;

var builder = WebApplication.CreateBuilder(args);
var clientOrigin = builder.Configuration["Cors:ClientOrigin"] ?? "http://localhost:5173";

builder.Services.AddControllers();
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

builder.Services.AddSingleton<IUsersRepo, UsersRepo>();
builder.Services.AddSingleton<ISpecialtiesRepo, SpecialtiesRepo>();
builder.Services.AddSingleton<IDoctorsRepo, DoctorsRepo>();
builder.Services.AddSingleton<IChatsRepo, ChatsRepo>();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<ISpecialtyService, SpecialtyService>();
builder.Services.AddSingleton<IDoctorService, DoctorService>();
builder.Services.AddSingleton<IChatService, ChatService>();
builder.Services.AddSingleton<IChatAssistant, GreetingChatAssistant>();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.Run();
