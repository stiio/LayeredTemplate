using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Web.Attributes;

public class FileResponse : ProducesAttribute
{
    public FileResponse()
        : base(
            MediaTypeNames.Application.Octet)
    {
        this.Type = typeof(FileResult);
    }
}