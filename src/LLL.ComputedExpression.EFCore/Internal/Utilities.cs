using System.Collections;
using System.Runtime.CompilerServices;
using LLL.ComputedExpression.EFCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LLL.ComputedExpression.EFCore;

public static class Utilities
{
    public static object? GetOriginalValue(this NavigationEntry navigationEntry)
    {
        var entityEntry = navigationEntry.EntityEntry;

        var dbContext = navigationEntry.EntityEntry.Context;

        var baseNavigation = navigationEntry.Metadata;

        var input = dbContext.GetComputedInput();

        if (baseNavigation.IsCollection)
        {
            var collectionAccessor = baseNavigation.GetCollectionAccessor()!;
            var originalValue = collectionAccessor.Create();

            if (baseNavigation is ISkipNavigation skipNavigation)
            {
                // Add current items that are not new
                foreach (var item in navigationEntry.GetEntities())
                {
                    var itemEntry = dbContext.Entry(item);

                    if (skipNavigation.IsRelationshipNew(input, entityEntry, itemEntry))
                        continue;

                    collectionAccessor.AddStandalone(originalValue, item);
                }

                // Add items that were in the collection but were removed
                var joinReferenceToOther = skipNavigation.Inverse.ForeignKey.DependentToPrincipal;
                foreach (var joinEntry in input.EntityEntriesOfType(skipNavigation.JoinEntityType))
                {
                    var selfReferenceEntry = joinEntry.Reference(skipNavigation.ForeignKey.DependentToPrincipal!);
                    var otherReferenceEntry = joinEntry.Reference(joinReferenceToOther!);
                    if ((joinEntry.State == EntityState.Deleted || selfReferenceEntry.IsModified || otherReferenceEntry.IsModified)
                        && joinEntry.State != EntityState.Added
                        && skipNavigation.ForeignKey.IsConnected(entityEntry.OriginalValues, joinEntry.OriginalValues))
                    {
                        collectionAccessor.AddStandalone(originalValue, otherReferenceEntry.GetOriginalValue()!);
                    }
                }
            }
            else if (baseNavigation is INavigation navigation)
            {
                // Add current items that are not new
                foreach (var item in navigationEntry.GetEntities())
                {
                    var itemEntry = dbContext.Entry(item);

                    if (navigation.IsRelationshipNew(entityEntry, itemEntry))
                        continue;

                    collectionAccessor.AddStandalone(originalValue, item);
                }

                // Add items that were in the collection but were removed
                foreach (var itemEntry in input.EntityEntriesOfType(baseNavigation.TargetEntityType))
                {
                    if (!navigation.IsRelated(entityEntry, itemEntry)
                        && navigation.WasRelated(entityEntry, itemEntry))
                    {
                        collectionAccessor.AddStandalone(originalValue, itemEntry.Entity);
                    }
                }
            }

            return originalValue;
        }
        else if (baseNavigation is INavigation navigation)
        {
            var foreignKey = navigation.ForeignKey;
            if (foreignKey.PrincipalEntityType == entityEntry.Metadata)
            {
                var inverseNavigation = baseNavigation.Inverse
                    ?? throw new Exception($"No inverse to compute original value for navigation '{baseNavigation}'");

                if (entityEntry.State != EntityState.Added)
                {
                    var entityOriginalValues = entityEntry.OriginalValues;

                    // Original value is the current value
                    if (navigationEntry is ReferenceEntry referenceEntry
                        && referenceEntry.TargetEntry is not null
                        && referenceEntry.TargetEntry.State != EntityState.Added
                        && foreignKey.IsConnected(entityOriginalValues, referenceEntry.TargetEntry.OriginalValues))
                    {
                        return navigationEntry.CurrentValue;
                    }

                    // Original value was another value
                    foreach (var itemEntry in input.EntityEntriesOfType(baseNavigation.TargetEntityType))
                    {
                        var inverseReferenceEntry = itemEntry.Reference(inverseNavigation);
                        if (inverseReferenceEntry.IsModified
                            && foreignKey.IsConnected(entityOriginalValues, itemEntry.OriginalValues))
                        {
                            return itemEntry.Entity;
                        }
                    }
                }

                return null;
            }
            else
            {
                var oldKeyValues = foreignKey.Properties
                    .Select(p => entityEntry.OriginalValues[p])
                    .ToArray();

                return entityEntry.Context.Find(
                    baseNavigation.TargetEntityType.ClrType,
                    oldKeyValues);
            }
        }
        else
        {
            throw new NotSupportedException($"Can't get original value of navigation {baseNavigation}");
        }
    }

    public static IReadOnlyCollection<object> GetOriginalEntities(this NavigationEntry navigationEntry)
    {
        var originalValue = navigationEntry.GetOriginalValue();
        if (navigationEntry.Metadata.IsCollection)
        {
            if (originalValue is IEnumerable values)
                return values.OfType<object>().ToArray();
        }
        else if (originalValue is not null)
        {
            return [originalValue];
        }

        return [];
    }

    public static IReadOnlyCollection<object> GetEntities(this NavigationEntry navigationEntry)
    {
        var currentValue = navigationEntry.CurrentValue;
        if (navigationEntry.Metadata.IsCollection)
        {
            if (currentValue is IEnumerable values)
                return values.OfType<object>().ToArray();
        }
        else if (currentValue is not null)
        {
            return [currentValue];
        }

        return [];
    }

    public static IReadOnlyCollection<object> GetModifiedEntities(this NavigationEntry navigationEntry)
    {
        var originalEntities = navigationEntry.EntityEntry.State == EntityState.Added
            ? []
            : navigationEntry.GetOriginalEntities().ToArray();

        var currentEntities = navigationEntry.EntityEntry.State == EntityState.Deleted
            ? []
            : navigationEntry.GetEntities().ToArray();

        return currentEntities.Except(originalEntities)
            .Concat(originalEntities.Except(currentEntities))
            .ToArray();
    }

    private readonly static ConditionalWeakTable<IProperty, IEntityProperty> _entityProperties = [];
    public static IEntityProperty GetEntityProperty(this IProperty property)
    {
        return _entityProperties.GetValue(property, static (property) =>
        {
            var closedType = typeof(EFCoreEntityProperty<>).MakeGenericType(property.DeclaringEntityType.ClrType);
            return (IEntityProperty)Activator.CreateInstance(closedType, property)!;
        });
    }

    private readonly static ConditionalWeakTable<INavigationBase, IEntityNavigation> _entityNavigations = [];
    public static IEntityNavigation GetEntityNavigation(this INavigationBase navigation)
    {
        return _entityNavigations.GetValue(navigation, static (navigation) =>
        {
            var closedType = typeof(EFCoreEntityNavigation<,>).MakeGenericType(navigation.DeclaringEntityType.ClrType, navigation.TargetEntityType.ClrType);
            return (IEntityNavigation)Activator.CreateInstance(closedType, navigation)!;
        });
    }

    public static async Task BulkLoadAsync<TEntity>(this DbContext dbContext, IEnumerable<TEntity> entities, INavigationBase navigation)
        where TEntity : class
    {
        var entitiesToLoad = entities.Where(e =>
        {
            var entityEntry = dbContext.Entry(e);
            if (entityEntry.State == EntityState.Detached)
                return false;

            var navigationEntry = entityEntry.Navigation(navigation);
            return !navigationEntry.IsLoaded;
        }).ToArray();

        if (entitiesToLoad.Any())
        {
            await dbContext.Set<TEntity>()
                .Where(e => entitiesToLoad.Contains(e))
                .IgnoreAutoIncludes()
                .Include(e => EF.Property<object>(e, navigation.Name))
                .AsSingleQuery()
                .LoadAsync();
        }
    }

    public static bool WasRelated(
        this INavigation navigation,
        EntityEntry entry,
        EntityEntry relatedEntry)
    {
        if (entry.State == EntityState.Added
            || relatedEntry.State == EntityState.Added)
            return false;

        var (principalEntry, dependantEntry) = navigation.IsOnDependent
            ? (relatedEntry, entry)
            : (entry, relatedEntry);

        return navigation.ForeignKey.IsConnected(principalEntry.OriginalValues, dependantEntry.OriginalValues);
    }

    public static bool IsRelated(
        this INavigation navigation,
        EntityEntry entry,
        EntityEntry relatedEntry)
    {
        if (entry.State == EntityState.Deleted
            || relatedEntry.State == EntityState.Deleted)
            return false;

        var (principalEntry, dependantEntry) = navigation.IsOnDependent
            ? (relatedEntry, entry)
            : (entry, relatedEntry);

        return navigation.ForeignKey.IsConnected(principalEntry.CurrentValues, dependantEntry.CurrentValues);
    }

    public static bool IsRelationshipNew(
        this INavigation navigation,
        EntityEntry principalEntry,
        EntityEntry dependentEntry
    )
    {
        return !navigation.WasRelated(principalEntry, dependentEntry)
            && navigation.IsRelated(principalEntry, dependentEntry);
    }

    public static bool WasRelated(
        this ISkipNavigation skipNavigation,
        IEFCoreComputedInput input,
        EntityEntry entry,
        EntityEntry relatedEntry)
    {
        var entityValues = entry.CurrentValues;
        var relatedEntityValues = relatedEntry.CurrentValues;
        var foreignKey = skipNavigation.ForeignKey;
        var relatedForeignKey = skipNavigation.Inverse!.ForeignKey;
        foreach (var joinEntry in input.EntityEntriesOfType(skipNavigation.JoinEntityType))
        {
            if (joinEntry.State == EntityState.Added)
                continue;

            var wasRelated = foreignKey.IsConnected(entityValues, joinEntry.OriginalValues)
                && relatedForeignKey.IsConnected(relatedEntityValues, joinEntry.OriginalValues);

            if (wasRelated)
                return true;
        }

        return false;
    }

    public static bool IsRelated(
        this ISkipNavigation skipNavigation,
        IEFCoreComputedInput input,
        EntityEntry entry,
        EntityEntry relatedEntry)
    {
        var entityValues = entry.CurrentValues;
        var relatedEntityValues = relatedEntry.CurrentValues;
        var foreignKey = skipNavigation.ForeignKey;
        var relatedForeignKey = skipNavigation.Inverse!.ForeignKey;
        foreach (var joinEntry in input.EntityEntriesOfType(skipNavigation.JoinEntityType))
        {
            if (joinEntry.State == EntityState.Deleted)
                continue;

            var isRelated = foreignKey.IsConnected(entityValues, joinEntry.CurrentValues)
                && relatedForeignKey.IsConnected(relatedEntityValues, joinEntry.CurrentValues);

            if (isRelated)
                return true;
        }

        return false;
    }

    public static bool IsRelationshipNew(
        this ISkipNavigation skipNavigation,
        IEFCoreComputedInput input,
        EntityEntry entry,
        EntityEntry relatedEntry)
    {
        return !skipNavigation.WasRelated(input, entry, relatedEntry)
            && skipNavigation.IsRelated(input, entry, relatedEntry);
    }

    public static void LoadJoinEntity(
        this ISkipNavigation skipNavigation,
        IEFCoreComputedInput input,
        EntityEntry entry,
        EntityEntry relatedEntry)
    {
        var inverse = skipNavigation.Inverse;

        if (entry.Navigation(skipNavigation).IsLoaded || relatedEntry.Navigation(inverse).IsLoaded)
            return;

        var foreignKey = skipNavigation.ForeignKey;
        var relatedForeignKey = skipNavigation.Inverse.ForeignKey;

        var entityValues = entry.CurrentValues;
        var relatedEntityValues = relatedEntry.CurrentValues;

        foreach (var joinEntry in input.EntityEntriesOfType(skipNavigation.JoinEntityType))
        {
            if (foreignKey.IsConnected(entityValues, joinEntry.OriginalValues)
                && relatedForeignKey.IsConnected(relatedEntityValues, joinEntry.OriginalValues))
                return;
        }

        if (input.LoadedJoinEntities.Add((entry, skipNavigation, relatedEntry)))
        {
            var foreignKeyPrincipalAndDependantProperties = foreignKey.PrincipalKey.Properties
                .Zip(foreignKey.Properties, (p, d) => (Principal: p, Dependant: d));

            var relatedForeignKeyPrincipalAndDependantProperties = relatedForeignKey.PrincipalKey.Properties
                .Zip(relatedForeignKey.Properties, (p, d) => (Principal: p, Dependant: d));

            var query = input.DbContext.QueryEntityType(skipNavigation.JoinEntityType);

            query = foreignKeyPrincipalAndDependantProperties.Aggregate(
                query,
                (c, p) => query.Where(e =>
                    EF.Property<object>(e, p.Dependant.Name).Equals(entry.CurrentValues[p.Principal])
                )
            );

            query = relatedForeignKeyPrincipalAndDependantProperties.Aggregate(
                query,
                (c, p) => query.Where(e =>
                    EF.Property<object>(e, p.Dependant.Name).Equals(relatedEntry.CurrentValues[p.Principal])
                )
            );

            query.Load();
        }
    }

    private static bool IsConnected(
        this IForeignKey foreignKey,
        PropertyValues principalValues,
        PropertyValues dependentValues)
    {
        for (var i = 0; i < foreignKey.PrincipalKey.Properties.Count; i++)
        {
            var principalProperty = foreignKey.PrincipalKey.Properties[i];
            var dependentProperty = foreignKey.Properties[i];

            var principalValue = principalValues[principalProperty];
            var dependentValue = dependentValues[dependentProperty];

            if (!principalProperty.GetKeyValueComparer().Equals(principalValue, dependentValue))
                return false;
        }

        return true;
    }

    public static IQueryable<object> QueryEntityType(this DbContext dbContext, IEntityType entityType)
    {
        var genericSetMethod = typeof(DbContext).GetMethod("Set", 1, [typeof(string)])
            ?? throw new Exception("DbContext generic Set method not found");

        return (IQueryable<object>)genericSetMethod.MakeGenericMethod(entityType.ClrType)
            .Invoke(dbContext, [entityType.Name])!;
    }
}