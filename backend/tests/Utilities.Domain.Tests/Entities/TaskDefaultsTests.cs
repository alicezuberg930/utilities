using Utilities.Domain.Constants;
using Utilities.Domain.Entities;
using Xunit;

namespace Utilities.Domain.Tests.Entities;

public sealed class TaskDefaultsTests
{
    [Fact]
    public void ConvertTask_NewEntity_StartsPending()
    {
        var task = new ConvertTask
        {
            OriginalFormat = "dwg",
            ConvertedFormat = "glb",
            DownloadUrl = "/api/v1/convert/file/test",
        };

        Assert.Equal(JobStatuses.Pending, task.Status);
    }

    [Fact]
    public void YoutubeTask_NewEntity_StartsPending()
    {
        var task = new YoutubeTask
        {
            Url = "https://example.com/video",
            Format = YoutubeFormats.Video,
        };

        Assert.Equal(JobStatuses.Pending, task.Status);
    }
}
