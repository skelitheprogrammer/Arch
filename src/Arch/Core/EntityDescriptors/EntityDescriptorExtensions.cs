namespace Arch.Core.EntityDescriptors;

public static class EntityDescriptorExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity CreateFromTemplate<T>(this World world) where T : IEntityDescriptor, new()
    {
        if (!world.EntityDescriptorCache.TryGetValue(typeof(T), out IEntityDescriptor descriptor))
        {
            descriptor = new T();
            world.EntityDescriptorCache.Add(typeof(T), descriptor);
        }

        return world.Create(descriptor.Archetype.ToArray());
    }
}
