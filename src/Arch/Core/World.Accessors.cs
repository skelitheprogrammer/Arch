using System.Diagnostics.Contracts;
using Arch.Core.Extensions.Internal;
using Arch.Core.Utils;

namespace Arch.Core;

public partial class World
{
    /// <summary>
    ///     Sets or replaces a component for an <see cref="Entity"/>.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="component">The instance, optional.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set<T>(Entity entity, in T? component = default)
    {
        var slot = EntityInfo.GetSlot(entity.Id);
        var archetype = EntityInfo.GetArchetype(entity.Id);
        archetype.Set(ref slot, in component);
        OnComponentSet<T>(entity);
    }

    /// <summary>
    ///     Checks if an <see cref="Entity"/> has a certain component.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <returns>True if it has the desired component, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public bool Has<T>(Entity entity)
    {
        var archetype = EntityInfo.GetArchetype(entity.Id);
        return archetype.Has<T>();
    }

    /// <summary>
    ///     Returns a reference to the <typeparamref name="T"/> component of an <see cref="Entity"/>.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <returns>A reference to the <typeparamref name="T"/> component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public ref T Get<T>(Entity entity)
    {
        var slot = EntityInfo.GetSlot(entity.Id);
        var archetype = EntityInfo.GetArchetype(entity.Id);
        return ref archetype.Get<T>(ref slot);
    }

    /// <summary>
    ///     Tries to return a reference to the component of an <see cref="Entity"/>.
    ///     Will copy the component if its a struct.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="component">The found component.</param>
    /// <returns>True if it exists, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public bool TryGet<T>(Entity entity, out T? component)
    {
        component = default;

        var slot = EntityInfo.GetSlot(entity.Id);
        var archetype = EntityInfo.GetArchetype(entity.Id);

        if (!archetype.Has<T>())
        {
            return false;
        }

        component = archetype.Get<T>(ref slot);
        return true;
    }

    /// <summary>
    ///     Tries to return a reference to the component of an <see cref="Entity"/>.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="exists">True if it exists, otherwise false.</param>
    /// <returns>A reference to the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public ref T TryGetRef<T>(Entity entity, out bool exists)
    {
        var slot = EntityInfo.GetSlot(entity.Id);
        var archetype = EntityInfo.GetArchetype(entity.Id);

        if (!(exists = archetype.Has<T>()))
        {
            return ref Unsafe.NullRef<T>();
        }

        return ref archetype.Get<T>(ref slot);
    }

    /// <summary>
    ///     Ensures the existence of an component on an <see cref="Entity"/>.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="component">The component value used if its being added.</param>
    /// <returns>A reference to the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public ref T AddOrGet<T>(Entity entity, T? component = default)
    {
        ref T cmp = ref TryGetRef<T>(entity, out var exists);
        if (exists)
        {
            return ref cmp;
        }

        Add(entity, component);
        return ref Get<T>(entity);
    }

    /// <summary>
    ///     Adds a new component to the <see cref="Entity"/> and moves it to the new <see cref="Archetype"/>.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="newArchetype">The <see cref="Entity"/>'s new <see cref="Archetype"/>.</param>
    /// <param name="slot">The new <see cref="Slot"/> in which the moved <see cref="Entity"/> will land.</param>
    /// <typeparam name="T">The component type.</typeparam>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    internal void Add<T>(Entity entity, out Archetype newArchetype, out Slot slot)
    {
        var oldArchetype = EntityInfo.GetArchetype(entity.Id);
        var type = Component<T>.ComponentType;
        newArchetype = GetOrCreateArchetypeByAddEdge(in type, oldArchetype);

        Move(entity, oldArchetype, newArchetype, out slot);
    }

    /// <summary>
    ///     Adds a new component to the <see cref="Entity"/> and moves it to the new <see cref="Archetype"/>.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <typeparam name="T">The component type.</typeparam>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public void Add<T>(Entity entity)
    {
        Add<T>(entity, out _, out _);
        OnComponentAdded<T>(entity);
    }

    /// <summary>
    ///     Adds a new component to the <see cref="Entity"/> and moves it to the new <see cref="Archetype"/>.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="component">The component instance.</param>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public void Add<T>(Entity entity, in T component)
    {
        Add<T>(entity, out var newArchetype, out var slot);
        newArchetype.Set(ref slot, component);
        OnComponentAdded<T>(entity);
    }

    /// <summary>
    ///     Removes an component from an <see cref="Entity"/> and moves it to a different <see cref="Archetype"/>.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public void Remove<T>(Entity entity)
    {
        var oldArchetype = EntityInfo.GetArchetype(entity.Id);
        var type = Component<T>.ComponentType;
        var newArchetype = GetOrCreateArchetypeByRemoveEdge(in type, oldArchetype);

        OnComponentRemoved<T>(entity);
        Move(entity, oldArchetype, newArchetype, out _);
    }
}

public partial class World
{

    /// <summary>
    ///     Sets or replaces a component for an <see cref="Entity"/>.
    /// </summary>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="component">The component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(Entity entity, object component)
    {
        var entitySlot = EntityInfo.GetEntitySlot(entity.Id);
        entitySlot.Archetype.Set(ref entitySlot.Slot, component);
        OnComponentSet(entity, component);
    }

    /// <summary>
    ///     Sets or replaces a <see cref="Span{T}"/> of components for an <see cref="Entity"/>.
    /// </summary>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="components">The <see cref="Span{T}"/> of components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetRange(Entity entity, Span<object> components)
    {
        var entitySlot = EntityInfo.GetEntitySlot(entity.Id);
        foreach (var cmp in components)
        {
            entitySlot.Archetype.Set(ref entitySlot.Slot, cmp);
            OnComponentSet(entity, cmp);
        }
    }

    /// <summary>
    ///     Checks if an <see cref="Entity"/> has a certain component.
    /// </summary>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="type">The component <see cref="ComponentType"/>.</param>
    /// <returns>True if it has the desired component, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public bool Has(Entity entity, ComponentType type)
    {
        var archetype = EntityInfo.GetArchetype(entity.Id);
        return archetype.Has(type);
    }

    /// <summary>
    ///     Checks if an <see cref="Entity"/> has a certain component.
    /// </summary>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="types">The component <see cref="ComponentType"/>.</param>
    /// <returns>True if it has the desired component, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public bool HasRange(Entity entity, Span<ComponentType> types)
    {
        var archetype = EntityInfo.GetArchetype(entity.Id);
        foreach (var type in types)
        {
            if (!archetype.Has(type))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Returns a reference to the component of an <see cref="Entity"/>.
    /// </summary>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="type">The component <see cref="ComponentType"/>.</param>
    /// <returns>A reference to the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public object? Get(Entity entity, ComponentType type)
    {
        var entitySlot = EntityInfo.GetEntitySlot(entity.Id);
        return entitySlot.Archetype.Get(ref entitySlot.Slot, type);
    }

    /// <summary>
    ///     Returns an array of components of an <see cref="Entity"/>.
    /// </summary>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="types">The component <see cref="ComponentType"/> as a <see cref="Span{T}"/>.</param>
    /// <returns>A reference to the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public object?[] GetRange(Entity entity, Span<ComponentType> types)
    {
        var entitySlot = EntityInfo.GetEntitySlot(entity.Id);
        var array = new object?[types.Length];
        for (var index = 0; index < types.Length; index++)
        {
            var type = types[index];
            array[index] = entitySlot.Archetype.Get(ref entitySlot.Slot, type);
        }

        return array;
    }

    /// <summary>
    ///     Outputs the components of an <see cref="Entity"/>.
    /// </summary>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="types">The component <see cref="ComponentType"/>.</param>
    /// <param name="components">A <see cref="Span{T}"/> in which the components are put.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetRange(Entity entity, Span<ComponentType> types, Span<object?> components)
    {
        var entitySlot = EntityInfo.GetEntitySlot(entity.Id);
        for (var index = 0; index < types.Length; index++)
        {
            var type = types[index];
            components[index] = entitySlot.Archetype.Get(ref entitySlot.Slot, type);
        }
    }

    /// <summary>
    ///     Tries to return a reference to the component of an <see cref="Entity"/>.
    ///     Will copy the component if its a struct.
    /// </summary>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="type">The component <see cref="ComponentType"/>.</param>
    /// <param name="component">The found component.</param>
    /// <returns>True if it exists, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public bool TryGet(Entity entity, ComponentType type, out object? component)
    {
        component = default;
        if (!Has(entity, type))
        {
            return false;
        }

        var entitySlot = EntityInfo.GetEntitySlot(entity.Id);
        component = entitySlot.Archetype.Get(ref entitySlot.Slot, type);
        return true;
    }

    /// <summary>
    ///     Adds a new component to the <see cref="Entity"/> and moves it to the new <see cref="Archetype"/>.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="cmp">The component.</param>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public void Add(Entity entity, in object cmp)
    {
        var oldArchetype = EntityInfo.GetArchetype(entity.Id);
        var type = (ComponentType)cmp.GetType();
        var newArchetype = GetOrCreateArchetypeByAddEdge(in type, oldArchetype);

        Move(entity, oldArchetype, newArchetype, out var slot);
        newArchetype.Set(ref slot, cmp);
        OnComponentAdded(entity, type);
    }

    /// <summary>
    ///     Adds a <see cref="IList{T}"/> of new components to the <see cref="Entity"/> and moves it to the new <see cref="Archetype"/>.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="components">The <see cref="Span{T}"/> of components.</param>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public void AddRange(Entity entity, Span<object> components)
    {
        var oldArchetype = EntityInfo.GetArchetype(entity.Id);

        // BitSet to stack/span bitset, size big enough to contain ALL registered components.
        Span<uint> stack = stackalloc uint[BitSet.RequiredLength(ComponentRegistry.Size)];
        oldArchetype.BitSet.AsSpan(stack);

        // Create a span bitset, doing it local saves us headache and gargabe
        var spanBitSet = new SpanBitSet(stack);
        for (var index = 0; index < components.Length; index++)
        {
            var type = Component.GetComponentType(components[index].GetType());
            spanBitSet.SetBit(type.Id);
        }

        // Get existing or new archetype
        if (!TryGetArchetype(spanBitSet.GetHashCode(), out var newArchetype))
        {
            var newComponents = new ComponentType[components.Length];
            for (var index = 0; index < components.Length; index++)
            {
                newComponents[index] = (ComponentType)components[index].GetType();
            }

            newArchetype = GetOrCreate(oldArchetype.Types.Add(newComponents));
        }

        // Move and fire events
        Move(entity, oldArchetype, newArchetype, out var slot);
        foreach (var cmp in components)
        {
            newArchetype.Set(ref slot, cmp);
            OnComponentAdded(entity, cmp.GetType());
        }
    }

    /// <summary>
    ///     Removes a <see cref="ComponentType"/> from the <see cref="Entity"/> and moves it to a different <see cref="Archetype"/>.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="type">The <see cref="ComponentType"/> to remove from the the <see cref="Entity"/>.</param>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public void Remove(Entity entity, ComponentType type)
    {
        var oldArchetype = EntityInfo.GetArchetype(entity.Id);

        // BitSet to stack/span bitset, size big enough to contain ALL registered components.
        Span<uint> stack = stackalloc uint[oldArchetype.BitSet.Length];
        oldArchetype.BitSet.AsSpan(stack);

        // Create a span bitset, doing it local saves us headache and gargabe
        var spanBitSet = new SpanBitSet(stack);
        spanBitSet.ClearBit(type.Id);

        if (!TryGetArchetype(spanBitSet.GetHashCode(), out var newArchetype))
        {
            newArchetype = GetOrCreate(oldArchetype.Types.Remove(type));
        }

        OnComponentRemoved(entity, type);
        Move(entity, oldArchetype, newArchetype, out _);
    }

    /// <summary>
    ///     Removes a list of <see cref="ComponentType"/>s from the <see cref="Entity"/> and moves it to a different <see cref="Archetype"/>.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="types">A <see cref="Span{T}"/> of <see cref="ComponentType"/>s, that are removed from the <see cref="Entity"/>.</param>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public void RemoveRange(Entity entity, Span<ComponentType> types)
    {
        var oldArchetype = EntityInfo.GetArchetype(entity.Id);

        // BitSet to stack/span bitset, size big enough to contain ALL registered components.
        Span<uint> stack = stackalloc uint[oldArchetype.BitSet.Length];
        oldArchetype.BitSet.AsSpan(stack);

        // Create a span bitset, doing it local saves us headache and gargabe
        var spanBitSet = new SpanBitSet(stack);
        for (var index = 0; index < types.Length; index++)
        {
            ref var cmp = ref types[index];
            spanBitSet.ClearBit(cmp.Id);
        }

        // Get or Create new archetype
        if (!TryGetArchetype(spanBitSet.GetHashCode(), out var newArchetype))
        {
            newArchetype = GetOrCreate(oldArchetype.Types.Remove(types.ToArray()));
        }

        // Fire events and move
        foreach (var type in types)
        {
            OnComponentRemoved(entity, type);
        }

        Move(entity, oldArchetype, newArchetype, out _);
    }
}
