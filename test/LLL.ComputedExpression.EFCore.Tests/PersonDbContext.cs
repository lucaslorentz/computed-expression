﻿using Microsoft.EntityFrameworkCore;

namespace LLL.ComputedExpression.EFCore.Tests;

public class Person
{
    public virtual int Id { get; set; }
    public virtual string? FirstName { get; set; }
    public virtual string? LastName { get; set; }
    public virtual string? FullName { get; protected set; }
    public virtual IList<Pet> Pets { get; protected set; } = [];
    public virtual bool HasCats { get; protected set; }
    public virtual string? Description { get; protected set; }
    public virtual int Total { get; protected set; }
    public virtual Pet? FavoritePet { get; set; }
    public virtual IList<Person> Friends { get; protected set; } = [];
    public virtual IList<Person> FriendsInverse { get; protected set; } = [];
}

public class Pet
{
    public virtual int Id { get; set; }
    public virtual string? Color { get; set; }
    public virtual string? Type { get; set; }
    public virtual Person? Owner { get; set; }
    public virtual Person? FavoritePetInverse { get; set; }
}

class PersonDbContext(
    DbContextOptions options,
    Action<ModelBuilder>? customizeModel
) : DbContext(options), ITestDbContext<PersonDbContext>
{
    public Action<ModelBuilder>? CustomizeModel => customizeModel;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var personBuilder = modelBuilder.Entity<Person>();

        personBuilder.HasOne(e => e.FavoritePet)
            .WithOne(e => e.FavoritePetInverse)
            .HasForeignKey<Person>("FavoritePetId");

        personBuilder.HasMany(e => e.Friends)
            .WithMany(e => e.FriendsInverse)
            .UsingEntity<Dictionary<string, object>>(
                l => l.HasOne<Person>("FromPerson").WithMany(),
                r => r.HasOne<Person>("ToPerson").WithMany()
            );

        modelBuilder.Entity<Pet>();

        customizeModel?.Invoke(modelBuilder);
    }

    public static async void SeedData(PersonDbContext dbContext)
    {
        var person1 = new Person
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Pets = {
                new Pet { Id = 1, Type = "Cat" }
            },
        };
        dbContext.Add(person1);

        dbContext.Add(new Person
        {
            Id = 2,
            FirstName = "Jane",
            LastName = "Doe",
            Friends = {
                person1
            }
        });
    }

    public static PersonDbContext Create(
        DbContextOptions options,
        Action<ModelBuilder>? customizeModel)
    {
        return new PersonDbContext(options, customizeModel);
    }
}
