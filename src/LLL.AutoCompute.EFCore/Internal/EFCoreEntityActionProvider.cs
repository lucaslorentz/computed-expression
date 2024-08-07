namespace LLL.AutoCompute.EFCore.Internal;

public class EFCoreEntityActionProvider : IEntityActionProvider<IEFCoreComputedInput>
{
    public EntityAction GetEntityAction(IEFCoreComputedInput input, object entity)
    {
        return input.DbContext.Entry(entity).State switch
        {
            Microsoft.EntityFrameworkCore.EntityState.Added => EntityAction.Create,
            Microsoft.EntityFrameworkCore.EntityState.Deleted => EntityAction.Delete,
            Microsoft.EntityFrameworkCore.EntityState.Detached => EntityAction.Delete,
            _ => EntityAction.None
        };
    }
}