using BookReader.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookReader.Data.ModelConfigurations;

public class UserBookHistoryConfiguration : IEntityTypeConfiguration<UserBookHistory>
{
    public void Configure(EntityTypeBuilder<UserBookHistory> builder)
    {
        builder.HasKey(history => new { history.UserId, history.BookId });
        builder.HasIndex(history => new { history.UserId, history.LastAccessedAt });
        builder.HasIndex(history => history.BookId);

        builder.HasOne(history => history.User)
            .WithMany(user => user.BookHistory)
            .HasForeignKey(history => history.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(history => history.Book)
            .WithMany(book => book.UserHistory)
            .HasForeignKey(history => history.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
