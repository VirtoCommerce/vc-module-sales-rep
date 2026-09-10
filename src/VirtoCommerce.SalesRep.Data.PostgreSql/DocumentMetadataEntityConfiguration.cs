using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VirtoCommerce.Platform.Data.PostgreSql.Extensions;
using VirtoCommerce.SalesRep.Data.Models;

namespace VirtoCommerce.SalesRep.Data.PostgreSql;

public class DocumentMetadataEntityConfiguration : IEntityTypeConfiguration<DocumentMetadataEntity>
{
    public void Configure(EntityTypeBuilder<DocumentMetadataEntity> builder)
    {
        builder.Property(x => x.Name).UseCaseInsensitiveCollation();
        builder.Property(x => x.Category).UseCaseInsensitiveCollation();
    }
}
