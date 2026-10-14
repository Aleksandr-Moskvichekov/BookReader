using BookReader.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookReader.Data.ModelConfigurations;

public class UserFavoriteConfiguration : IEntityTypeConfiguration<UserFavorite>
{
    public void Configure(EntityTypeBuilder<UserFavorite> builder)
    {
        builder.HasKey(favorite => new { favorite.UserId, favorite.BookId });
        builder.HasIndex(favorite => new { favorite.UserId, favorite.CreatedAt });
        builder.HasIndex(favorite => favorite.BookId);

        builder.HasOne(favorite => favorite.User)
            .WithMany(user => user.Favorites)
            .HasForeignKey(favorite => favorite.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(favorite => favorite.Book)
            .WithMany(book => book.FavoritedBy)
            .HasForeignKey(favorite => favorite.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
