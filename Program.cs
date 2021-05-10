using System;
using System.Linq;
using System.Threading.Tasks; 
using System.Collections.Generic;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

// WebHost.CreateDefaultBuilder()
//     .Configure(app => app.Run(c => c.Response.WriteAsync("Hello world!")))
//     .Build()
//     .Run();

// WebHost.Start(routes => 
//     routes.MapGet("hello/{name}", (req, res, data) => res.WriteAsync($"Hello, {data.Values["name"]}")));
// Console.ReadKey();

WebHost.CreateDefaultBuilder()
    .ConfigureServices(s => {
        s.AddSingleton<ContactService>();
        s.AddAuthorization(options => {
            options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes("Bearer")
                .RequireAuthenticatedUser()
                .RequireClaim("scope", "read")
                .Build();
        })
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o => {
            o.Authority = "http://localhost:5000/openid";
            o.Audience = "embedded";
            o.RequireHttpsMetadata = false;
        });
        s.AddIdentityServer().AddTestConfig();

    })
    .Configure(app => {
        app.UseRouting();
        app.Map("/openid", id => {
            // use embedded identity server to issue tokens
            id.UseIdentityServer();
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(e => {
            e.MapGet("/", c => c.Response.WriteAsync("Hello world!"));
            e.MapGet("hello/{name}", c => c.Response.WriteAsync($"Hello, {c.Request.RouteValues["name"]}"));

            var contactService = e.ServiceProvider.GetRequiredService<ContactService>();
            e.MapGet("/contacts", async c => await c.Response.WriteAsJsonAsync(await contactService.GetAll()));
            e.MapGet("/contacts/{id:int}", async c => await c.Response.WriteAsJsonAsync(await contactService.Get(int.Parse((string)c.Request.RouteValues["id"]))));
            e.MapPost("/contacts",
                async c =>
                {
                    await contactService.Add(await c.Request.ReadFromJsonAsync<Contact>());
                    c.Response.StatusCode = 201;
                });
            e.MapDelete("/contacts/{id:int}",
                async c =>
                {
                    await contactService.Delete(int.Parse((string)c.Request.RouteValues["id"]));
                    c.Response.StatusCode = 204;
                });
        });
    }).Build().Run();

public record Contact(
    int ContactId,
    string Name,
    string Address,
    string City);

public class ContactService
{
    private readonly List<Contact> _contacts = new List<Contact>
    {
        new Contact(1, "Person One", "1 Main St", "Test"),
        new Contact(2, "Person Two", "2 Main St", "Test"),
        new Contact(3, "Person Three", "3 Main St", "Test"),
        new Contact(4, "Person Four", "4 Main St", "Test"),
        new Contact(5, "Person Five", "5 Main St", "Test"),
    };

    public Task<IEnumerable<Contact>> GetAll() => Task.FromResult(_contacts.AsEnumerable());
    public Task<Contact> Get(int id) => Task.FromResult(_contacts.FirstOrDefault(x => x.ContactId == id));

    public Task<int> Add(Contact contact) 
    {
        var newId = (_contacts.LastOrDefault()?.ContactId ?? 0) + 1;
        _contacts.Add(contact with { ContactId = newId });
        return Task.FromResult(newId);
    }

    public async Task Delete(int id)
    {
        var contact = await Get(id);
        if (contact == null)
        {
            throw new InvalidOperationException(string.Format("Contact with id '{0}' does not exists", id));
        }

        _contacts.Remove(contact);
    }
}