using Arch.Core.Utils;

namespace Arch.Core.EntityDescriptors;
public interface IEntityDescriptor
{
    ComponentType[] Archetype { get; }
}
