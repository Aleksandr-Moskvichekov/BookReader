using BookReader.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookReader.Data.ModelConfigurations
{
    public class RatingConfiguration : IEntityTypeConfiguration<Rating>
    {
        public void Configure(EntityTypeBuilder<Rating> builder)
        {
            // один пользователь = одна оценка на книгу
            builder.HasIndex(r => new { r.BookId, r.UserId }).IsUnique();

            builder.HasOne(r => r.Book).WithMany(b => b.Ratings).HasForeignKey(r => r.BookId);
            builder.HasOne(r => r.User).WithMany(u => u.Ratings).HasForeignKey(r => r.UserId);
        }
    }
}
