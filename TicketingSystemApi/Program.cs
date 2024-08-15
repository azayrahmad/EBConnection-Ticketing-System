using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Org.BouncyCastle.Asn1.Cmp;
using Org.BouncyCastle.Crypto.Generators;
using TicketingSystemApi.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<TicketingContext>(options =>
    options.UseMySQL(builder.Configuration.GetConnectionString("EbConnection"))
);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

builder.Services.AddAuthorization();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PT A Tower Rental Ticketing System",
        Version = "v1",
        Contact = new OpenApiContact() { Name = "Aziz Rahmad", Email = "azayrahmad@gmail.com", Url = new Uri("https://azayrahmad.github.io") }
    });
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });
    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// POST Login
app.MapPost("/login", async (string username, string password, TicketingContext db) =>
{
    var user = await db.Users.SingleOrDefaultAsync(u => u.Username == username);

    if (user != null)
    {
        var decodedPassword = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(user.Password));

        if (password == decodedPassword)
        {
            var token = GenerateJwtToken(user.Username, app.Configuration);
            return Results.Ok(new { Token = token });
        }
    }

    return Results.Unauthorized();
}).WithName("Login");


// POST Create Ticket
app.MapPost("/tickets", async (string ticketTitle, string ticketDescription, TicketingContext db) =>
{
    var ticket = new Ticket
    {
        Title = ticketTitle,
        Description = ticketDescription,
        CreatedAt = DateTime.UtcNow,
        Status = Status.OPEN
    };
    db.Tickets.Add(ticket);
    await db.SaveChangesAsync();
    return Results.Ok(ticket);
})
    .WithName("Create Ticket")
    .RequireAuthorization();

// POST Create Work Order
app.MapPost("/workorders", async (TicketingContext db, int ticketId, string details, string assignedTo, bool isInternal) =>
{
    // Set the default values for the new work order
    var workOrder = new WorkOrder
    {
        TicketId = ticketId,
        Details = details,
        AssignedTo = assignedTo,
        Internal = isInternal,
        CreatedAt = DateTime.UtcNow,
        Status = Status.OPEN
    };

    var ticket = await db.Tickets.FindAsync(workOrder.TicketId);
    if (ticket == null)
    {
        return Results.NotFound("Tiket tidak ditemukan.");
    }

    ticket.Status = Status.ON_PROGRESS;

    db.WorkOrders.Add(workOrder);
    await db.SaveChangesAsync();

    return Results.Ok(workOrder);
})
    .WithName("Create Work Order")
    .RequireAuthorization();

// POST Send Notification
app.MapPost("/workorders/sendnotification", async (TicketingContext db, int workOrderId) =>
{
    var workOrder = await db.WorkOrders.FindAsync(workOrderId);
    if (workOrder == null)
        return Results.NotFound();

    // Proses mengirim notifikasi
    workOrder.NotificationSent = true;

    await db.SaveChangesAsync();
    return Results.Ok(workOrder);
})
    .WithName("Send Notification")
    .RequireAuthorization();

// POST AcceptWO
app.MapPost("/workorders/accept", async (TicketingContext db, int workOrderId) =>
{
    var workOrder = await db.WorkOrders.FindAsync(workOrderId);
    if (workOrder == null)
        return Results.NotFound();

    workOrder.Status = Status.ON_PROGRESS;
    await db.SaveChangesAsync();
    return Results.Ok(workOrder);
})
    .WithName("Accept WO")
    .RequireAuthorization();

// POST DoneWO
app.MapPost("/workorders/done", async (TicketingContext db, int workOrderId) =>
{
    var workOrder = await db.WorkOrders.FindAsync(workOrderId);
    if (workOrder == null)
        return Results.NotFound();

    workOrder.Status = Status.DONE;
    await db.SaveChangesAsync();
    return Results.Ok(workOrder);
})
    .WithName("Done WO")
    .RequireAuthorization();

// POST Done Ticket
app.MapPost("/tickets/done", async (TicketingContext db, int ticketId) =>
{
    var ticket = await db.Tickets.FindAsync(ticketId);
    if (ticket == null)
        return Results.NotFound();

    ticket.Status = Status.DONE;
    await db.SaveChangesAsync();
    return Results.Ok(ticket);
})
    .WithName("Done Ticket")
    .RequireAuthorization();

// GET List Ticket
app.MapGet("/tickets", async (TicketingContext db) =>
{
    return Results.Ok(await db.Tickets.ToListAsync());
})
    .WithName("List Ticket")
    .RequireAuthorization();

// GET List Work Order
app.MapGet("/workorders", async (TicketingContext db) =>
{
    return Results.Ok(await db.WorkOrders.ToListAsync());
})
    .WithName("List Work Order")
    .RequireAuthorization();

app.Run();

string GenerateJwtToken(string username, IConfiguration configuration)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.Name, username),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: configuration["Jwt:Issuer"],
        audience: configuration["Jwt:Audience"],
        claims: claims,
        expires: DateTime.Now.AddHours(3),
        signingCredentials: creds);

    return new JwtSecurityTokenHandler().WriteToken(token);
}

