using Arch.Core;
using Arch.Core.EntityDescriptors;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using static NUnit.Framework.Assert;

namespace Arch.Tests;

[TestFixture]
internal sealed class EntityDescriptorsTest
{
    private World _world;

    [SetUp]
    public void Setup()
    {
        _world = World.Create();
    }

    [TearDown]
    public void TearDown()
    {
        World.Destroy(_world);
    }

    [Test]
    public void CollectionInstanceExists()
    {
        That(_world.EntityDescriptorCache is not null, Is.True);
    }

    [Test]
    public void CreateEntityDescriptorAndCheckContains()
    {
        IEntityDescriptor entityDescriptor = new TestEntityDescriptor();

        That(entityDescriptor.Archetype.Contains(typeof(TestTag)), Is.True);
    }

    [Test]
    public void CreateEntityDescriptorAndCheckCache()
    {
        IEntityDescriptor descriptor = new TestEntityDescriptor();
        _world.CreateFromTemplate(descriptor);

        That(_world.EntityDescriptorCache.ContainsValue(descriptor), Is.True);
    }

    [Test]
    public void CreateEntityDescriptorGenericAndCheckCache()
    {
        _world.CreateFromTemplate<TestEntityDescriptor>();

        That(_world.EntityDescriptorCache.ContainsKey(typeof(TestEntityDescriptor)), Is.True);
    }

    [Test]
    public void CreateEntityFromTemplate()
    {
        IEntityDescriptor descriptor = new TestEntityDescriptor();

        Entity entity = _world.CreateFromTemplate(descriptor);
        Multiple(() =>
        {
            That(entity.IsAlive(), Is.True, "Is the entity alive?");
            That(entity.Has<TestTag>(), Is.True, "If an entity has a tag?");
        });
    }

    [Test]
    public void CreateEntityFromGenericTemplate()
    {
        Entity entity = _world.CreateFromTemplate<TestEntityDescriptor>();
        bool hasTag = entity.Has<TestTag>();

        Multiple(() =>
        {
            That(entity.IsAlive(), Is.True, "Is the entity alive?");
            That(hasTag, Is.True, "If an entity has a tag?");
        });
    }

    private struct TestTag { }

    private class TestEntityDescriptor : IEntityDescriptor
    {
        public ComponentType[] Archetype { get; } = new ComponentType[]
        {
            typeof(TestTag)
        };
    }
}
