using DockLauncher.BuildingBlocks.Domain.Abstractions;
using DockLauncher.BuildingBlocks.Domain.Guards;

namespace DockLauncher.Modules.Groups.Domain;

public sealed class Group : AggregateRoot<Guid>
{
    private readonly List<Guid> _itemIds = [];

    public Group(Guid id, string name)
        : base(id)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
    }

    public string Name { get; private set; }

    public IReadOnlyCollection<Guid> ItemIds => _itemIds;

    public void Rename(string name)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
    }

    public void AddItem(Guid itemId)
    {
        if (!_itemIds.Contains(itemId))
        {
            _itemIds.Add(itemId);
        }
    }

    public void RemoveItem(Guid itemId)
    {
        _itemIds.Remove(itemId);
    }
}
