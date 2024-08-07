namespace LLL.AutoCompute.EntityContexts;

public abstract class EntityContext
{
    private readonly HashSet<IEntityMember> _accessedMembers = [];
    private readonly HashSet<IEntityMember> _allAccessedMembers = [];
    private readonly IList<EntityContext> _childContexts = [];

    public IEnumerable<IEntityMember> AccessedMembers => _accessedMembers;
    public IEnumerable<IEntityMember> AllAccessedMembers => _allAccessedMembers;
    public IEnumerable<EntityContext> ChildContexts => _childContexts;

    public abstract Type EntityType { get; }
    public abstract bool IsTrackingChanges { get; }

    public void RegisterAccessedMember(IEntityMember member)
    {
        _accessedMembers.Add(member);
        OnAccessedMember(member);
    }

    public void OnAccessedMember(IEntityMember member)
    {
        _allAccessedMembers.Add(member);
        NotifyParentsOfAccessedMember(member);
    }

    protected abstract void NotifyParentsOfAccessedMember(IEntityMember member);

    public virtual void RegisterChildContext(EntityContext context)
    {
        _childContexts.Add(context);
    }

    public async Task<IReadOnlyCollection<object>> GetAffectedEntitiesAsync(object input, IncrementalContext incrementalContext)
    {
        var entities = new HashSet<object>();

        foreach (var member in _accessedMembers)
        {
            var ents = await member.GetAffectedEntitiesAsync(input, incrementalContext);
            foreach (var ent in ents)
                entities.Add(ent);
        }

        foreach (var childContext in _childContexts)
        {
            var ents = await childContext.GetParentAffectedEntities(input, incrementalContext);
            foreach (var ent in ents)
                entities.Add(ent);
        }

        return entities;
    }

    public abstract Task<IReadOnlyCollection<object>> GetParentAffectedEntities(object input, IncrementalContext incrementalContext);

    public virtual async Task EnrichIncrementalContextAsync(object input, IReadOnlyCollection<object> entities, IncrementalContext incrementalContext)
    {
        foreach (var childContext in _childContexts)
            await childContext.EnrichIncrementalContextFromParentAsync(input, entities, incrementalContext);
    }

    public virtual async Task EnrichIncrementalContextFromParentAsync(object input, IReadOnlyCollection<object> parentEntities, IncrementalContext incrementalContext)
    {
        await EnrichIncrementalContextAsync(input, parentEntities, incrementalContext);
    }

    public abstract Task EnrichIncrementalContextTowardsRootAsync(object input, IReadOnlyCollection<object> entities, IncrementalContext incrementalContext);

    public virtual async Task PreLoadNavigationsAsync(object input, IReadOnlyCollection<object> entities, IncrementalContext incrementalContext)
    {
        foreach (var childContext in _childContexts)
            await childContext.PreLoadNavigationsFromParentAsync(input, entities, incrementalContext);
    }

    public virtual async Task PreLoadNavigationsFromParentAsync(object input, IReadOnlyCollection<object> parentEntities, IncrementalContext incrementalContext)
    {
        await PreLoadNavigationsAsync(input, parentEntities, incrementalContext);
    }

    public abstract void MarkNavigationToLoadAll();
}
