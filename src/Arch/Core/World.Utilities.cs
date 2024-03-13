using System.Diagnostics.Contracts;
using Arch.Core;

namespace Arch.Core
{
    public partial class World
    {
        /// <summary>
        ///     Checks if the <see cref="Entity"/> is alive in this <see cref="World"/>.
        /// </summary>
        /// <param name="entity">The <see cref="Entity"/>.</param>
        /// <returns>True if it exists and is alive, otherwise false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public bool IsAlive(Entity entity)
        {
            return EntityInfo.Has(entity.Id);
        }

        /// <summary>
        ///     Checks if the <see cref="EntityReference"/> is alive and valid in this <see cref="World"/>.
        /// </summary>
        /// <param name="entityReference">The <see cref="EntityReference"/>.</param>
        /// <returns>True if it exists and is alive, otherwise false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public bool IsAlive(EntityReference entityReference)
        {
            if (entityReference == EntityReference.Null)
            {
                return false;
            }

            var reference = Reference(entityReference.Entity);
            return entityReference == reference;
        }

        /// <summary>
        ///     Returns the version of an <see cref="Entity"/>.
        ///     Indicating how often it was recycled.
        /// </summary>
        /// <param name="entity">The <see cref="Entity"/>.</param>
        /// <returns>Its version.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int Version(Entity entity)
        {
            return EntityInfo.GetVersion(entity.Id);
        }

        /// <summary>
        ///     Returns a <see cref="EntityReference"/> to an <see cref="Entity"/>.
        /// </summary>
        /// <param name="entity">The <see cref="Entity"/>.</param>
        /// <returns>Its <see cref="EntityReference"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public EntityReference Reference(Entity entity)
        {
            var entityInfo = EntityInfo.TryGetVersion(entity.Id, out var version);
            return entityInfo ? new EntityReference(in entity, version) : EntityReference.Null;
        }

        /// <summary>
        ///     Returns the <see cref="Archetype"/> of an <see cref="Entity"/>.
        /// </summary>
        /// <param name="entity">The <see cref="Entity"/>.</param>
        /// <returns>Its <see cref="Archetype"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public Archetype GetArchetype(Entity entity)
        {
            return EntityInfo.GetArchetype(entity.Id);
        }

        /// <summary>
        ///     Returns the <see cref="Chunk"/> of an <see cref="Entity"/>.
        /// </summary>
        /// <param name="entity">The <see cref="Entity"/>.</param>
        /// <returns>A reference to its <see cref="Chunk"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public ref readonly Chunk GetChunk(Entity entity)
        {
            var entityInfo = EntityInfo.GetEntitySlot(entity.Id);
            return ref entityInfo.Archetype.GetChunk(entityInfo.Slot.ChunkIndex);
        }

        /// <summary>
        ///     Returns all <see cref="ComponentType"/>s of an <see cref="Entity"/>.
        /// </summary>
        /// <param name="entity">The <see cref="Entity"/>.</param>
        /// <returns>Its array of <see cref="ComponentType"/>s.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public ComponentType[] GetComponentTypes(Entity entity)
        {
            var archetype = EntityInfo.GetArchetype(entity.Id);
            return archetype.Types;
        }

        /// <summary>
        ///     Returns all components of an <see cref="Entity"/> as an array.
        ///     Will allocate memory.
        /// </summary>
        /// <param name="entity">The <see cref="Entity"/>.</param>
        /// <returns>A newly allocated array containing the entities components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public object?[] GetAllComponents(Entity entity)
        {
            // Get archetype and chunk.
            var entitySlot = EntityInfo.GetEntitySlot(entity.Id);
            var archetype = entitySlot.Archetype;
            ref var chunk = ref archetype.GetChunk(entitySlot.Slot.ChunkIndex);
            var components = chunk.Components;

            // Loop over components, collect and returns them.
            var entityIndex = entitySlot.Slot.Index;
            var cmps = new object?[components.Length];

            for (var index = 0; index < components.Length; index++)
            {
                var componentArray = components[index];
                var component = componentArray.GetValue(entityIndex);
                cmps[index] = component;
            }

            return cmps;
        }
    }
}

