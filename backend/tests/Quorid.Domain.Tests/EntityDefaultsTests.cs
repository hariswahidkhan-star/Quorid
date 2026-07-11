using Quorid.Domain.Entities;
using Quorid.Domain.Enums;
using Xunit;

namespace Quorid.Domain.Tests;

public class EntityDefaultsTests
{
    [Fact]
    public void Document_has_id_and_secure_defaults()
    {
        var doc = new Document();

        Assert.NotEqual(Guid.Empty, doc.Id);
        Assert.Equal(DocumentStatus.Draft, doc.Status);
        Assert.Equal(VerificationTier.T1, doc.VerificationTier);
        Assert.Equal(PrivacyLevel.Controlled, doc.PrivacyLevel);
        Assert.False(doc.IsDeleted);
    }

    [Fact]
    public void Archived_document_is_soft_deleted()
    {
        var doc = new Document { Status = DocumentStatus.Archived };

        Assert.True(doc.IsDeleted);
    }

    [Fact]
    public void DataRoom_defaults_enable_security_controls()
    {
        var room = new DataRoom();

        Assert.True(room.NdaRequired);
        Assert.True(room.WatermarkEnabled);
        Assert.True(room.QaEnabled);
        Assert.Equal(RoomStatus.Draft, room.Status);
        Assert.Equal(DownloadPolicy.ViewOnly, room.DownloadPolicy);
    }
}
