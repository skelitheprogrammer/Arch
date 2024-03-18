using Arch.Core.Utils;

namespace Arch.Core.EntityDescriptors;

public abstract class EntityDescriptorBase : IEntityDescriptor
{
    public abstract ComponentType[] Archetype { get; }
}
