using Arch.Core.Utils;

namespace Arch.Core.EntityDescriptors;

public sealed class EntityDescriptor : EntityDescriptorBase
{
    public override ComponentType[] Archetype { get; }

    public EntityDescriptor(ComponentType[] archetype)
    {
        Archetype = archetype;
    }
}
