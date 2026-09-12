namespace AzuCraftyBoxes.IContainers;

public interface IContainer
{
    public int ProcessContainerInventory(string reqName, int totalAmount, int totalRequirement);
    public int ItemCount(string name);
    public void RemoveItem(string name, int amount);
    public Vector3 GetPosition();
    public void Save();
    public string GetPrefabName();
    public Inventory? GetInventory();

    // Reverse of pulling: moves stacks from playerInventory into this
    // container for every item type the container already contains --
    // returns how many item stacks were moved.
    public int PushMatchingStacks(Inventory playerInventory);
}

static class IContainerExtensions
{
    public static bool ContainsItem(this IContainer container, string name, int amount, out int result)
    {
        result = container.ItemCount(name);
        return result >= amount;
    }
}