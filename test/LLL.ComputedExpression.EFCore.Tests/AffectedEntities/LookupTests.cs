﻿using FluentAssertions;

namespace LLL.Computed.EFCore.Tests.AffectedEntities;

public class LookupTests
{
    [Fact]
    public async void TestLookupKeys()
    {
        using var context = await TestDbContext.Create<PersonDbContext>();

        var pet = context!.Set<Pet>().Find(1)!;
        pet.Type = "Modified";

        var affectedEntities = await context.GetAffectedEntitiesAsync(
            (Person person) => person.Pets.ToLookup(p => p).Where(kv => kv.Key.Type != null).Count());

        affectedEntities.Should().BeEquivalentTo([pet.Owner]);
    }

    [Fact]
    public async void TestLookupValue()
    {
        using var context = await TestDbContext.Create<PersonDbContext>();

        var pet = context!.Set<Pet>().Find(1)!;
        pet.Type = "Modified";

        var affectedEntities = await context.GetAffectedEntitiesAsync(
            (Person person) => person.Pets.ToLookup(p => p.Id).Where(kv => kv.Any(p => p.Type != null)).Count());

        affectedEntities.Should().BeEquivalentTo([pet.Owner]);
    }
}
