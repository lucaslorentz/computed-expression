﻿using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LLL.ComputedExpression.EFCore.Internal;

public class EFCoreEntityMemberAccessLocator(IModel model) :
    IEntityNavigationAccessLocator<IEFCoreComputedInput>,
    IEntityPropertyAccessLocator<IEFCoreComputedInput>
{
    public virtual IEntityMemberAccess<IEntityNavigation>? GetEntityNavigationAccess(Expression node)
    {
        if (node is MemberExpression memberExpression
            && memberExpression.Expression is not null)
        {
            var type = memberExpression.Expression.Type;
            var entityType = model.FindEntityType(type);
            var navigation = (INavigationBase?)entityType?.FindNavigation(memberExpression.Member)
                ?? entityType?.FindSkipNavigation(memberExpression.Member);
            if (navigation != null)
                return EntityMemberAccess.Create(node, memberExpression.Expression, GetNavigation(navigation));
        }

        return null;
    }

    public virtual IEntityMemberAccess<IEntityProperty>? GetEntityPropertyAccess(Expression node)
    {
        if (node is MemberExpression memberExpression
            && memberExpression.Expression is not null)
        {
            var type = memberExpression.Expression.Type;
            var entityType = model.FindEntityType(type);
            var property = entityType?.FindProperty(memberExpression.Member);
            if (property is not null)
                return EntityMemberAccess.Create(node, memberExpression.Expression, GetProperty(property));
        }

        return null;
    }

    protected virtual IEntityNavigation GetNavigation(INavigationBase navigation)
    {
        return navigation.GetEntityNavigation();
    }

    protected virtual IEntityProperty GetProperty(IProperty property)
    {
        return property.GetEntityProperty();
    }
}
