using BookReader.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookReader.Data.ModelConfigurations
{
    public class BookConfiguration : IEntityTypeConfiguration<Book>
    {
        public void Configure(EntityTypeBuilder<Book> builder)
        {
            builder.HasIndex(b => b.Title);          

            builder.HasOne(b => b.Author)
                   .WithMany(u => u.Books)
                   .HasForeignKey(b => b.AuthorId)
                   .OnDelete(DeleteBehavior.Restrict); 

            builder.HasMany(b => b.Genres)
                   .WithMany(g => g.Books)
                   .UsingEntity("BookGenres");        
        }
    }
}
