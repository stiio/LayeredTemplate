namespace LayeredTemplate.Application.Features.Info.Models;

public class InfoResponse
{
    public DateTime? BuildDate { get; set; }

    public string? NpmPackageVersion { get; set; }
}