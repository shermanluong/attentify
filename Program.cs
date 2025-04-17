using GoogleLogin.Models;
using GoogleLogin.CustomPolicy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using GoogleLogin.Services;
using Twilio.Clients;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

builder.Services.AddDbContext<AppIdentityDbContext>(options => options.UseSqlServer(builder.Configuration["ConnectionStrings:GoogleAuthInAspNetCoreMVCContextConnection"]));

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -";
}).AddEntityFrameworkStores<AppIdentityDbContext>().AddDefaultTokenProviders();

builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("AspManager", policy =>
    {
        policy.RequireRole("Manager");
        policy.RequireClaim("Coding-Skill", "ASP.NET Core MVC");
    });
});

builder.Services.AddTransient<IAuthorizationHandler, AllowUsersHandler>();
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("AllowTom", policy =>
    {
        policy.AddRequirements(new AllowUserPolicy("tom"));
    });
});

builder.Services.AddAuthentication()
    .AddCookie()
    .AddGoogle(opts =>
    {
        opts.ClientId     = builder.Configuration["clientId"] ?? "";
        opts.ClientSecret = builder.Configuration["clientSecret"] ?? "";
        opts.SignInScheme = IdentityConstants.ExternalScheme;
        opts.Scope.Add("https://www.googleapis.com/auth/gmail.readonly");
        opts.Scope.Add("https://www.googleapis.com/auth/gmail.modify");
        opts.Scope.Add("https://www.googleapis.com/auth/gmail.send");
        opts.Scope.Add("https://www.googleapis.com/auth/pubsub");
        opts.Scope.Add("profile");
        opts.SaveTokens = true;
    });

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<EMailService>();
builder.Services.AddScoped<EMailTokenService>();
builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<LLMService>();
builder.Services.AddScoped<ShopifyService>();
builder.Services.AddScoped<CompanyService>();
builder.Services.AddScoped<MemberService>();
builder.Services.AddScoped<StripeService>();

builder.Services.AddSingleton(new TwilioRestClient(
            builder.Configuration["Twilio:AccountSid"],
            builder.Configuration["Twilio:AuthToken"]
        ));

builder.Services.AddSession(options =>
{
    options.IdleTimeout         = TimeSpan.FromMinutes(30);    
    options.Cookie.IsEssential  = true;
});
builder.Services.AddSignalR(); 

var app = builder.Build();
var loggerFactory = app.Services.GetService<ILoggerFactory>();
loggerFactory.AddFile(builder.Configuration["Logging:LogFilePath"]?.ToString());

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");    
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();
app.UseSession();
app.MapHub<DataWebsocket>("/ws");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=account}/{action=login}");

app.Run();
