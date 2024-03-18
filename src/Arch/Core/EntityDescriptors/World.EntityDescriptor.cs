using Arch.Core.EntityDescriptors;
using Arch.Core.Utils;

namespace Arch.Core;

public partial class World
{
    internal Dictionary<Type, IEntityDescriptor> EntityDescriptorCache = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity CreateFromTemplate(IEntityDescriptor descriptor)
    {
        if (!EntityDescriptorCache.ContainsValue(descriptor))
        {
            EntityDescriptorCache.Add(descriptor.GetType(), descriptor);
        }

        return Create(descriptor.Archetype);
    }
}
