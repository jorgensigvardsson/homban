using System;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Sigvardsson.Homban.Api.Controllers;

[ApiController]
[Route("/api/resources")]
[AllowAnonymous]
public class ResourcesController : ControllerBase
{
    private readonly string m_backdropPath;
    
    public ResourcesController(IConfiguration configuration)
    {
        m_backdropPath = configuration["BackdropPath"] ?? throw new ApplicationException("No BackdropPath configuration.");
    }
    
    [HttpGet("backdrop")]
    public FileResult Backdrop()
    {
        return new PhysicalFileResult(m_backdropPath, MediaTypeOf(m_backdropPath));
    }

    private string MediaTypeOf(string filePath)
    {
        if (Path.GetExtension(filePath).Equals(".png", StringComparison.OrdinalIgnoreCase))
            return "image/png";
        if (Path.GetExtension(filePath).Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(filePath).Equals(".png", StringComparison.OrdinalIgnoreCase))
            return "image/jpeg";

        return "application/octet-stream";
    }
}