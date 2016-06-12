public abstract class MSStockpilerWindow
{
    public const string InterfaceName = "MSStockpilerWindow";
    public const string InterfaceSetItemToStock = "SetItemToStock";
    public const string InterfaceSetStockpile = "SetStockpile";

    public static bool SetItemToStock(Player player, MSStockpiler port, ItemBase itemtostock)
    {
        port.SetItemToStock(itemtostock);
        port.MarkDirtyDelayed();
        if (!WorldScript.mbIsServer)
            NetworkManager.instance.SendInterfaceCommand("MSStockpilerWindow", "SetItemToStock", (string)null, itemtostock, (SegmentEntity)port, 0.0f);
        return true;
    }

    public static bool SetStockpile(Player player, MSStockpiler port, int stockpile)
    {
        port.SetStockpile(stockpile);
        port.MarkDirtyDelayed();
        if (!WorldScript.mbIsServer)
            NetworkManager.instance.SendInterfaceCommand("MSStockpilerWindow", "SetStockpile", stockpile.ToString(), (ItemBase)null, (SegmentEntity)port, 0.0f);
        return true;
    }

    public static NetworkInterfaceResponse HandleNetworkCommand(Player player, NetworkInterfaceCommand nic)
    {
        MSStockpiler port = nic.target as MSStockpiler;
        string key = nic.command;
        if (key != null)
        {
            if (key == "SetItemToStock")
            {
                MSStockpilerWindow.SetItemToStock(player, port, nic.itemContext);
            }
            else if (key == "SetStockpile")
            {
                int stockpile;
                if (int.TryParse(nic.payload ?? "0", out stockpile))
                    port.SetStockpile(stockpile);
            }
        }
        return new NetworkInterfaceResponse()
        {
            entity = (SegmentEntity)port,
            inventory = player.mInventory
        };
    }
}

