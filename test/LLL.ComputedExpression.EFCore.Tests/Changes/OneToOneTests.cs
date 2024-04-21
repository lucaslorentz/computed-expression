﻿// using System.Linq.Expressions;
// using FluentAssertions;

// namespace LLL.ComputedExpression.EFCore.Tests.Changes;

// public class OneToOneTests
// {
//     private static readonly Expression<Func<Person, string?>> _computedExpression = (Person person) => person.FavoritePet != null ? person.FavoritePet.Type : null;

//     [Fact]
//     public async void TestReferenceSet()
//     {
//         using var context = await TestDbContext.Create<PersonDbContext>();

//         var person = context!.Set<Person>().Find(1)!;
//         var pet = context!.Set<Pet>().Find(1)!;
//         person.FavoritePet = pet;

//         var changes = await context.GetChangesAsync(_computedExpression);
//         changes.Should().BeEquivalentTo(new Dictionary<Person, ConstValueChange<string?>>{
//             { person, new ConstValueChange<string?>(null, "Cat")}
//         });
//     }

//     [Fact]
//     public async void TestInverseReferenceSet()
//     {
//         using var context = await TestDbContext.Create<PersonDbContext>();

//         var person = context!.Set<Person>().Find(1)!;
//         var pet = context!.Set<Pet>().Find(1)!;
//         pet.FavoritePetInverse = person;

//         var changes = await context.GetChangesAsync(_computedExpression);
//         changes.Should().BeEquivalentTo(new Dictionary<Person, ConstValueChange<string?>>{
//             { person, new ConstValueChange<string?>(null, "Cat")}
//         });
//     }

//     [Fact]
//     public async void TestReferencedEntityModified()
//     {
//         using var context = await TestDbContext.Create<PersonDbContext>();

//         var person = context!.Set<Person>().Find(1)!;
//         var pet = context!.Set<Pet>().Find(1)!;
//         person.FavoritePet = pet;
//         await context.SaveChangesAsync();

//         pet.Type = "Dog";

//         var changes = await context.GetChangesAsync(_computedExpression);
//         changes.Should().BeEquivalentTo(new Dictionary<Person, ConstValueChange<string?>>{
//             { person, new ConstValueChange<string?>("Cat", "Dog")}
//         });
//     }

//     [Fact]
//     public async void TestReferenceUnset()
//     {
//         using var context = await TestDbContext.Create<PersonDbContext>();

//         var person = context!.Set<Person>().Find(1)!;
//         var pet = context!.Set<Pet>().Find(1)!;
//         person.FavoritePet = pet;
//         await context.SaveChangesAsync();

//         person.FavoritePet = null;

//         var changes = await context.GetChangesAsync(_computedExpression);
//         changes.Should().BeEquivalentTo(new Dictionary<Person, ConstValueChange<string?>>{
//             { person, new ConstValueChange<string?>("Cat", null)}
//         });
//     }

//     [Fact]
//     public async void TestReferenceUnsetInverse()
//     {
//         using var context = await TestDbContext.Create<PersonDbContext>();

//         var person = context!.Set<Person>().Find(1)!;
//         var pet = context!.Set<Pet>().Find(1)!;
//         person.FavoritePet = pet;
//         await context.SaveChangesAsync();

//         pet.FavoritePetInverse = null;

//         var changes = await context.GetChangesAsync(_computedExpression);
//         changes.Should().BeEquivalentTo(new Dictionary<Person, ConstValueChange<string?>>{
//             { person, new ConstValueChange<string?>("Cat", null)}
//         });
//     }
// }
