using Microsoft.AspNetCore.Mvc;

namespace Utilities.Api.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class FileConversionUploadLimitsAttribute : TypeFilterAttribute
{
    public FileConversionUploadLimitsAttribute()
        : base(typeof(FileConversionUploadLimitsFilter))
    {
        Order = 900;
    }
}
