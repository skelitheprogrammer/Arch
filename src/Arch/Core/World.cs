using System.Diagnostics.Contracts;
using System.Threading;
using Arch.Core.Utils;
using Collections.Pooled;
using Schedulers;

namespace Arch.Core;

/// <summary>
///     The <see cref="RecycledEntity"/> struct
///     stores information about a recycled <see cref="Entity"/>: its ID and its version.
/// </summary>
[SkipLocalsInit]
internal record struct RecycledEntity
{
    /// <summary>
    ///     The recycled id.
    /// </summary>
    public int Id;

    /// <summary>
    ///     The new version.
    /// </summary>
    public int Version;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecycledEntity"/> struct.
    /// </summary>
    /// <param name="id">Its ID.</param>
    /// <param name="version">Its version.</param>
    public RecycledEntity(int id, int version)
    {
        Id = id;
        Version = version;
    }
}

/// <summary>
///     The <see cref="IForEach"/> interface
///     provides a method to execute logic on an <see cref="Entity"/>.
/// </summary>
/// <remarks>
///     Commonly used with queries to provide a clean API.
/// </remarks>
public interface IForEach
{
    /// <summary>
    ///     Called on an <see cref="Entity"/> to execute logic on it.
    /// </summary>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(Entity entity);
}

/// <summary>
///     The <see cref="ForEach"/> delegate
///     provides a callback to execute logic on an <see cref="Entity"/>.
/// </summary>
/// <param name="entity">The <see cref="Entity"/>.</param>
public delegate void ForEach(Entity entity);

// Static world, create and destroy
#region Static Create and Destroy
public partial class World
{

    /// <summary>
    ///     A list of all existing <see cref="Worlds"/>.
    ///     Should not be modified by the user.
    /// </summary>
    public static World[] Worlds { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; private set; } = new World[4];

    /// <summary>
    ///     Stores recycled <see cref="World"/> IDs.
    /// </summary>
    private static PooledQueue<int> RecycledWorldIds { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; } = new(8);

    /// <summary>
    ///     Tracks how many <see cref="World"/>s exists.
    /// </summary>
    public static int WorldSize { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] private set; }

    /// <summary>
    ///     The shared static <see cref="JobScheduler"/> used for Multithreading.
    /// </summary>
    public static JobScheduler? SharedJobScheduler { get; set; }

    /// <summary>
    ///     Creates a <see cref="World"/> instance.
    /// </summary>
    /// <returns>The created <see cref="World"/> instance.</returns>
    public static World Create()
    {
#if PURE_ECS
        return new World(-1);
#else
        lock (Worlds)
        {
            var recycle = RecycledWorldIds.TryDequeue(out var id);
            var recycledId = recycle ? id : WorldSize;

            var world = new World(recycledId);

            // If you need to ensure a higher capacity, you can manually check and increase it
            if (recycledId >= Worlds.Length)
            {
                var newCapacity = Worlds.Length * 2;
                var worlds = Worlds;
                Array.Resize(ref worlds, newCapacity);
                Worlds = worlds;
            }

            Worlds[recycledId] = world;
            WorldSize++;
            return world;
        }
#endif
    }

    /// <summary>
    ///     Destroys an existing <see cref="World"/>.
    /// </summary>
    /// <param name="world">The <see cref="World"/> to destroy.</param>
    public static void Destroy(World world)
    {
#if !PURE_ECS
        lock (Worlds)
        {
            Worlds[world.Id] = null!;
            RecycledWorldIds.Enqueue(world.Id);
            WorldSize--;
        }
#endif

        world.Capacity = 0;
        world.Size = 0;

        // Dispose
        world.JobHandles.Dispose();
        world.GroupToArchetype.Dispose();
        world.RecycledIds.Dispose();
        world.QueryCache.Dispose();

        // Set archetypes to null to free them manually since Archetypes are set to ClearMode.Never to fix #65
        for (var index = 0; index < world.Archetypes.Count; index++)
        {
            world.Archetypes[index] = null!;
        }

        world.Archetypes.Dispose();
    }
}

#endregion

// Constructors, properties, disposal
#region World Management

/// <summary>
///     The <see cref="World"/> class
///     stores <see cref="Entity"/>s in <see cref="Archetype"/>s and <see cref="Chunk"/>s, manages them, and provides methods to query for specific <see cref="Entity"/>s.
/// </summary>
/// <remarks>
///     The <see cref="World"/> class is only thread-safe under specific circumstances. Read-only operations like querying entities can be done simultaneously by multiple threads.
///     However, any method which mentions "structural changes" must not run alongside any other methods. Any operation that adds or removes an <see cref="Entity"/>, or changes
///     its <see cref="Archetype"/> counts as a structural change. Structural change methods are also marked with <see cref="StructuralChangeAttribute"/>, to enable source-generators
///     to edit their behavior based on the thread-safety of the method.
/// </remarks>
public partial class World : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="World"/> class.
    /// </summary>
    /// <param name="id">Its unique ID.</param>
    private World(int id)
    {
        Id = id;

        // Mapping.
        GroupToArchetype = new PooledDictionary<int, Archetype>(8);

        // Entity stuff.
        Archetypes = new PooledList<Archetype>(8, ClearMode.Never);
        EntityInfo = new EntityInfoStorage();
        RecycledIds = new PooledQueue<RecycledEntity>(256);

        // Query.
        QueryCache = new PooledDictionary<QueryDescription, Query>(8);

        // Multithreading/Jobs.
        JobHandles = new PooledList<JobHandle>(Environment.ProcessorCount);
        JobsCache = new List<IJob>(Environment.ProcessorCount);
    }

    /// <summary>
    ///     The unique <see cref="World"/> ID.
    /// </summary>
    public int Id { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }

    /// <summary>
    ///     The amount of <see cref="Entity"/>s currently stored by this <see cref="World"/>.
    /// </summary>
    public int Size { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; internal set; }

    /// <summary>
    ///     The available <see cref="Entity"/> capacity of this <see cref="World"/>.
    /// </summary>
    public int Capacity { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; internal set; }

    /// <summary>
    ///     All <see cref="Archetype"/>s that exist in this <see cref="World"/>.
    /// </summary>
    public PooledList<Archetype> Archetypes { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }

    /// <summary>
    ///     Maps an <see cref="Entity"/> to its <see cref="EntityInfo"/> for quick lookup.
    /// </summary>
    internal EntityInfoStorage EntityInfo { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }

    /// <summary>
    ///     Stores recycled <see cref="Entity"/> IDs and their last version.
    /// </summary>
    internal PooledQueue<RecycledEntity> RecycledIds { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }

    /// <summary>
    ///     A cache to map <see cref="QueryDescription"/> to their <see cref="Core.Query"/>, to avoid allocs.
    /// </summary>
    internal PooledDictionary<QueryDescription, Query> QueryCache { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }

    private ReaderWriterLockSlim _queryCacheLock = new();

    /// <summary>
    ///     Reserves space for a certain number of <see cref="Entity"/>s of a given component structure/<see cref="Archetype"/>.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    /// <param name="types">The component structure/<see cref="Archetype"/>.</param>
    /// <param name="amount">The amount of <see cref="Entity"/>s to reserve space for.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public void Reserve(Span<ComponentType> types, int amount)
    {
        var archetype = GetOrCreate(types);
        archetype.Reserve(amount);

        var requiredCapacity = Capacity + amount;
        EntityInfo.EnsureCapacity(requiredCapacity);
        Capacity = requiredCapacity;
    }

    /// <summary>
    ///     Creates a new <see cref="Entity"/> using its given component structure/<see cref="Archetype"/>.
    ///     Might resize its target <see cref="Archetype"/> and allocate new space if its full.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    /// <param name="types">Its component structure/<see cref="Archetype"/>.</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public Entity Create(params ComponentType[] types)
    {
        return Create(types.AsSpan());
    }

    /// <summary>
    ///     Creates a new <see cref="Entity"/> using its given component structure/<see cref="Archetype"/>.
    ///     Might resize its target <see cref="Archetype"/> and allocate new space if its full.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    /// <param name="types">Its component structure/<see cref="Archetype"/>.</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public Entity Create(Span<ComponentType> types)
    {
        // Recycle id or increase
        var recycle = RecycledIds.TryDequeue(out var recycledId);
        var recycled = recycle ? recycledId : new RecycledEntity(Size, 1);

        // Create new entity and put it to the back of the array
        var entity = new Entity(recycled.Id, Id);

        // Add to archetype & mapping
        var archetype = GetOrCreate(types);
        var createdChunk = archetype.Add(entity, out var slot);

        // Resize map & Array to fit all potential new entities
        if (createdChunk)
        {
            Capacity += archetype.EntitiesPerChunk;
            EntityInfo.EnsureCapacity(Capacity);
        }

        // Map
        EntityInfo.Add(entity.Id, recycled.Version, archetype, slot);
        Size++;
        OnEntityCreated(entity);

#if EVENTS
        foreach (ref var type in types)
        {
            OnComponentAdded(entity, type);
        }
#endif

        return entity;
    }

    /// <summary>
    ///     Moves an <see cref="Entity"/> from one <see cref="Archetype"/> <see cref="Slot"/> to another.
    /// </summary>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="source">Its <see cref="Archetype"/>.</param>
    /// <param name="destination">The new <see cref="Archetype"/>.</param>
    /// <param name="destinationSlot">The new <see cref="Slot"/> in which the moved <see cref="Entity"/> will land.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Move(Entity entity, Archetype source, Archetype destination, out Slot destinationSlot)
    {
        // A common mistake, happening in many cases.
        Debug.Assert(source != destination, "From-Archetype is the same as the To-Archetype. Entities cannot move within the same archetype using this function. Probably an attempt was made to attach already existing components to the entity or to remove non-existing ones.");

        // Copy entity to other archetype
        ref var slot = ref EntityInfo.GetSlot(entity.Id);
        var created = destination.Add(entity, out destinationSlot);
        Archetype.CopyComponents(source, ref slot, destination, ref destinationSlot);
        source.Remove(ref slot, out var movedEntity);

        // Update moved entity from the remove
        EntityInfo.Move(movedEntity, slot);
        EntityInfo.Move(entity.Id, destination, destinationSlot);

        // Calculate the entity difference between the moved archetypes to allocate more space accordingly.
        if (created)
        {
            Capacity += destination.EntitiesPerChunk;
            EntityInfo.EnsureCapacity(Capacity);
        }
    }

    /// <summary>
    ///     Destroys an <see cref="Entity"/>.
    ///     Might resize its target <see cref="Archetype"/> and release memory.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public void Destroy(Entity entity)
    {
        #if EVENTS
        // Raise the OnComponentRemoved event for each component on the entity.
        var arch = GetArchetype(entity);
        foreach (var compType in arch.Types)
        {
            OnComponentRemoved(entity, compType);
        }
        #endif

        OnEntityDestroyed(entity);

        // Remove from archetype
        var entityInfo = EntityInfo[entity.Id];
        entityInfo.Archetype.Remove(ref entityInfo.Slot, out var movedEntityId);

        // Update info of moved entity which replaced the removed entity.
        EntityInfo.Move(movedEntityId, entityInfo.Slot);
        EntityInfo.Remove(entity.Id);

        // Recycle id && Remove mapping
        RecycledIds.Enqueue(new RecycledEntity(entity.Id, unchecked(entityInfo.Version + 1)));
        Size--;
    }

    /// <summary>
    ///     Trims this <see cref="World"/> instance and releases unused memory.
    ///     Should not be called every single update or frame.
    ///     One single <see cref="Chunk"/> from each <see cref="Archetype"/> is spared.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public void TrimExcess()
    {
        Capacity = 0;

        // Trim entity info and archetypes
        EntityInfo.TrimExcess();
        for (var index = Archetypes.Count - 1; index >= 0; index--)
        {
            // Remove empty archetypes.
            var archetype = Archetypes[index];
            if (archetype.EntityCount == 0)
            {
                Capacity += archetype.EntitiesPerChunk; // Since the destruction substracts that amount, add it before due to the way we calculate the new capacity.
                DestroyArchetype(archetype);
                continue;
            }

            archetype.TrimExcess();
            Capacity += archetype.ChunkCount * archetype.EntitiesPerChunk; // Since always one chunk always exists.
        }

        // Traverse recycled ids and remove all that are higher than the current capacity.
        // If we do not do this, a new entity might get a id higher than the entityinfo array which causes it to go out of bounds.
        RecycledIds.RemoveWhere(entity => entity.Id >= Capacity);
    }

    /// <summary>
    ///     Clears or resets this <see cref="World"/> instance. Will drop used <see cref="Archetypes"/> and therefore release some memory to the garbage collector.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public void Clear()
    {
        Capacity = 0;
        Size = 0;

        // Clear
        RecycledIds.Clear();
        JobHandles.Clear();
        GroupToArchetype.Clear();
        EntityInfo.Clear();
        QueryCache.Clear();

        // Set archetypes to null to free them manually since Archetypes are set to ClearMode.Never to fix #65
        for (var index = 0; index < Archetypes.Count; index++)
        {
            Archetypes[index] = null!;
        }

        Archetypes.Clear();
    }

    /// <summary>
    ///     Creates a <see cref="Core.Query"/> using a <see cref="QueryDescription"/>
    ///     which can be used to iterate over the matching <see cref="Entity"/>s, <see cref="Archetype"/>s and <see cref="Chunk"/>s.
    /// </summary>
    /// <param name="queryDescription">The <see cref="QueryDescription"/> which specifies which components are searched for.</param>
    /// <returns>The generated <see cref="Core.Query"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public Query Query(in QueryDescription queryDescription)
    {
        // Looping over all archetypes, their chunks and their entities.
        if (QueryCache.TryGetValue(queryDescription, out var query))
        {
            return query;
        }

        query = new Query(Archetypes, queryDescription);
        QueryCache[queryDescription] = query;

        return query;
    }

    /// <summary>
    ///     Counts all <see cref="Entity"/>s that match a <see cref="QueryDescription"/> and returns the number.
    /// </summary>
    /// <param name="queryDescription">The <see cref="QueryDescription"/> which specifies the components or <see cref="Entity"/>s for which to search.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public int CountEntities(in QueryDescription queryDescription)
    {
        var counter = 0;
        var query = Query(in queryDescription);
        foreach (var archetype in query.GetArchetypeIterator())
        {
            var entities = archetype.EntityCount;
            counter += entities;
        }

        return counter;
    }

    /// <summary>
    ///     Searches all matching <see cref="Entity"/>s and puts them into the given <see cref="Span{T}"/>.
    /// </summary>
    /// <param name="queryDescription">The <see cref="QueryDescription"/> which specifies the components or <see cref="Entity"/>s for which to search.</param>
    /// <param name="list">The <see cref="Span{T}"/> receiving the found <see cref="Entity"/>s.</param>
    /// <param name="start">The start index inside the <see cref="Span{T}"/>. Default is 0.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetEntities(in QueryDescription queryDescription, Span<Entity> list, int start = 0)
    {
        var index = 0;
        var query = Query(in queryDescription);
        foreach (ref var chunk in query)
        {
            ref var entityFirstElement = ref chunk.Entity(0);
            foreach (var entityIndex in chunk)
            {
                var entity = Unsafe.Add(ref entityFirstElement, entityIndex);
                list[start + index] = entity;
                index++;
            }
        }
    }

    /// <summary>
    ///     Searches all matching <see cref="Archetype"/>s and puts them into the given <see cref="IList{T}"/>.
    /// </summary>
    /// <param name="queryDescription">The <see cref="QueryDescription"/> which specifies the components for which to search.</param>
    /// <param name="archetypes">The <see cref="Span{T}"/> receiving <see cref="Archetype"/>s containing <see cref="Entity"/>s with the matching components.</param>
    /// <param name="start">The start index inside the <see cref="Span{T}"/>. Default is 0.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetArchetypes(in QueryDescription queryDescription, Span<Archetype> archetypes, int start = 0)
    {
        var index = 0;
        var query = Query(in queryDescription);
        foreach (var archetype in query.GetArchetypeIterator())
        {
            archetypes[start + index] = archetype;
            index++;
        }
    }

    /// <summary>
    ///     Searches all matching <see cref="Chunk"/>s and put them into the given <see cref="IList{T}"/>.
    /// </summary>
    /// <param name="queryDescription">The <see cref="QueryDescription"/> which specifies which components are searched for.</param>
    /// <param name="chunks">The <see cref="Span{T}"/> receiving <see cref="Chunk"/>s containing <see cref="Entity"/>s with the matching components.</param>
    /// <param name="start">The start index inside the <see cref="Span{T}"/>. Default is 0.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetChunks(in QueryDescription queryDescription, Span<Chunk> chunks, int start = 0)
    {
        var index = 0;
        var query = Query(in queryDescription);
        foreach (ref var chunk in query)
        {
            chunks[start + index] = chunk;
            index++;
        }
    }

    /// <summary>
    ///     Creates and returns a new <see cref="Enumerator{T}"/> instance to iterate over all <see cref="Archetype"/>s.
    /// </summary>
    /// <returns>A new <see cref="Enumerator{T}"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public Enumerator<Archetype> GetEnumerator()
    {
        return new Enumerator<Archetype>(Archetypes.Span);
    }

    /// <summary>
    ///     Disposes this <see cref="World"/> instance and removes it from the static <see cref="Worlds"/> list.
    /// </summary>
    /// <remarks>
    ///     Causes a structural change.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [StructuralChange]
    public void Dispose()
    {
        Destroy(this);
        // In case the user (or us) decides to override and provide a finalizer, prevents them from having
        // to re-implement Dispose() to avoid calling it twice.
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Converts this <see cref="World"/> to a human-readable <c>string</c>.
    /// </summary>
    /// <returns>A <c>string</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public override string ToString()
    {
        return $"{GetType().Name} {{ {nameof(Id)} = {Id}, {nameof(Capacity)} = {Capacity}, {nameof(Size)} = {Size} }}";
    }
}

#endregion
