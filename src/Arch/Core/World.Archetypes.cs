using System.Diagnostics.Contracts;
using Arch.Core.Utils;
using Collections.Pooled;

namespace Arch.Core;
public partial class World
{
    /// <summary>
    ///     Maps a <see cref="Group"/> hash to its <see cref="Archetype"/>.
    /// </summary>
    internal PooledDictionary<int, Archetype> GroupToArchetype { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }

    /// <summary>
    ///     Returns an <see cref="Archetype"/> based on its components. If it does not exist, it will be created.
    /// </summary>
    /// <param name="types">Its <see cref="ComponentType"/>s.</param>
    /// <returns>An existing or new <see cref="Archetype"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Archetype GetOrCreate(Span<ComponentType> types)
    {
        if (TryGetArchetype(types, out var archetype))
        {
            return archetype;
        }

        // Create archetype
        archetype = new Archetype(types.ToArray());
        var hash = Component.GetHashCode(types);

        GroupToArchetype[hash] = archetype;
        Archetypes.Add(archetype);

        // Archetypes always allocate one single chunk upon construction
        Capacity += archetype.EntitiesPerChunk;
        EntityInfo.EnsureCapacity(Capacity);

        return archetype;
    }

    /// <summary>
    ///     Tries to find an <see cref="Archetype"/> by the hash of its components.
    /// </summary>
    /// <param name="hash">Its hash.</param>
    /// <param name="archetype">The found <see cref="Archetype"/>.</param>
    /// <returns>True if found, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    internal bool TryGetArchetype(int hash, [MaybeNullWhen(false)] out Archetype archetype)
    {
        return GroupToArchetype.TryGetValue(hash, out archetype);
    }

    /// <summary>
    ///     Tries to find an <see cref="Archetype"/> by a <see cref="BitSet"/>.
    /// </summary>
    /// <param name="bitset">A <see cref="BitSet"/> indicating the <see cref="Archetype"/> structure.</param>
    /// <param name="archetype">The found <see cref="Archetype"/>.</param>
    /// <returns>True if found, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public bool TryGetArchetype(BitSet bitset, [MaybeNullWhen(false)] out Archetype archetype)
    {
        return TryGetArchetype(bitset.GetHashCode(), out archetype);
    }

    /// <summary>
    ///     Tries to find an <see cref="Archetype"/> by a <see cref="SpanBitSet"/>.
    /// </summary>
    /// <param name="bitset">A <see cref="SpanBitSet"/> indicating the <see cref="Archetype"/> structure.</param>
    /// <param name="archetype">The found <see cref="Archetype"/>.</param>
    /// <returns>True if found, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public bool TryGetArchetype(SpanBitSet bitset, [MaybeNullWhen(false)] out Archetype archetype)
    {
        return TryGetArchetype(bitset.GetHashCode(), out archetype);
    }

    /// <summary>
    ///     Tries to find an <see cref="Archetype"/> by the hash of its components.
    /// </summary>
    /// <param name="types">Its <see cref="ComponentType"/>s.</param>
    /// <param name="archetype">The found <see cref="Archetype"/>.</param>
    /// <returns>True if found, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public bool TryGetArchetype(Span<ComponentType> types, [MaybeNullWhen(false)] out Archetype archetype)
    {
        var hash = Component.GetHashCode(types);
        return TryGetArchetype(hash, out archetype);
    }

    /// <summary>
    ///     Destroys the passed <see cref="Archetype"/> and removes it from this <see cref="World"/>.
    /// </summary>
    /// <param name="archetype">The <see cref="Archetype"/> to destroy.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DestroyArchetype(Archetype archetype)
    {
        var hash = Component.GetHashCode(archetype.Types);
        Archetypes.Remove(archetype);
        GroupToArchetype.Remove(hash);

        // Remove archetype from other archetypes edges.
        foreach (var otherArchetype in this)
        {
            otherArchetype.RemoveEdge(archetype);
        }

        archetype.Clear();
        Capacity -= archetype.EntitiesPerChunk;
    }
}
