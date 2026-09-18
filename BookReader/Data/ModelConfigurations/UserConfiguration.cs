using BookReader.Data.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookReader.Data.ModelConfigurations
{
    public class UserConfiguration
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.Property(u => u.Email).HasMaxLength(256);
            builder.HasIndex(u => u.Email).IsUnique();
        }
    }
}
