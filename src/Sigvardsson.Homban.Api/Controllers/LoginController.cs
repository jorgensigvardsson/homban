using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Sigvardsson.Homban.Api.Controllers;

public record Credentials(string Username, string Password);

[ApiController]
[Route("/api/login")]
public class LoginController : Controller
{
    private readonly IConfiguration m_configuration;
    private readonly IHttpContextAccessor m_httpContextAccessor;
    private readonly ILogger<LoginController> m_logger;
    private readonly TokenValidationParameters m_tokenValidationParameters;

    public LoginController(IConfiguration configuration,
                           IHttpContextAccessor httpContextAccessor,
                           ILogger<LoginController> logger,
                           TokenValidationParameters tokenValidationParameters)
    {
        m_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        m_httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        m_logger = logger ?? throw new ArgumentNullException(nameof(logger));
        m_tokenValidationParameters = tokenValidationParameters ?? throw new ArgumentNullException(nameof(tokenValidationParameters));
    }
    
    [AllowAnonymous]
    [HttpPost]
    [Produces("application/json")]
    public async Task<IActionResult> Login([FromBody] Credentials credentials)
    {
        if (credentials.Username != m_configuration["Credentials:Username"] ||
            credentials.Password != m_configuration["Credentials:Password"])
        {
            await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(2)); // Just to thwart any attempts to brute force
            return Unauthorized();
        }

        return Ok(GenerateToken(credentials.Username));
    }

    private const string BearerPrefix = "Bearer ";
    
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost("renew-token")]
    [Produces("application/json")]
    public async Task<IActionResult> RenewToken()
    {
        if (m_httpContextAccessor.HttpContext == null)
            throw new InvalidOperationException("No HttpContext!");

        if (m_httpContextAccessor.HttpContext.Request.Headers.Authorization.Count == 0)
        {
            m_logger.LogError("No authorization header present in request.");
            return Unauthorized("No authorization header present in request.");
        }
        
        if (m_httpContextAccessor.HttpContext.Request.Headers.Authorization.Count > 1)
        {
            m_logger.LogError("More than one authorization header present in request.");
            return Unauthorized("More than one authorization header present in request.");
        }

        var authHeader = m_httpContextAccessor.HttpContext.Request.Headers.Authorization[0];
        if (authHeader == null || !authHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            m_logger.LogError("Missing bearer token in authorization header.");
            return Unauthorized("Missing bearer token in authorization header.");
        }

        var token = authHeader.Remove(0, BearerPrefix.Length).Trim();
        var tokenHandler = new JwtSecurityTokenHandler();
        var claimsPrincipal = tokenHandler.ValidateToken(token, m_tokenValidationParameters, out _);

        if (claimsPrincipal.Identity?.Name == null)
        {
            m_logger.LogError("Identity decoded from token in authorization header does not have name.");
            return Unauthorized("Identity decoded from token in authorization header does not have name.");
        }

        return Ok(GenerateToken(claimsPrincipal.Identity.Name));
    }
    
    private string GenerateToken(string username)
    {
        return GenerateToken(new[]
        {
            new Claim("sub", username)
        });
    }
    
    private string GenerateToken(IEnumerable<Claim> claims)
    {
        var securityKey = new SymmetricSecurityKey(SHA256.HashData(Encoding.UTF8.GetBytes(m_configuration["JwtSigningKey"]!)));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken("Sigvardsson.Homban",
                                         "Sigvardsson.Homban",
                                         claims,
                                         expires: DateTime.UtcNow.AddDays(30),
                                         signingCredentials: credentials);


        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("check")]
    public IActionResult Check()
    {
        return Ok();
    }
}